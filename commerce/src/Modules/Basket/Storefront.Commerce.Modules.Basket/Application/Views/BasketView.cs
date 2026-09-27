namespace Storefront.Commerce.Modules.Basket.Application.Views;

/// <summary>What the storefront shows: the lines, the total, and whether a price moved since the shopper last looked.</summary>
public sealed record BasketView(IReadOnlyList<BasketLineView> Lines, decimal Total, string Currency, bool PricesChanged);
