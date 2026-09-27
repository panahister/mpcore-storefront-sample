namespace Storefront.Commerce.Modules.Ordering.Domain.Events;

/// <summary>What every order event carries, so the host can partition Kafka by order and consumers can order by version.</summary>
public interface IOrderEvent
{
    Guid OrderId { get; }

    string OrderNumber { get; }

    int AggregateVersion { get; }
}
