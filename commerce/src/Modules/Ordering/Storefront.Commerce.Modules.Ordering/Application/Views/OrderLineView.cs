namespace Storefront.Commerce.Modules.Ordering.Application.Views;

public sealed record OrderLineView(string Sku, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);
