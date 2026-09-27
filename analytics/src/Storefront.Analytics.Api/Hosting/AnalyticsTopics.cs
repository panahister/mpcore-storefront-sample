namespace Storefront.Analytics.Api.Hosting;

/// <summary>The Kafka topics this host reads, and its consumer group.</summary>
/// <remarks>
/// One topic per contract, named after the contract and carrying its version. The names are the contract
/// with Storefront Commerce, which declares the same in its own code. This host publishes nothing: it
/// reads the stream, at its own pace, and may read it again from the start.
/// </remarks>
public static class AnalyticsTopics
{
    public const string OrderPlaced = "storefront.ordering.order-placed.v1";

    public const string OrderPaid = "storefront.ordering.order-paid.v1";

    public const string OrderCancelled = "storefront.ordering.order-cancelled.v1";

    /// <summary>The consumer group: every instance of this service shares the work and the position.</summary>
    public const string ConsumerGroup = "storefront-analytics";
}
