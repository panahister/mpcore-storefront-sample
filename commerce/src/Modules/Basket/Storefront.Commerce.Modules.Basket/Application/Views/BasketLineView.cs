namespace Storefront.Commerce.Modules.Basket.Application.Views;

public sealed record BasketLineView(string Sku, string ProductName, decimal UnitPrice, decimal? PreviousUnitPrice, int Quantity, decimal LineTotal);
