using MPCore.Domain.Events;

namespace Storefront.Fulfillment.Application.Contracts;

/// <summary>
/// The shop's announcement that an order is paid and can be shipped, as this service reads it.
/// </summary>
/// <remarks>
/// <para>
/// Published by Storefront Commerce as <c>storefront.ordering.order-ready-to-ship</c>, version 1. The two
/// services share no assembly: each declares the contract in its own code and the two agree on the name,
/// the version and the JSON. This is the consumer's copy, and it declares only what the warehouse uses.
/// </para>
/// <para>
/// It carries a snapshot, everything the warehouse needs, so the warehouse never calls the shop back.
/// </para>
/// </remarks>
public sealed record OrderReadyToShip : IntegrationEvent
{
    public const string Name = "storefront.ordering.order-ready-to-ship";

    public OrderReadyToShip(
        Guid eventId, Guid orderId, string orderNumber, OrderReadyToShipAddress shipTo,
        IReadOnlyList<OrderReadyToShipLine> lines, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, eventId)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        ShipTo = shipTo;
        Lines = lines;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

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
