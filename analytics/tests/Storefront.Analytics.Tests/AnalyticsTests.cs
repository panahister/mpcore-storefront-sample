using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MPCore.Application.Time;
using MPCore.Caching.Abstractions;
using MPCore.Domain.Events;
using MPCore.Persistence.Abstractions;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;
using NetArchTest.Rules;
using Storefront.Analytics.Application.Contracts;
using Storefront.Analytics.Application.Events;
using Storefront.Analytics.Application.Ports;
using Storefront.Analytics.Application.Queries;
using Storefront.Analytics.Application.Views;
using Storefront.Analytics.Domain;
using Storefront.Analytics.Infrastructure.Persistence;

namespace Storefront.Analytics.Tests;

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("A handler must not call SaveChangesAsync; MP Core's transaction middleware saves.");
}

public sealed class FakeFacts : IOrderFactRepository
{
    private readonly Dictionary<Guid, OrderFact> items = [];

    public IReadOnlyCollection<OrderFact> All => items.Values;

    public Task<OrderFact?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.GetValueOrDefault(id));

    public void Add(OrderFact aggregate) => items[aggregate.Id] = aggregate;

    public void Remove(OrderFact aggregate) => items.Remove(aggregate.Id);
}

/// <summary>A cache that keeps everything, for ever: what a test needs to see whether it was asked.</summary>
public sealed class FakeCache : IReadThroughCache
{
    private readonly Dictionary<string, object?> items = [];

    public IReadOnlyCollection<string> Keys => items.Keys;

    public async ValueTask<T> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, ValueTask<T>> factory, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        if (!items.TryGetValue(key, out var value))
        {
            items[key] = value = await factory(cancellationToken);
        }

        return (T)value!;
    }
}

public sealed class RecordingSales : ISalesReadModel
{
    public (DateTimeOffset From, DateTimeOffset To)? Asked { get; private set; }

    public int Reads { get; private set; }

    public Task<IReadOnlyList<HourlySales>> HourlyAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        Asked = (from, to);
        Reads++;
        return Task.FromResult<IReadOnlyList<HourlySales>>([new HourlySales(from, "USD", 2, 300m)]);
    }

    public Task<IReadOnlyList<RegionalSales>> ByRegionAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        Asked = (from, to);
        return Task.FromResult<IReadOnlyList<RegionalSales>>([]);
    }
}

[Trait("Category", "Unit")]
public sealed class OrderFactTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeFacts facts = new();

    [Fact]
    public async Task Each_event_becomes_one_fact_under_the_events_identity()
    {
        var order = Guid.NewGuid();
        var placed = new OrderPlaced(Guid.CreateVersion7(), order, "ORD-1", "USD", 300m,
            [new OrderPlacedLine("TNT-1", 2, 100m), new OrderPlacedLine("STV-1", 1, 100m)], "California", "San Francisco", Now);
        var paid = new OrderPaid(Guid.CreateVersion7(), order, "ORD-1", 300m, "USD", Now.AddSeconds(2));
        var cancelled = new OrderCancelled(Guid.CreateVersion7(), order, "ORD-1", "CUSTOMER_REQUEST", Now.AddMinutes(5));

        await OrderEventsHandler.Handle(placed, facts, new FakeUnitOfWork(), CancellationToken.None);
        await OrderEventsHandler.Handle(paid, facts, new FakeUnitOfWork(), CancellationToken.None);
        await OrderEventsHandler.Handle(cancelled, facts, new FakeUnitOfWork(), CancellationToken.None);

        Assert.Equal(
            [(OrderFactKind.Placed, 300m, 3, "San Francisco"), (OrderFactKind.Paid, 300m, 0, null), (OrderFactKind.Cancelled, 0m, 0, null)],
            facts.All.OrderBy(static f => f.OccurredOnUtc).Select(static f => (f.Kind, f.Amount, f.ItemCount, f.City)));
        Assert.Equal([placed.EventId, paid.EventId, cancelled.EventId], facts.All.OrderBy(static f => f.OccurredOnUtc).Select(static f => f.Id));
    }

    [Fact]
    public async Task An_event_read_twice_is_counted_once()
    {
        var paid = new OrderPaid(Guid.CreateVersion7(), Guid.NewGuid(), "ORD-1", 300m, "USD", Now);

        await OrderEventsHandler.Handle(paid, facts, new FakeUnitOfWork(), CancellationToken.None);
        await OrderEventsHandler.Handle(paid, facts, new FakeUnitOfWork(), CancellationToken.None);

        Assert.Single(facts.All);
    }
}

