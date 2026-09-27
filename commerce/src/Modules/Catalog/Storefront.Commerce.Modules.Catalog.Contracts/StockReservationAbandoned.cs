namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>
/// The request to hold an order's stock was given up: it failed, was retried, and went to the error queue.
/// Nothing is held. Not an answer of the warehouse about stock, so it is not a rejection; the order process
/// needs to hear it all the same, or the order waits for ever.
/// </summary>
public sealed record StockReservationAbandoned(Guid OrderId);
