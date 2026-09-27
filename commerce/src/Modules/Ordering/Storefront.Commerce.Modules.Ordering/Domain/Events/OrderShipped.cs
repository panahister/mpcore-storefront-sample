using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Ordering.Domain.Events;

public sealed record OrderShipped : IntegrationEvent, IOrderEvent
{
    public const string Name = "storefront.ordering.order-shipped";

    public OrderShipped(
        Guid orderId, string orderNumber, int aggregateVersion, string carrier, string trackingCode, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, Guid.CreateVersion7())
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        AggregateVersion = aggregateVersion;
        Carrier = carrier;
        TrackingCode = trackingCode;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public int AggregateVersion { get; init; }

    public string Carrier { get; init; }

    public string TrackingCode { get; init; }
}
