using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Ordering.Domain.Events;

/// <summary>The card was charged. For the event stream: it holds no personal data.</summary>
public sealed record OrderPaid : IntegrationEvent, IOrderEvent
{
    public const string Name = "storefront.ordering.order-paid";

    public OrderPaid(
        Guid orderId, string orderNumber, int aggregateVersion, decimal total, string currency,
        string paymentReference, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, Guid.CreateVersion7())
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        AggregateVersion = aggregateVersion;
        Total = total;
        Currency = currency;
        PaymentReference = paymentReference;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public int AggregateVersion { get; init; }

    public decimal Total { get; init; }

    public string Currency { get; init; }

    public string PaymentReference { get; init; }
}
