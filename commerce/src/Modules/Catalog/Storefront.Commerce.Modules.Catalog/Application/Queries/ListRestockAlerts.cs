using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using MPCore.Application.Messaging;
using MPCore.Application.Querying;
using MPCore.Application.Results;

namespace Storefront.Commerce.Modules.Catalog.Application.Queries;

/// <summary>What purchasing has been told to reorder, newest first.</summary>
public sealed record ListRestockAlerts(int Page = 1, int Size = PageRequest.DefaultSize) : IQuery<Result<Page<RestockAlertView>>>;

public static class ListRestockAlertsHandler
{
    public static async Task<Result<Page<RestockAlertView>>> Handle(
        ListRestockAlerts query, ICatalogReadModel catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await catalog.ListRestockAlertsAsync(new PageRequest(query.Page, query.Size), cancellationToken)
            .ConfigureAwait(false);
        return Result<Page<RestockAlertView>>.Success(page);
    }
}
