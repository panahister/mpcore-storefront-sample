namespace Storefront.Fulfillment.Api.Hosting;

/// <summary>The RabbitMQ queues this host listens on and publishes to.</summary>
/// <remarks>
/// <para>
/// One queue per contract, named after the service that reads it and the contract. A queue is a work list:
/// each message is taken by one worker, which is what a warehouse wants of an order to ship. Kafka, which
/// the shop uses for its event stream, is a log that any number of readers replay. Gregor Hohpe and Bobby
/// Woolf distinguish the two as <i>Point-to-Point Channel</i> and <i>Publish-Subscribe Channel</i>
/// (<i>Enterprise Integration Patterns</i>).
/// </para>
/// <para>
/// The names are the contract with Storefront Commerce, which declares the same two in its own code.
/// </para>
/// </remarks>
public static class FulfillmentQueues
{
    /// <summary><c>storefront.ordering.order-ready-to-ship</c> v1, from the shop.</summary>
    public const string OrdersReadyToShip = "storefront.fulfillment.orders-ready-to-ship.v1";

    /// <summary><c>storefront.fulfillment.shipment-dispatched</c> v1, to the shop.</summary>
    public const string ShipmentsDispatched = "storefront.commerce.shipments-dispatched.v1";
}
