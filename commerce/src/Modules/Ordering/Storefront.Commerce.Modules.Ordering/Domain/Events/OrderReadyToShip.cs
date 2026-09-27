using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Ordering.Domain.Events;

/// <summary>
/// The order is paid and can be shipped. It carries a snapshot, everything the warehouse needs, so the
/// warehouse never has to call the shop back.
/// </summary>
/// <remarks>
/// <para>
/// Two events leave when an order is paid, for two kinds of reader. <see cref="OrderPaid"/> goes to the event
/// stream, for whoever wants to know that money was taken; it holds no personal data. This one goes to one
/// queue, read by the warehouse only, and holds the address, because a parcel cannot be sent without one.
/// </para>
/// <para>
/// Gregor Hohpe and Bobby Woolf call the two channels <i>Publish-Subscribe</i> and <i>Point-to-Point</i>
/// (<i>Enterprise Integration Patterns</i>). What may travel on a channel depends on who can read it.
/// </para>
/// </remarks>
public sealed record OrderReadyToShip : IntegrationEvent, IOrderEvent
{
    public const string Name = "storefront.ordering.order-ready-to-ship";

    public OrderReadyToShip(
        Guid orderId, string orderNumber, int aggregateVersion, OrderReadyToShipAddress shipTo,
        IReadOnlyList<OrderReadyToShipLine> lines, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, Guid.CreateVersion7())
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        AggregateVersion = aggregateVersion;
        ShipTo = shipTo;
        Lines = lines;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public int AggregateVersion { get; init; }

    public OrderReadyToShipAddress ShipTo { get; init; }

    public IReadOnlyList<OrderReadyToShipLine> Lines { get; init; }

    /// <summary>What a log may show of this message: which order, never the recipient or the address.</summary>
    public override string ToString() => $"{nameof(OrderReadyToShip)} {{ OrderNumber = {OrderNumber}, Lines = {Lines?.Count ?? 0} }}";
}

public sealed record OrderReadyToShipAddress(string RecipientName, string Phone, string Province, string City, string Line, string PostalCode)
{
    public override string ToString() => $"{nameof(OrderReadyToShipAddress)} {{ Province = {Province}, City = {City} }}";
}

public sealed record OrderReadyToShipLine(string Sku, string ProductName, int Quantity);
