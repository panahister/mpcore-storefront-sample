namespace Storefront.Commerce.Modules.Ordering.Domain.Events;

public sealed record OrderPlacedLine(string Sku, int Quantity, decimal UnitPrice);
