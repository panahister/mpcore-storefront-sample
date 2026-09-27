namespace Storefront.Commerce.Modules.Catalog.Application.Views;

public sealed record ProductDetails(
    string Sku, string Name, string Description, string Brand, string Category, decimal Price, string Currency,
    int PriceVersion, int Available, string Status);
