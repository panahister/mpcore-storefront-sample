namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>The Basket asks the Catalog for a price through this, inside its own transaction.</summary>
public interface ICatalogLookup
{
    Task<ProductForSale?> FindAsync(string sku, CancellationToken cancellationToken);
}
