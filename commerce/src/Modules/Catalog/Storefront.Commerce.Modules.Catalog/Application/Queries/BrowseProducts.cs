using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using MPCore.Application.Messaging;
using MPCore.Application.Querying;
using MPCore.Application.Results;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Catalog.Application.Queries;

/// <summary>The storefront listing. Anonymous; sortable only by the published fields.</summary>
public sealed record BrowseProducts(
    string? Category, string? Search, int Page = 1, int Size = PageRequest.DefaultSize,
    string? Sort = null, bool Descending = false, bool IncludeDiscontinued = false)
    : IQuery<Result<Page<ProductSummary>>>;

public static class BrowseProductsHandler
{
    public static readonly SortAllowlist Sorts = new("name", "price", "newest");

    public static async Task<Result<Page<ProductSummary>>> Handle(
        BrowseProducts query,
        ICatalogReadModel catalog,
        ICurrentActorAccessor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(actor);

        var requested = query.Sort is null
            ? null
            : new SortSpec(query.Sort, query.Descending ? SortDirection.Descending : SortDirection.Ascending);
        var sort = Sorts.Resolve(requested, new SortSpec("name"));
        if (sort.IsFailure)
        {
            return Result<Page<ProductSummary>>.FromFailure(sort.FailureDescriptor!);
        }

        // A shopper asking for discontinued products gets the storefront anyway. The flag is a back-
        // office convenience, and the decision about who is back office belongs here, not to the caller.
        var includeDiscontinued = query.IncludeDiscontinued && actor.Current.HasRole(CatalogRoles.Manager);
        var page = await catalog.BrowseAsync(
            new ProductFilter(query.Category, query.Search?.Trim(), includeDiscontinued),
            new PageRequest(query.Page, query.Size), sort.Value, cancellationToken).ConfigureAwait(false);
        return Result<Page<ProductSummary>>.Success(page);
    }
}
