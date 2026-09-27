namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>The Catalog's answer: every line of the order is held.</summary>
public sealed record StockReserved(Guid OrderId);
