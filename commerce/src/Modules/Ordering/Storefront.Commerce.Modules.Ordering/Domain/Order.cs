using Storefront.Commerce.Modules.Ordering.Domain.Events;
using Storefront.Commerce.Modules.Ordering.Domain.Rules;
using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>
/// An order: what was bought, where it goes, and how far along it is. The aggregate owns the status machine
/// (rule O5) and checks every transition before it happens, so an order is never in a state the business
/// would refuse.
/// </summary>
/// <remarks>
/// <para>
/// The status is also the state of the order process: which module answered, and what is still owed. The
/// process manager in the Application layer decides the next step; the rules that say whether a step is
/// allowed live here.
/// </para>
/// <para>
/// Four integration events leave the host on Kafka: placed, paid, cancelled, shipped. The street address
/// is not in them. A fifth, <see cref="OrderReadyToShip"/>, goes to the warehouse's queue and carries it.
/// </para>
/// </remarks>
public sealed class Order : AggregateRoot<OrderId>
{
    private const string RefundedEntry = "Refunded";

    private readonly List<OrderLine> lines = [];
    private readonly List<OrderHistoryEntry> history = [];

    private Order()
    {
        OrderNumber = string.Empty;
        BuyerId = string.Empty;
        BuyerName = string.Empty;
        Currency = string.Empty;
        ShippingAddress = null!;
    }

    private Order(
        OrderId id, string buyerId, string buyerName, ShippingAddress address, string currency,
        IEnumerable<OrderLine> orderLines, DateTimeOffset now)
        : base(id)
    {
        OrderNumber = NumberFor(id, now);
        BuyerId = buyerId;
        BuyerName = buyerName;
        ShippingAddress = address;
        Currency = currency;
        lines.AddRange(orderLines);
        Total = lines.Sum(static l => l.LineTotal);
        Status = OrderStatus.Submitted;
        PlacedOnUtc = now;
        Version = 1;
        history.Add(new OrderHistoryEntry(nameof(OrderStatus.Submitted), now, null));
    }

    /// <summary>The number people quote: <c>ORD-yyMMdd-XXXXXX</c>.</summary>
    public string OrderNumber { get; private set; }

    public string BuyerId { get; private set; }

    public string BuyerName { get; private set; }

    public ShippingAddress ShippingAddress { get; private set; }

    public string Currency { get; private set; }

    public IReadOnlyList<OrderLine> Lines => lines;

    public decimal Total { get; private set; }

    public OrderStatus Status { get; private set; }

    /// <summary>Incremented on every transition; carried by the integration events so consumers can order them.</summary>
    public int Version { get; private set; }

    public DateTimeOffset PlacedOnUtc { get; private set; }

    public string? PaymentReference { get; private set; }

    public string? CancellationReason { get; private set; }

    public string? Carrier { get; private set; }

    public string? TrackingCode { get; private set; }

    public IReadOnlyList<OrderHistoryEntry> History => history;

    public bool IsCancelled => Status == OrderStatus.Cancelled;

    /// <summary>Answers without throwing, for the process manager that must ignore a late or repeated answer.</summary>
    public bool CanConfirmStock => Status == OrderStatus.Submitted;

    public bool CanMarkPaid => Status == OrderStatus.AwaitingPayment;

    public bool CanCancel => Status is not OrderStatus.Shipped and not OrderStatus.Cancelled;

    public static Order Place(
        OrderId id, string buyerId, string buyerName, ShippingAddress address, string currency,
        IReadOnlyList<OrderLine> orderLines, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buyerId);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(orderLines);
        CheckRule(new OrderMustHaveLines(orderLines.Count));

        var order = new Order(id, buyerId, buyerName, address, currency, orderLines, now);
        order.Raise(new OrderPlaced(
            id.Value, order.OrderNumber, order.Version, buyerId, currency, order.Total,
            [.. orderLines.Select(static l => new OrderPlacedLine(l.Sku, l.Quantity, l.UnitPrice))],
            address.Province, address.City, now));
        return order;
    }

    public void ConfirmStock(DateTimeOffset now) =>
        Move(OrderStatus.Submitted, OrderStatus.AwaitingPayment, now, null);

    public void MarkPaid(string paymentReference, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        Move(OrderStatus.AwaitingPayment, OrderStatus.Paid, now, null);
        PaymentReference = paymentReference;
        Raise(new OrderPaid(Id.Value, OrderNumber, Version, Total, Currency, paymentReference, now));

        // The warehouse is told in a message of its own, with the snapshot it needs; see OrderReadyToShip.
        Raise(new OrderReadyToShip(
            Id.Value, OrderNumber, Version,
            new OrderReadyToShipAddress(
                ShippingAddress.RecipientName, ShippingAddress.Phone.Value, ShippingAddress.Province,
                ShippingAddress.City, ShippingAddress.Line, ShippingAddress.PostalCode.Value),
            [.. lines.Select(static l => new OrderReadyToShipLine(l.Sku, l.ProductName, l.Quantity))],
            now));
    }

    /// <summary>Stops the order. Returns whether money has to go back.</summary>
    public bool Cancel(string reasonCode, string? note, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        CheckRule(new OrderMustNotBeShipped(this));
        CheckRule(new OrderMustNotBeCancelled(this));

        var wasPaid = Status == OrderStatus.Paid;
        Status = OrderStatus.Cancelled;
        CancellationReason = reasonCode;
        Version++;
        history.Add(new OrderHistoryEntry(nameof(OrderStatus.Cancelled), now, note is null ? reasonCode : $"{reasonCode}: {note}"));
        Raise(new OrderCancelled(Id.Value, OrderNumber, Version, reasonCode, wasPaid, now));
        return wasPaid;
    }

    public void Ship(string carrier, string trackingCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrier);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingCode);
        Move(OrderStatus.Paid, OrderStatus.Shipped, now, $"{carrier} {trackingCode}");
        Carrier = carrier;
        TrackingCode = trackingCode;
        Raise(new OrderShipped(Id.Value, OrderNumber, Version, carrier, trackingCode, now));
    }

    /// <summary>Records that the money of a cancelled order was paid back. Idempotent per refund reference.</summary>
    public bool RecordRefund(string refundReference, DateTimeOffset now)
    {
        if (history.Any(h => h.Status == RefundedEntry && h.Note == refundReference))
        {
            return false;
        }

        history.Add(new OrderHistoryEntry(RefundedEntry, now, refundReference));
        return true;
    }

    private void Move(OrderStatus from, OrderStatus to, DateTimeOffset now, string? note)
    {
        CheckRule(new OrderMustNotBeCancelled(this));
        CheckRule(new OrderMustBeInStatus(from, Status));

        Status = to;
        Version++;
        history.Add(new OrderHistoryEntry(to.ToString(), now, note));
    }

    private static string NumberFor(OrderId id, DateTimeOffset now) =>
        $"ORD-{now:yyMMdd}-{id.Value.ToString("N")[^6..].ToUpperInvariant()}";
}
