using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Caching.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Queries;

/// <summary>A product page. Anonymous, and cached for two minutes; a change to the product evicts it.</summary>
public sealed record GetProductDetails(string Sku) : IQuery<Result<ProductDetails>>;

public static class GetProductDetailsHandler
{
    public static readonly TimeSpan DetailsLifetime = TimeSpan.FromMinutes(2);

    public static string CacheKey(string sku) => $"catalog:product:{Sku.Normalize(sku)}";

    public static async Task<Result<ProductDetails>> Handle(
        GetProductDetails query,
        ICatalogReadModel catalog,
        IReadThroughCache cache,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(cache);

        // A malformed SKU names nothing: "not found", not "invalid". Only the shape of the request is validated.
        if (!Sku.TryParse(query.Sku, out var sku))
        {
            return Result<ProductDetails>.FromFailure(CatalogFailures.ProductNotFound(Sku.Normalize(query.Sku)));
        }

        var details = await cache.GetOrCreateAsync(
            CacheKey(sku.Value),
            ct => new ValueTask<ProductDetails?>(catalog.FindDetailsAsync(sku, ct)),
            DetailsLifetime,
            cancellationToken).ConfigureAwait(false);

        // A discontinued product's page stays reachable (old links, order history) and says so;
        // it is simply not sellable. Unknown is the only "not found".
        return details is null
            ? Result<ProductDetails>.FromFailure(CatalogFailures.ProductNotFound(sku.Value))
            : Result<ProductDetails>.Success(details);
    }
}
