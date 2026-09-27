namespace Storefront.Commerce.Api.Hosting;

/// <summary>The RabbitMQ queues this host publishes to and listens on.</summary>
/// <remarks>
/// <para>
/// This host uses two brokers, for two different jobs. Kafka carries the event stream
/// (<see cref="CommerceTopics"/>): what happened, kept in order, for any number of readers. RabbitMQ carries
/// work for one reader: an order for the warehouse to ship, and the warehouse's word that it has left.
/// Gregor Hohpe and Bobby Woolf call the two <i>Publish-Subscribe Channel</i> and <i>Point-to-Point
/// Channel</i> (<i>Enterprise Integration Patterns</i>).
/// </para>
/// <para>
/// One queue per contract, named after the service that reads it and the contract. The names are the
/// contract with Storefront Fulfillment, which declares the same two in its own code.
/// </para>
/// </remarks>
public static class CommerceQueues
{
    /// <summary><c>storefront.ordering.order-ready-to-ship</c> v1, to the warehouse.</summary>
    public const string OrdersReadyToShip = "storefront.fulfillment.orders-ready-to-ship.v1";

    /// <summary><c>storefront.fulfillment.shipment-dispatched</c> v1, from the warehouse.</summary>
    public const string ShipmentsDispatched = "storefront.commerce.shipments-dispatched.v1";
}
