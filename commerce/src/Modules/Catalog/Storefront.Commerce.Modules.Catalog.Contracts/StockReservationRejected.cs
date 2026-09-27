namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>The Catalog's answer: at least one line could not be held, so nothing was.</summary>
public sealed record StockReservationRejected(Guid OrderId, IReadOnlyList<StockShortage> Shortages);
