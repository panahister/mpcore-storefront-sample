using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using MPCore.Application.Querying;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

/// <summary>
/// The read side, and the published <see cref="ICatalogLookup"/>: the same context, so a Basket asking for a
/// price inside its own handler reads the committed catalog. Value objects are read back through their
/// converters and unwrapped in the final projection, which Entity Framework evaluates on the client.
/// </summary>
public sealed class CatalogReadModel<TContext>(TContext database) : ICatalogReadModel, ICatalogLookup where TContext : DbContext
{
    private IQueryable<Product> Products => database.Set<Product>().AsNoTracking();

    public async Task<Page<ProductSummary>> BrowseAsync(
        ProductFilter filter, PageRequest page, SortSpec sort, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(sort);

        var query = Products;
        if (!filter.IncludeDiscontinued)
        {
            query = query.Where(p => p.Status == ProductStatus.Active);
        }

        if (filter.Category is not null)
        {
            query = query.Where(p => p.Category == filter.Category);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Brand, pattern));
        }

        var descending = sort.Direction == SortDirection.Descending;
        // The allowlist already turned the caller's field into one of these three canonical names.
        query = sort.Field switch
        {
            "price" => descending ? query.OrderByDescending(p => p.Price).ThenBy(p => p.Sku) : query.OrderBy(p => p.Price).ThenBy(p => p.Sku),
            "newest" => descending ? query.OrderBy(p => p.CreatedOnUtc).ThenBy(p => p.Sku) : query.OrderByDescending(p => p.CreatedOnUtc).ThenBy(p => p.Sku),
            _ => descending ? query.OrderByDescending(p => p.Name).ThenBy(p => p.Sku) : query.OrderBy(p => p.Name).ThenBy(p => p.Sku)
        };

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip(page.Skip).Take(page.Size)
            .Select(p => new ProductSummary(
                p.Sku.Value, p.Name, p.Brand, p.Category, p.Price.Amount, Price.Currency,
                p.OnHand - p.Reserved > 0, p.Status.ToString()))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new Page<ProductSummary>(items, page.Number, page.Size, total);
    }

    public Task<ProductDetails?> FindDetailsAsync(Sku sku, CancellationToken cancellationToken) =>
        Products
            .Where(p => p.Sku == sku)
            .Select(p => new ProductDetails(
                p.Sku.Value, p.Name, p.Description, p.Brand, p.Category, p.Price.Amount, Price.Currency, p.PriceVersion,
                p.OnHand - p.Reserved, p.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ProductStockView?> FindStockAsync(Sku sku, CancellationToken cancellationToken) =>
        Products
            .Where(p => p.Sku == sku)
            .Select(p => new ProductStockView(
                p.Sku.Value, p.Name, p.Price.Amount, p.PriceVersion, p.Status.ToString(),
                p.OnHand, p.Reserved, p.OnHand - p.Reserved, p.ReorderThreshold))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Page<RestockAlertView>> ListRestockAlertsAsync(PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var alerts = database.Set<RestockAlert>().AsNoTracking();
        var total = await alerts.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await alerts
            .OrderByDescending(a => a.RaisedOnUtc)
            .Skip(page.Skip).Take(page.Size)
            .Select(a => new RestockAlertView(a.Sku, a.ProductName, a.AvailableAtAlert, a.Threshold, a.RaisedOnUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new Page<RestockAlertView>(items, page.Number, page.Size, total);
    }

    public async Task<ProductForSale?> FindAsync(string sku, CancellationToken cancellationToken)
    {
        if (!Sku.TryParse(sku, out var parsed))
        {
            return null;
        }

        return await Products
            .Where(p => p.Sku == parsed)
            .Select(p => new ProductForSale(p.Sku.Value, p.Name, p.Price.Amount, Price.Currency, p.PriceVersion, p.Status == ProductStatus.Active))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
