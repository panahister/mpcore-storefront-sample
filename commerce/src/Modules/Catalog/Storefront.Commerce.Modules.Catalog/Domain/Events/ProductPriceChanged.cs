using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Catalog.Domain.Events;

/// <summary>
/// A price changed. An integration event: it leaves the host on Kafka, so it carries a name, a contract
/// version and only primitives. The Basket module hears it the way any other system would.
/// </summary>
public sealed record ProductPriceChanged : IntegrationEvent
{
    public const string Name = "storefront.catalog.product-price-changed";

    public const int ContractVersion = 1;

    public ProductPriceChanged(
        Guid productId, string sku, string productName, decimal oldPrice, decimal newPrice,
        string currency, int priceVersion, DateTimeOffset occurredOnUtc)
        : base(Name, ContractVersion, occurredOnUtc, Guid.CreateVersion7())
    {
        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        OldPrice = oldPrice;
        NewPrice = newPrice;
        Currency = currency;
        PriceVersion = priceVersion;
    }

    public Guid ProductId { get; init; }

    public string Sku { get; init; }

    public string ProductName { get; init; }

    public decimal OldPrice { get; init; }

    public decimal NewPrice { get; init; }

    public string Currency { get; init; }

    public int PriceVersion { get; init; }
}
