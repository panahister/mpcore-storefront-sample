namespace Storefront.Commerce.Api.Hosting;

/// <summary>
/// The Kafka topics this host publishes to and listens on, and its consumer group.
/// </summary>
/// <remarks>
/// <para>
/// One topic per contract, named after the contract and carrying its version. Automatic topic creation
/// is off on the local broker, so a typo fails loudly instead of creating a topic nobody reads;
/// in Development the host provisions its own topics (<c>Messaging:AutoProvision</c>).
/// </para>
/// <para>
/// Only <c>ProductPriceChanged</c> is also consumed here, by the Basket module. The order topics are
/// this host's contract with the rest of Storefront: the Analytics service reads them. Work for the
/// warehouse does not travel here but on a queue; see <see cref="CommerceQueues"/>.
/// </para>
/// </remarks>
public static class CommerceTopics
{
    /// <summary><c>storefront.catalog.product-price-changed</c> v1. Key: SKU.</summary>
    public const string ProductPriceChanged = "storefront.catalog.product-price-changed.v1";

    /// <summary><c>storefront.ordering.order-placed</c> v1. Key: order id.</summary>
    public const string OrderPlaced = "storefront.ordering.order-placed.v1";

    /// <summary><c>storefront.ordering.order-paid</c> v1. Key: order id.</summary>
    public const string OrderPaid = "storefront.ordering.order-paid.v1";

    /// <summary><c>storefront.ordering.order-cancelled</c> v1. Key: order id.</summary>
    public const string OrderCancelled = "storefront.ordering.order-cancelled.v1";

    /// <summary><c>storefront.ordering.order-shipped</c> v1. Key: order id.</summary>
    public const string OrderShipped = "storefront.ordering.order-shipped.v1";

    /// <summary>The consumer group of the Basket's price listener.</summary>
    public const string BasketPricingGroup = "storefront-commerce.basket-pricing";
}
