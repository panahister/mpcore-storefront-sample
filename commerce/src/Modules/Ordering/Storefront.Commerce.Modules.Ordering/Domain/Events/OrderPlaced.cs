using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Ordering.Domain.Events;

/// <summary>An order was placed. Province and city travel; the street address does not.</summary>
public sealed record OrderPlaced : IntegrationEvent, IOrderEvent
{
    public const string Name = "storefront.ordering.order-placed";

    public OrderPlaced(
        Guid orderId, string orderNumber, int aggregateVersion, string buyerId, string currency, decimal total,
        IReadOnlyList<OrderPlacedLine> lines, string province, string city, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, Guid.CreateVersion7())
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        AggregateVersion = aggregateVersion;
        BuyerId = buyerId;
        Currency = currency;
        Total = total;
        Lines = lines;
        Province = province;
        City = city;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public int AggregateVersion { get; init; }

    public string BuyerId { get; init; }

    public string Currency { get; init; }

    public decimal Total { get; init; }

    public IReadOnlyList<OrderPlacedLine> Lines { get; init; }

    public string Province { get; init; }

    public string City { get; init; }
}
