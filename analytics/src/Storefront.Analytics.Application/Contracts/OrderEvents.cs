using MPCore.Domain.Events;

namespace Storefront.Analytics.Application.Contracts;

// The shop's order events, as this service reads them. Published by Storefront Commerce on Kafka, one topic
// per contract. The two services share no assembly: each declares the contract in its own code, and the
// two agree on the name, the version and the JSON. These are the consumer's copies, and each declares only
// what the figures use.

/// <summary><c>storefront.ordering.order-placed</c>, version 1.</summary>
public sealed record OrderPlaced : IntegrationEvent
{
    public const string Name = "storefront.ordering.order-placed";

    public OrderPlaced(
        Guid eventId, Guid orderId, string orderNumber, string currency, decimal total,
        IReadOnlyList<OrderPlacedLine> lines, string province, string city, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, eventId)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        Currency = currency;
        Total = total;
        Lines = lines;
        Province = province;
        City = city;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public string Currency { get; init; }

    public decimal Total { get; init; }

    public IReadOnlyList<OrderPlacedLine> Lines { get; init; }

    public string Province { get; init; }

    public string City { get; init; }
}

public sealed record OrderPlacedLine(string Sku, int Quantity, decimal UnitPrice);

/// <summary><c>storefront.ordering.order-paid</c>, version 1.</summary>
public sealed record OrderPaid : IntegrationEvent
{
    public const string Name = "storefront.ordering.order-paid";

    public OrderPaid(Guid eventId, Guid orderId, string orderNumber, decimal total, string currency, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, eventId)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        Total = total;
        Currency = currency;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public decimal Total { get; init; }

    public string Currency { get; init; }
}

/// <summary><c>storefront.ordering.order-cancelled</c>, version 1.</summary>
public sealed record OrderCancelled : IntegrationEvent
{
    public const string Name = "storefront.ordering.order-cancelled";

    public OrderCancelled(Guid eventId, Guid orderId, string orderNumber, string reason, DateTimeOffset occurredOnUtc)
        : base(Name, 1, occurredOnUtc, eventId)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        Reason = reason;
    }

    public Guid OrderId { get; init; }

    public string OrderNumber { get; init; }

    public string Reason { get; init; }
}
