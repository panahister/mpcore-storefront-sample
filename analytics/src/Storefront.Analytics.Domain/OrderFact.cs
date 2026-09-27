using MPCore.Domain.Model;

namespace Storefront.Analytics.Domain;

/// <summary>What happened to an order, as far as the figures are concerned.</summary>
public enum OrderFactKind
{
    Placed = 1,
    Paid = 2,
    Cancelled = 3
}

/// <summary>
/// One thing that happened to one order, at one moment: the row of a time series.
/// </summary>
/// <remarks>
/// <para>
/// A fact is written once and never changed. Its identity is the identity of the event that reported it,
/// so an event delivered twice is one fact (Hohpe and Woolf's <i>Idempotent Receiver</i>).
/// </para>
/// <para>
/// This context holds no order. It does not know what an order may do next and decides nothing; it counts.
/// Eric Evans's bounded context is what allows that: the same word, "order", means a process in the shop
/// and a row of figures here, and neither model has to serve the other.
/// </para>
/// </remarks>
public sealed class OrderFact : AggregateRoot<Guid>
{
    private OrderFact()
    {
        OrderNumber = string.Empty;
        Currency = string.Empty;
    }

    private OrderFact(
        Guid eventId, DateTimeOffset occurredOnUtc, OrderFactKind kind, Guid orderId, string orderNumber,
        decimal amount, string currency, int itemCount, string? region, string? city, string? reason)
        : base(eventId)
    {
        OccurredOnUtc = occurredOnUtc;
        Kind = kind;
        OrderId = orderId;
        OrderNumber = orderNumber;
        Amount = amount;
        Currency = currency;
        ItemCount = itemCount;
        Region = region;
        City = city;
        Reason = reason;
    }

    /// <summary>When it happened, in the shop. The time axis of the series.</summary>
    public DateTimeOffset OccurredOnUtc { get; private set; }

    public OrderFactKind Kind { get; private set; }

    public Guid OrderId { get; private set; }

    public string OrderNumber { get; private set; }

    /// <summary>The order's total for a placed or paid order; zero for a cancellation.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public int ItemCount { get; private set; }

    /// <summary>Where the order goes. Known when it is placed; the other events do not carry it.</summary>
    public string? Region { get; private set; }

    public string? City { get; private set; }

    /// <summary>Why it was cancelled.</summary>
    public string? Reason { get; private set; }

    public static OrderFact Placed(
        Guid eventId, DateTimeOffset occurredOnUtc, Guid orderId, string orderNumber, decimal total, string currency,
        int itemCount, string region, string city) =>
        new(eventId, occurredOnUtc, OrderFactKind.Placed, orderId, orderNumber, total, currency, itemCount, region, city, null);

    public static OrderFact Paid(Guid eventId, DateTimeOffset occurredOnUtc, Guid orderId, string orderNumber, decimal total, string currency) =>
        new(eventId, occurredOnUtc, OrderFactKind.Paid, orderId, orderNumber, total, currency, 0, null, null, null);

    public static OrderFact Cancelled(Guid eventId, DateTimeOffset occurredOnUtc, Guid orderId, string orderNumber, string reason) =>
        new(eventId, occurredOnUtc, OrderFactKind.Cancelled, orderId, orderNumber, 0m, string.Empty, 0, null, null, reason);
}
