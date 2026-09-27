namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>One SKU and quantity of a reservation. A value object: it has no identity of its own.</summary>
public sealed record ReservationLine(string Sku, int Quantity);
