using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Domain.Rules;
using MPCore.Persistence.Abstractions;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Tests;

// Hand-written fakes for the ports. Every handler takes its collaborators as parameters, so a rule is
// tested in microseconds with no container, no database and no mocking library.

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public static FakeClock At2026() => new(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero));
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("A handler must not call SaveChangesAsync; MP Core's transaction middleware saves.");
}

public sealed class FakeAudit : IBusinessAuditRecorder
{
    public List<(string Action, AuditOutcome Outcome, IReadOnlyDictionary<string, string>? Metadata)> Records { get; } = [];

    public ValueTask RecordAsync(string module, string action, string? entityType = null, string? entityId = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        Records.Add((action, AuditOutcome.Succeeded, metadata));
        return ValueTask.CompletedTask;
    }

    public ValueTask RecordAttemptAsync(string module, string action, AuditOutcome outcome, AuditFailure? failure = null,
        string? reason = null, string? entityType = null, string? entityId = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        Records.Add((action, outcome, metadata));
        return ValueTask.CompletedTask;
    }
}

public sealed class FakeShipments : IShipmentRepository
{
    private readonly Dictionary<Guid, Shipment> items = [];

    public IReadOnlyCollection<Shipment> All => items.Values;

    public Task<Shipment?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.GetValueOrDefault(id));

    public void Add(Shipment aggregate) => items[aggregate.Id] = aggregate;

    public void Remove(Shipment aggregate) => items.Remove(aggregate.Id);
}

public static class Rules
{
    /// <summary>The code of the business rule the action breaks; fails the test when no rule breaks.</summary>
    public static string Broken(Action action) => Assert.Throws<BusinessRuleValidationException>(action).Rule.Code;

    public static async Task<string> BrokenAsync(Func<Task> action) =>
        (await Assert.ThrowsAsync<BusinessRuleValidationException>(action)).Rule.Code;
}
