using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Domain.Events;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;
using Storefront.Commerce.Modules.Ordering.Application.Commands;
using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Domain;
using ResultFailureException = MPCore.Application.Results.ResultFailureException;

namespace Storefront.Commerce.Api.Hosting;

/// <summary>
/// The warehouse's word that a parcel has left, as this host reads it.
/// </summary>
/// <remarks>
/// Published by Storefront Fulfillment as <c>storefront.fulfillment.shipment-dispatched</c>, version 1. The
/// two services share no assembly: each declares the contract in its own code, and the two agree on the
/// name, the version and the JSON. This is the consumer's copy.
/// </remarks>
public sealed record ShipmentDispatched : IntegrationEvent
{
    public const string Name = "storefront.fulfillment.shipment-dispatched";

    public ShipmentDispatched(Guid eventId, Guid orderId, string orderNumber, string carrier, string trackingCode, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, eventId)
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

/// <summary>
/// The RabbitMQ consumer seam between the warehouse and the Ordering module.
/// </summary>
/// <remarks>
/// <para>
/// The Ordering module must not know that a warehouse service exists, or what its messages look like. The
/// host is the only place that knows both, so the host receives the integration event and translates it
/// into Ordering's own command, exactly as <see cref="CatalogEventsConsumer"/> does for a price change.
/// </para>
/// <para>
/// MP Core's inbox stops a second delivery of the same event before this runs. News about an order that
/// has already left is accepted and changes nothing. A failure the command returns is thrown, so that
/// the host's error policy decides about the message.
/// </para>
/// </remarks>
public static class FulfillmentEventsConsumer
{
    /// <summary>Consumes <see cref="ShipmentDispatched"/>.</summary>
    public static async Task Handle(
        ShipmentDispatched message,
        IOrderRepository orders,
        IMessagePublisher publisher,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(orders);

        // A paid order can also be shipped at this host's own gRPC door. The warehouse's word about an order
        // that has already left is then old news, not a fault: Gregor Hohpe's idempotent receiver, in business
        // terms. The inbox cannot know this; it only knows whether it has seen this event before.
        var known = await orders.GetAsync(new OrderId(message.OrderId), cancellationToken).ConfigureAwait(false);
        if (known is { Status: OrderStatus.Shipped })
        {
            return;
        }

        var result = await ShipOrderHandler.Handle(
            new ShipOrder(message.OrderId, message.Carrier, message.TrackingCode),
            orders, publisher, audit, unitOfWork, clock, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new ResultFailureException(result.FailureDescriptor!);
        }
    }
}
