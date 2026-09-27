namespace Storefront.Commerce.Modules.Catalog.Application.Views;

/// <summary>What the back office sees after a change: price, status and the warehouse numbers.</summary>
public sealed record ProductStockView(
    string Sku, string Name, decimal Price, int PriceVersion, string Status, int OnHand, int Reserved, int Available, int ReorderThreshold);
