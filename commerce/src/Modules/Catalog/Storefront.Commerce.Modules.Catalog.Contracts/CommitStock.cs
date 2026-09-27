namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>The order shipped: its held units left the warehouse. Idempotent.</summary>
public sealed record CommitStock(Guid OrderId);
