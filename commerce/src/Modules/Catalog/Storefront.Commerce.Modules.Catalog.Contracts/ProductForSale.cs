namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>What the storefront needs to know about a product to sell it: never the aggregate itself.</summary>
public sealed record ProductForSale(string Sku, string Name, decimal Price, string Currency, int PriceVersion, bool IsSellable);
