namespace Storefront.Commerce.Modules.Basket.Contracts;

/// <summary>One line of a basket that was checked out, at the price the shopper saw.</summary>
public sealed record CheckoutLine(string Sku, string ProductName, decimal UnitPrice, int Quantity);
