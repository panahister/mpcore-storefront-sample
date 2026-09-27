using MPCore.Domain.Events;

namespace Storefront.Fulfillment.Domain.Events;

/// <summary>The parcel left the warehouse. The shop marks the order shipped and tells the shopper.</summary>
public sealed record ShipmentDispatched : IntegrationEvent
{
    public const string Name = "storefront.fulfillment.shipment-dispatched";

    public ShipmentDispatched(Guid orderId, string orderNumber, string carrier, string trackingCode, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, Guid.CreateVersion7())
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        Carrier = carrier;
        TrackingCode = trackingCode;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public string Carrier { get; init; }

    public string TrackingCode { get; init; }
}
