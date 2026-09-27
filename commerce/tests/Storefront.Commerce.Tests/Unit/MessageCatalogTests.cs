using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Storefront.Commerce.Api.Resources;
using Storefront.Commerce.Modules.Basket.Resources;
using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Catalog.Resources;
using Storefront.Commerce.Modules.Ordering.Resources;
using Storefront.Commerce.Modules.Payments.Resources;
using Storefront.Commerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using MPCore.Application.Results;
using MPCore.Domain.Rules;
using MPCore.Localization;

namespace Storefront.Commerce.Tests.Unit;

/// <summary>
/// No sentence is written in code: a rule, a validator and a returned failure carry a message key, and the
/// catalog renders it in the caller's language. A key without a text would reach the caller as a problem
/// document with no <c>detail</c>, so every key the code uses must have one, in every language shipped.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class MessageCatalogTests
{
    private static IMessageCatalog Catalog()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMPCoreMessageCatalog(catalog => catalog
            .AddResources<CatalogMessages>()
            .AddResources<BasketMessages>()
            .AddResources<OrderingMessages>()
            .AddResources<PaymentsMessages>()
            .AddResources<HostMessages>());
        return services.BuildServiceProvider().GetRequiredService<IMessageCatalog>();
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

    [Fact]
    public void A_broken_rule_renders_in_both_languages_with_its_arguments()
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 10);
        var broken = Assert.Throws<BusinessRuleValidationException>(() => product.ChangePrice(Price.Of(5_000m), FakeClock.At2026().UtcNow));
        var message = new FailureMessageDescriptor(broken.Rule.MessageKey, broken.Rule.MessageArguments);
        var catalog = Catalog();

        Assert.Equal("A price may move by at most 50% in one step (from 1000.00 to 5000.00).", catalog.Localize(message, CultureInfo.GetCultureInfo("en")));
        Assert.Contains("50٪", catalog.Localize(message, CultureInfo.GetCultureInfo("fa-IR")), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_message_key_the_code_uses_has_a_default_text()
    {
        var catalog = Catalog();
        var used = Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(static path => KeyLiteral().Matches(File.ReadAllText(path)).Select(static match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.True(catalog.IsKnownKey(key), $"no default text for message key {key}"));
    }

    [Fact]
    public void Every_language_file_translates_exactly_the_default_keys()
    {
        var files = Directory.EnumerateFiles(SourceRoot(), "*Messages.resx", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(5, files.Count);

        foreach (var defaults in files)
        {
            var persian = defaults[..^".resx".Length] + ".fa.resx";
            Assert.True(File.Exists(persian), $"missing {persian}");
            Assert.Equal(Keys(defaults), Keys(persian));
        }
    }

    private static List<string> Keys(string file) =>
        [.. XDocument.Load(file).Root!.Elements("data").Select(static data => (string)data.Attribute("name")!).Order(StringComparer.Ordinal)];

    [GeneratedRegex("\"((?:catalog|basket|ordering|payments|localization)\\.[a-z][a-z0-9_]*(?:\\.[a-z][a-z0-9_]*)*)\"")]
    private static partial Regex KeyLiteral();
}
