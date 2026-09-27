namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>The order will not ship: return its held units to the shelf. Idempotent.</summary>
public sealed record ReleaseStock(Guid OrderId);
