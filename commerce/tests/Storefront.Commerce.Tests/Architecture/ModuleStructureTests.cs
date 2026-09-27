using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using MPCore.Application.Messaging;
using MPCore.Audit;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;
using NetArchTest.Rules;

namespace Storefront.Commerce.Tests.Architecture;

/// <summary>
/// One project per bounded context, layers as folders (Simon Brown's <i>package by component</i>): the
/// compiler guards the boundary between modules, and these tests guard the boundary between the layer
/// folders, reading the compiled assemblies (NetArchTest, as Jason Taylor's Clean Architecture template does).
/// </summary>
[Trait("Category", "Architecture")]
public sealed partial class ModuleStructureTests
{
    private static readonly string[] Modules = ["Catalog", "Basket", "Ordering", "Payments"];

    private static readonly string[] Providers =
    [
        "Microsoft.EntityFrameworkCore", "Npgsql", "Wolverine", "Confluent.Kafka", "StackExchange.Redis",
        "Microsoft.AspNetCore", "Grpc", "Polly", "System.Net.Http"
    ];

    private static Assembly ModuleAssembly(string module) => Assembly.Load($"Storefront.Commerce.Modules.{module}");

    private static string Root(string module) => $"Storefront.Commerce.Modules.{module}";

    public static TheoryData<string> AllModules() => [.. Modules];

