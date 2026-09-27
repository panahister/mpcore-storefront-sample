using MPCore.Domain.Model;
using Storefront.Fulfillment.Domain.Events;
using Storefront.Fulfillment.Domain.Rules;

namespace Storefront.Fulfillment.Domain;

/// <summary>
/// One paid order, as the warehouse sees it: what to pick, where to send it, and whether it has left.
/// </summary>
/// <remarks>
/// <para>
/// <b>One shipment per order, keyed by the order.</b> That single choice makes the warehouse idempotent: the
/// same order announced twice finds its shipment and adds nothing. Gregor Hohpe and Bobby Woolf call this
/// the <i>Idempotent Receiver</i> (<i>Enterprise Integration Patterns</i>).
/// </para>
/// <para>
/// The shipment holds a copy of what the shop told it: lines and address. It never asks the shop again, so
/// the warehouse keeps working while the shop is down.
/// </para>
/// </remarks>
public sealed class Shipment : AggregateRoot<Guid>
{
    private readonly List<ShipmentLine> lines = [];

    private Shipment()
    {
        OrderNumber = string.Empty;
        Address = null!;
    }

    private Shipment(Guid orderId, string orderNumber, DeliveryAddress address, IEnumerable<ShipmentLine> shipmentLines, DateTimeOffset now)
        : base(orderId)
    {
        OrderNumber = orderNumber;
        Address = address;
        lines.AddRange(shipmentLines);
        Status = ShipmentStatus.Pending;
        ReceivedOnUtc = now;
    }

    public Guid OrderId => Id;

    /// <summary>The number people quote.</summary>
    public string OrderNumber { get; private set; }

    public DeliveryAddress Address { get; private set; }

    public IReadOnlyList<ShipmentLine> Lines => lines;

    public ShipmentStatus Status { get; private set; }

    public string? Carrier { get; private set; }

    public string? TrackingCode { get; private set; }

    public DateTimeOffset ReceivedOnUtc { get; private set; }

    public DateTimeOffset? DispatchedOnUtc { get; private set; }

    public int ItemCount => lines.Sum(static l => l.Quantity);

    public static Shipment Receive(
        Guid orderId, string orderNumber, DeliveryAddress address, IReadOnlyList<ShipmentLine> shipmentLines, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(shipmentLines);
        CheckRule(new ShipmentMustHaveLines(shipmentLines.Count));

        return new Shipment(orderId, orderNumber, address, shipmentLines, now);
    }

    public void Dispatch(string carrier, string trackingCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrier);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingCode);
        CheckRule(new ShipmentMustBePending(this));

        Status = ShipmentStatus.Dispatched;
        Carrier = carrier;
        TrackingCode = trackingCode;
        DispatchedOnUtc = now;
        Raise(new ShipmentDispatched(Id, OrderNumber, carrier, trackingCode, now));
    }
}
