namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>One line of a stock request.</summary>
public sealed record StockLine(string Sku, int Quantity);