    private static void AssertSuccess(TestResult result) =>
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));

    [Theory]
    [MemberData(nameof(AllModules))]
    public void The_domain_folder_depends_on_MPCore_Domain_and_nothing_else_of_ours(string module)
    {
        AssertSuccess(Types.InAssembly(ModuleAssembly(module))
            .That().ResideInNamespaceStartingWith($"{Root(module)}.Domain")
            .ShouldNot().HaveDependencyOnAny(
            [
                .. Providers, "FluentValidation", "MPCore.Application", "MPCore.Persistence", "MPCore.Messaging",
                "MPCore.Security", "MPCore.Audit", "MPCore.Caching", $"{Root(module)}.Application", $"{Root(module)}.Infrastructure"
            ])
            .GetResult());
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void The_application_folder_names_no_provider_broker_transport_or_infrastructure(string module)
    {
        AssertSuccess(Types.InAssembly(ModuleAssembly(module))
            .That().ResideInNamespaceStartingWith($"{Root(module)}.Application")
            .ShouldNot().HaveDependencyOnAny([.. Providers, $"{Root(module)}.Infrastructure"])
            .GetResult());
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void A_module_references_only_contracts_of_other_modules_never_their_projects(string module)
    {
        var references = ModuleAssembly(module).GetReferencedAssemblies().Select(static a => a.Name!)
            .Where(static name => name.StartsWith("Storefront.Commerce.Modules.", StringComparison.Ordinal));

        Assert.All(references, name => Assert.True(
            name.EndsWith(".Contracts", StringComparison.Ordinal),
            $"{module} references {name}, which is another module's project rather than its Contracts"));
    }

    [Fact]
    public void Contracts_projects_hold_only_records_and_interfaces_and_reference_nothing_of_ours()
    {
        foreach (var module in new[] { "Catalog", "Basket", "Payments" })
        {
            var contracts = Assembly.Load($"Storefront.Commerce.Modules.{module}.Contracts");
            Assert.DoesNotContain(contracts.GetReferencedAssemblies(), static a => a.Name!.StartsWith("Storefront", StringComparison.Ordinal) || a.Name.StartsWith("MPCore", StringComparison.Ordinal));
            Assert.All(contracts.GetExportedTypes(), type => Assert.True(
                type.IsInterface || type.GetMethod("<Clone>$") is not null,
                $"{type.Name} in {module}.Contracts is neither an interface nor a record"));
        }
    }

    [Fact]
    public void A_module_offers_other_modules_reads_and_messages_but_no_way_to_write_its_data()
    {
        // A module writes only its own data; the others learn through messages (Vaughn Vernon: one
        // aggregate per transaction; Kamil Grzybek: modules integrate through events). An interface in a
        // Contracts project is a synchronous call across the boundary, so each one is listed here, and each
        // one listed only reads: a price for the basket, and whether a payment intent can pay.
        var interfaces = new[] { "Catalog", "Basket", "Payments" }
            .SelectMany(static module => Assembly.Load($"Storefront.Commerce.Modules.{module}.Contracts").GetExportedTypes())
            .Where(static type => type.IsInterface)
            .Select(static type => type.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["ICatalogLookup", "IPaymentIntentLookup"], interfaces);
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void A_handler_takes_repositories_of_its_own_module_only(string module)
    {
        var repositories = ModuleAssembly(module).GetTypes()
            .Where(static t => t.IsClass && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(static m => m.Name == "Handle"))
            .SelectMany(static m => m.GetParameters().Select(static p => p.ParameterType))
            .Where(static type => type.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<,>)));

        Assert.All(repositories, type => Assert.StartsWith($"{Root(module)}.", type.Namespace, StringComparison.Ordinal));
    }

    private static IEnumerable<Type> SecretCarriers()
    {
        // Every message of ours with a property whose name says it is a secret. A new one joins by itself.
        var assemblies = Modules.Select(ModuleAssembly)
            .Concat(new[] { "Catalog", "Basket", "Payments" }.Select(static m => Assembly.Load($"Storefront.Commerce.Modules.{m}.Contracts")));
        return assemblies.SelectMany(static a => a.GetExportedTypes())
            .Where(static t => t.GetMethod("<Clone>$") is not null)
            .Where(static t => t.GetProperties().Any(static p => p.PropertyType == typeof(string) && IsSecret(p.Name)));
    }

    public static TheoryData<Type> MessagesThatCarryASecret() => [.. SecretCarriers()];

    private static bool IsSecret(string propertyName) =>
        propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase);

    [Theory]
    [MemberData(nameof(MessagesThatCarryASecret))]
    public void A_message_never_prints_its_secret(Type message)
    {
        // Wolverine logs a message whose handling failed by printing it, and a record prints every
        // property unless it says otherwise. Found by running the scenarios: a refused checkout wrote the
        // payment token and the shopper's phone number into the log.
        const string Secret = "tok_must_never_be_printed";
        var constructor = message.GetConstructors().OrderByDescending(static c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select(p =>
            p.ParameterType == typeof(string) ? (IsSecret(p.Name!) ? Secret : "text")
            : p.ParameterType == typeof(Storefront.Commerce.Modules.Basket.Contracts.CheckoutAddress)
                ? new Storefront.Commerce.Modules.Basket.Contracts.CheckoutAddress("Sara Ahmadi", "+14155550123", "California", "San Francisco", "12 Harbour Street", "94103")
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType)
            : p.ParameterType.IsGenericType ? Array.CreateInstance(p.ParameterType.GenericTypeArguments[0], 0)
            : null).ToArray();

        var printed = constructor.Invoke(arguments).ToString()!;

        Assert.DoesNotContain(Secret, printed, StringComparison.Ordinal);
        Assert.DoesNotContain("+14155550123", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("Sara Ahmadi", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_request_to_payments_carries_the_payment_token()
    {
        var names = SecretCarriers().Select(static type => type.Name).Order(StringComparer.Ordinal);
        // Only the request that hands the token to Payments carries it; no message between
        // modules does.
        Assert.Equal(["CreatePaymentIntent"], names);
    }

    [Fact]
    public void Only_ordering_and_basket_use_other_modules_published_contracts()
    {
        static string[] Ours(string module) => [.. ModuleAssembly(module).GetReferencedAssemblies().Select(static a => a.Name!).Where(static n => n.StartsWith("Storefront.Commerce.Modules.", StringComparison.Ordinal)).Order()];

        Assert.Equal(["Storefront.Commerce.Modules.Catalog.Contracts"], Ours("Catalog"));
        Assert.Equal(["Storefront.Commerce.Modules.Payments.Contracts"], Ours("Payments"));
        Assert.Equal(["Storefront.Commerce.Modules.Basket.Contracts", "Storefront.Commerce.Modules.Catalog.Contracts", "Storefront.Commerce.Modules.Payments.Contracts"], Ours("Basket"));
        Assert.Equal(["Storefront.Commerce.Modules.Basket.Contracts", "Storefront.Commerce.Modules.Catalog.Contracts", "Storefront.Commerce.Modules.Payments.Contracts"], Ours("Ordering"));
    }

    [Fact]
    public void The_api_does_not_reach_into_module_infrastructure()
    {
        AssertSuccess(Types.InAssembly(typeof(Api.Hosting.HandlerAssemblies).Assembly)
            .ShouldNot().HaveDependencyOnAny([.. Modules.Select(m => $"{Root(m)}.Infrastructure")])
            .GetResult());
    }

    [Fact]
    public void Every_module_is_listed_for_handler_and_validator_discovery()
    {
        var listed = Api.Hosting.HandlerAssemblies.All.Select(static a => a.GetName().Name).ToArray();
        foreach (var module in Modules)
        {
            Assert.Contains(Root(module), listed);
        }
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void A_class_with_a_handle_method_is_named_so_that_Wolverine_discovers_it(string module)
    {
        var handlers = ModuleAssembly(module).GetTypes()
            .Where(static t => t.IsClass && t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Any(static m => m.Name == "Handle"));

        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler => Assert.True(
            handler.Name.EndsWith("Handler", StringComparison.Ordinal) || handler.Name.EndsWith("Consumer", StringComparison.Ordinal),
            $"{handler.FullName} has a Handle method but Wolverine would never discover it"));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Every_command_a_caller_can_send_has_a_validator(string module)
    {
        // Internal module messages (in Contracts) come from other modules' code, not from a caller, so they
        // need no validator. These two application commands carry no caller input either.
        string[] exempt = ["AcknowledgeBasketPrices", "ApplyCatalogPriceChange"];
        var assembly = ModuleAssembly(module);
        var commands = assembly.GetExportedTypes()
            .Where(t => t.Namespace == $"{Root(module)}.Application.Commands" && !exempt.Contains(t.Name))
            .Where(static t => t.GetInterfaces().Any(static i => i == typeof(ICommand) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))))
            .ToList();

        foreach (var command in commands)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(command);
            Assert.True(assembly.GetTypes().Any(t => !t.IsAbstract && validatorType.IsAssignableFrom(t)), $"{command.Name} has no validator");
        }
    }

    // ---- Command-query separation (Bertrand Meyer), applied to messages ------------------------------

    private static bool IsCommand(Type type) =>
        type.GetInterfaces().Any(static i => i == typeof(ICommand) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

    private static bool IsQuery(Type type) =>
        type.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Commands_and_queries_each_live_in_their_own_folder(string module)
    {
        foreach (var type in ModuleAssembly(module).GetExportedTypes())
        {
            if (IsCommand(type))
            {
                Assert.Equal($"{Root(module)}.Application.Commands", type.Namespace);
            }

            if (IsQuery(type))
            {
                Assert.Equal($"{Root(module)}.Application.Queries", type.Namespace);
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void A_query_handler_only_reads_and_reads_through_a_read_model(string module)
    {
        var parameters = ModuleAssembly(module).GetTypes()
            .Where(t => t.Namespace == $"{Root(module)}.Application.Queries" && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(static m => m.Name == "Handle"))
            .SelectMany(static m => m.GetParameters().Select(p => (Handler: m.DeclaringType!.Name, Type: p.ParameterType)));

        foreach (var (handler, type) in parameters)
        {
            // No unit of work, so nothing can be saved; no publisher, no audit of a business action; and
            // no repository, because a repository hands out aggregates and a query returns views.
            Assert.False(type == typeof(IUnitOfWork), $"{handler} declares IUnitOfWork: a query changes nothing");
            Assert.False(type == typeof(IMessagePublisher), $"{handler} publishes: a query changes nothing");
            Assert.False(type == typeof(IBusinessAuditRecorder), $"{handler} records a business action: a query changes nothing");
            Assert.False(
                type.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<,>)),
                $"{handler} reads through the repository {type.Name}; a query reads through a read-model port");
        }
    }

    [Fact]
    public void An_http_get_only_ever_sends_a_query()
    {
        // RFC 9110: GET is safe. A retry, a prefetch or a cache must never change anything, so no GET
        // endpoint may send a command. Read from the endpoint source, where the mapping is written.
        var commands = Modules.Select(ModuleAssembly).Append(typeof(Api.Hosting.HandlerAssemblies).Assembly)
            .SelectMany(static a => a.GetExportedTypes()).Where(IsCommand).Select(static t => t.Name).ToHashSet(StringComparer.Ordinal);
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Storefront.Commerce.Api", "Rest", "Endpoints", "StorefrontEndpoints.cs"));

        var gets = GetMapping().Matches(source);
        Assert.NotEmpty(gets);
        foreach (Match get in gets)
        {
            foreach (Match message in Construction().Matches(get.Value))
            {
                Assert.False(commands.Contains(message.Groups[1].Value), $"GET {get.Groups[1].Value} sends the command {message.Groups[1].Value}");
            }
        }
    }

    private static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Storefront.Commerce.Backend.sln")))
            {
                return Path.Combine(directory.FullName, "src");
            }
        }

        throw new InvalidOperationException("Storefront.Commerce.Backend.sln not found above the test output.");
    }

    [GeneratedRegex("\\.MapGet\\(\"([^\"]*)\".*?(?=\\.Map(?:Get|Post|Put|Delete|Patch|Group)\\(|\\z)", RegexOptions.Singleline)]
    private static partial Regex GetMapping();

    [GeneratedRegex("new ([A-Z][A-Za-z0-9]*)\\(")]
    private static partial Regex Construction();
}
