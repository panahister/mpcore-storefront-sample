using Microsoft.Extensions.Logging;
using MPCore.Application.Time;
using MPCore.Persistence.Abstractions;
using Storefront.Fulfillment.Application.Contracts;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Application.Events;

/// <summary>An order is paid: the warehouse has a parcel to prepare. Arrives from RabbitMQ.</summary>
/// <remarks>
/// <para>
/// A broker delivers at least once. Two guards stand in front of a second shipment. MP Core's inbox stops a
/// second delivery of the same event, by its <c>EventId</c>, before this handler runs. And the shipment is
/// keyed by the order, so the same order announced in a second event finds its shipment here.
/// </para>
/// <para>
/// The first guard is the framework's and covers every consumer; the second is this service's, because
/// only it knows that an order is shipped once (MP Core ADR-013 §1).
/// </para>
/// </remarks>
public static class OrderReadyToShipHandler
{
    public static async Task Handle(
        OrderReadyToShip message,
        IShipmentRepository shipments,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<OrderReadyToShip> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (await shipments.GetAsync(message.OrderId, cancellationToken).ConfigureAwait(false) is not null)
        {
            logger.LogInformation("Order {OrderNumber} already has a shipment; nothing added", message.OrderNumber);
            return;
        }

        var to = message.ShipTo;
        shipments.Add(Shipment.Receive(
            message.OrderId, message.OrderNumber,
            new DeliveryAddress(to.RecipientName, to.Phone, to.Province, to.City, to.Line, to.PostalCode),
            [.. message.Lines.Select(static l => new ShipmentLine(l.Sku, l.ProductName, l.Quantity))],
            clock.UtcNow));
        logger.LogInformation("Order {OrderNumber} received for shipping: {LineCount} line(s)", message.OrderNumber, message.Lines.Count);
    }
}
