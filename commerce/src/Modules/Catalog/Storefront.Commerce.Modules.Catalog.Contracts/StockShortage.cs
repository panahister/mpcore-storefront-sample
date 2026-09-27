namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>Why a line could not be reserved: what was asked, what was there.</summary>
public sealed record StockShortage(string Sku, int Requested, int Available);
