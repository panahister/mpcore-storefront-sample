using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MPCore.Application.Messaging;
using MPCore.Audit;
using MPCore.Domain.Events;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;
using NetArchTest.Rules;
using Storefront.Fulfillment.Domain;
using Storefront.Fulfillment.Infrastructure.Persistence;

namespace Storefront.Fulfillment.Tests;

/// <summary>
/// One bounded context, three projects: the compiler guards the direction between the layers, and these
/// tests guard what the compiler cannot see.
/// </summary>
[Trait("Category", "Architecture")]
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Shipment).Assembly;
    private static readonly Assembly Application = typeof(Storefront.Fulfillment.Application.AssemblyReference).Assembly;

    private static readonly string[] Providers =
        ["Microsoft.EntityFrameworkCore", "Npgsql", "Wolverine", "RabbitMQ", "Microsoft.AspNetCore", "Grpc"];

    [Fact]
    public void The_domain_and_the_application_name_no_provider()
    {
        foreach (var assembly in new[] { Domain, Application })
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(Providers).GetResult();
            Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void A_query_handler_only_reads_and_reads_through_a_read_model()
    {
        var parameters = Application.GetTypes()
            .Where(static t => t.Namespace == "Storefront.Fulfillment.Application.Queries" && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(static m => m.Name == "Handle"))
            .SelectMany(static m => m.GetParameters().Select(static p => p.ParameterType))
            .ToList();

        Assert.NotEmpty(parameters);
        Assert.DoesNotContain(typeof(IUnitOfWork), parameters);
        Assert.DoesNotContain(typeof(IMessagePublisher), parameters);
        Assert.DoesNotContain(typeof(IBusinessAuditRecorder), parameters);
        Assert.DoesNotContain(parameters, static type => type.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<,>)));
    }

    [Fact]
    public void Commands_and_queries_each_live_in_their_own_folder()
    {
        foreach (var type in Application.GetExportedTypes())
        {
            var interfaces = type.GetInterfaces();
            if (interfaces.Any(static i => i == typeof(ICommand) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))))
            {
                Assert.Equal("Storefront.Fulfillment.Application.Commands", type.Namespace);
            }

            if (interfaces.Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            {
                Assert.Equal("Storefront.Fulfillment.Application.Queries", type.Namespace);
            }
        }
    }

    [Fact]
    public void The_model_and_its_migrations_agree_and_the_inbox_commits_with_the_shipment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        PostgreSqlDbContextOptions.Apply(options, "Host=model-only;Database=none;Username=none;Password=none");
        using var context = new AppDbContext(options.Options, TimeProvider.System, NullAggregateEventSink.Instance);

        Assert.Equal(("fulfillment", "shipments"), (context.Model.FindEntityType(typeof(Shipment))!.GetSchema(), context.Model.FindEntityType(typeof(Shipment))!.GetTableName()));
        Assert.Contains(context.Model.GetEntityTypes(), static e => e.GetSchema() == "idempotency" && e.GetTableName() == "processed_messages");
        Assert.Contains(context.Model.GetEntityTypes(), static e => e.GetSchema() == "audit");
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
