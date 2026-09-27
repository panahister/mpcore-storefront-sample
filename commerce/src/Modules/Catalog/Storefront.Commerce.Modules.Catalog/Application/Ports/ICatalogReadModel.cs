using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Application.Querying;

namespace Storefront.Commerce.Modules.Catalog.Application.Ports;

/// <summary>The read side: typed pages and views, never a queryable. Paging and sorting are decided here, in the application.</summary>
public interface ICatalogReadModel
{
    Task<Page<ProductSummary>> BrowseAsync(ProductFilter filter, PageRequest page, SortSpec sort, CancellationToken cancellationToken);

    Task<ProductDetails?> FindDetailsAsync(Sku sku, CancellationToken cancellationToken);

    Task<ProductStockView?> FindStockAsync(Sku sku, CancellationToken cancellationToken);

    Task<Page<RestockAlertView>> ListRestockAlertsAsync(PageRequest page, CancellationToken cancellationToken);
}
