using MPCore.Persistence.Abstractions;
using Storefront.Analytics.Application.Contracts;
using Storefront.Analytics.Application.Ports;
using Storefront.Analytics.Domain;

namespace Storefront.Analytics.Application.Events;

/// <summary>Turns the shop's order events into facts. Arrives from Kafka.</summary>
/// <remarks>
/// Kafka delivers at least once, and a reader that starts again reads again. MP Core's inbox stops a second
/// delivery of the same event before a handler runs (ADR-013); the fact is keyed by the event as well, so
/// each of the two guards would be enough on its own.
/// </remarks>
public static class OrderEventsHandler
{
    public static async Task Handle(OrderPlaced message, IOrderFactRepository facts, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (await facts.GetAsync(message.EventId, cancellationToken).ConfigureAwait(false) is null)
        {
            facts.Add(OrderFact.Placed(
                message.EventId, message.OccurredOnUtc, message.OrderId, message.OrderNumber, message.Total, message.Currency,
                message.Lines.Sum(static l => l.Quantity), message.Province, message.City));
        }
    }

    public static async Task Handle(OrderPaid message, IOrderFactRepository facts, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (await facts.GetAsync(message.EventId, cancellationToken).ConfigureAwait(false) is null)
        {
            facts.Add(OrderFact.Paid(message.EventId, message.OccurredOnUtc, message.OrderId, message.OrderNumber, message.Total, message.Currency));
        }
    }

    public static async Task Handle(OrderCancelled message, IOrderFactRepository facts, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (await facts.GetAsync(message.EventId, cancellationToken).ConfigureAwait(false) is null)
        {
            facts.Add(OrderFact.Cancelled(message.EventId, message.OccurredOnUtc, message.OrderId, message.OrderNumber, message.Reason));
        }
    }
}
