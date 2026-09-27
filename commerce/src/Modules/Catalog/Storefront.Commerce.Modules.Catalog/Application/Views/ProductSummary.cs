namespace Storefront.Commerce.Modules.Catalog.Application.Views;

/// <summary>A product as the storefront lists it. A view: what a query returns, never the aggregate.</summary>
public sealed record ProductSummary(
    string Sku, string Name, string Brand, string Category, decimal Price, string Currency, bool InStock, string Status);
