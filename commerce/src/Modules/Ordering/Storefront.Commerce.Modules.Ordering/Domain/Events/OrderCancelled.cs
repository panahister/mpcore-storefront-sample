using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Ordering.Domain.Events;

public sealed record OrderCancelled : IntegrationEvent, IOrderEvent
{
    public const string Name = "storefront.ordering.order-cancelled";

    public OrderCancelled(
        Guid orderId, string orderNumber, int aggregateVersion, string reason, bool refundRequired, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, Guid.CreateVersion7())
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        AggregateVersion = aggregateVersion;
        Reason = reason;
        RefundRequired = refundRequired;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public int AggregateVersion { get; init; }

    public string Reason { get; init; }

    public bool RefundRequired { get; init; }
}