[Trait("Category", "Unit")]
public sealed class SalesReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Without_a_period_the_hourly_report_covers_the_last_day()
    {
        var sales = new RecordingSales();

        var report = await SalesReportsHandler.Handle(new GetHourlySales(null, null), sales, new FakeCache(), new FakeClock(Now), CancellationToken.None);

        // The minute that has begun is part of the report: an order paid a second ago is in it.
        Assert.Equal((Now.AddMinutes(1).AddHours(-24), Now.AddMinutes(1)), sales.Asked);
        Assert.Equal(300m, Assert.Single(report.Value.Rows).Revenue);
    }

    [Fact]
    public async Task The_same_report_asked_for_a_moment_later_is_not_read_again()
    {
        var sales = new RecordingSales();
        var cache = new FakeCache();
        var clock = new FakeClock(Now.AddSeconds(5));

        var first = await SalesReportsHandler.Handle(new GetHourlySales(null, null), sales, cache, clock, CancellationToken.None);
        clock.UtcNow = Now.AddSeconds(40);
        var second = await SalesReportsHandler.Handle(new GetHourlySales(null, null), sales, cache, clock, CancellationToken.None);

        Assert.Equal(1, sales.Reads);
        Assert.Equal(first.Value.To, second.Value.To);
        Assert.Equal("analytics:sales:hourly:202609261001:202609271001", Assert.Single(cache.Keys));
    }

    [Fact]
    public async Task Another_period_is_another_report()
    {
        var sales = new RecordingSales();
        var cache = new FakeCache();

        await SalesReportsHandler.Handle(new GetHourlySales(null, null), sales, cache, new FakeClock(Now), CancellationToken.None);
        await SalesReportsHandler.Handle(new GetHourlySales(null, null), sales, cache, new FakeClock(Now.AddMinutes(1)), CancellationToken.None);

        Assert.Equal(2, sales.Reads);
    }

    [Theory]
    [InlineData("2026-09-27T10:00:00Z", "2026-09-27T09:00:00Z", "PERIOD_INVALID")]
    [InlineData("2026-09-27T10:00:00Z", "2026-09-27T10:00:00Z", "PERIOD_INVALID")]
    [InlineData("2026-08-01T00:00:00Z", "2026-09-27T10:00:00Z", "PERIOD_TOO_LONG")]
    public async Task A_period_that_makes_no_sense_is_refused_before_anything_is_read(string from, string to, string rule)
    {
        var sales = new RecordingSales();

        var report = await SalesReportsHandler.Handle(
            new GetSalesByRegion(DateTimeOffset.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateTimeOffset.Parse(to, System.Globalization.CultureInfo.InvariantCulture)),
            sales, new FakeCache(), new FakeClock(Now), CancellationToken.None);

        var violation = Assert.IsType<MPCore.Application.Results.ValidationFailureDetail>(Assert.Single(report.FailureDescriptor!.Details)).Violations.Single();
        Assert.Equal(rule, violation.RuleCode);
        Assert.Null(sales.Asked);
    }
}

[Trait("Category", "Architecture")]
public sealed class ArchitectureTests
{
    private static readonly Assembly Application = typeof(Storefront.Analytics.Application.AssemblyReference).Assembly;

    [Fact]
    public void The_domain_and_the_application_name_no_provider()
    {
        foreach (var assembly in new[] { typeof(OrderFact).Assembly, Application })
        {
            var result = Types.InAssembly(assembly).ShouldNot()
                .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "Wolverine", "Confluent.Kafka", "Microsoft.AspNetCore")
                .GetResult();
            Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void A_query_handler_changes_nothing()
    {
        var parameters = Application.GetTypes()
            .Where(static t => t.Namespace == "Storefront.Analytics.Application.Queries" && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(static m => m.Name == "Handle"))
            .SelectMany(static m => m.GetParameters().Select(static p => p.ParameterType))
            .ToList();

        Assert.NotEmpty(parameters);
        Assert.DoesNotContain(typeof(IUnitOfWork), parameters);
        Assert.DoesNotContain(typeof(IOrderFactRepository), parameters);
    }

    [Fact]
    public void The_facts_are_keyed_by_event_and_time_and_the_migrations_match_the_model()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        PostgreSqlDbContextOptions.Apply(options, "Host=model-only;Database=none;Username=none;Password=none");
        using var context = new AppDbContext(options.Options, TimeProvider.System, NullAggregateEventSink.Instance);

        var fact = context.Model.FindEntityType(typeof(OrderFact))!;
        Assert.Equal(("analytics", "order_facts"), (fact.GetSchema(), fact.GetTableName()));
        // A hypertable's unique indexes contain its time column.
        Assert.Equal(["event_id", OrderFactConfiguration.TimeColumn], fact.FindPrimaryKey()!.Properties.Select(static p => p.GetColumnName()));
        Assert.Contains(context.Model.GetEntityTypes(), static e => e.GetSchema() == "idempotency" && e.GetTableName() == "processed_messages");
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void The_migration_makes_the_table_a_hypertable()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Storefront.Analytics.Backend.sln")))
        {
            directory = directory.Parent;
        }

        var migration = Directory.GetFiles(Path.Combine(directory!.FullName, "src/Storefront.Analytics.Infrastructure/Migrations"), "*_InitialAnalytics.cs").Single();
        Assert.Contains("CreateHypertable(\"order_facts\", \"occurred_on_utc\", schema: \"analytics\"", File.ReadAllText(migration), StringComparison.Ordinal);
    }
}
