using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using MPCore.Application.Messaging;
using MPCore.Application.Querying;
using MPCore.Application.Results;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Ordering.Application.Queries;

public sealed record ListMyOrders(int Page = 1, int Size = PageRequest.DefaultSize, string? Sort = null, bool Descending = true)
    : IQuery<Result<Page<OrderSummary>>>;

public static class ListMyOrdersHandler
{
    public static readonly SortAllowlist Sorts = new("placed", "total");

    public static async Task<Result<Page<OrderSummary>>> Handle(
        ListMyOrders query, ICurrentActorAccessor actor, IOrderReadModel read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(read);

        var buyerId = actor.Current.SubjectId;
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return Result<Page<OrderSummary>>.FromFailure(OrderingFailures.BuyerRequired());
        }

        var sort = Sorts.Resolve(
            query.Sort is null ? null : new SortSpec(query.Sort, query.Descending ? SortDirection.Descending : SortDirection.Ascending),
            new SortSpec("placed", SortDirection.Descending));
        if (sort.IsFailure)
        {
            return Result<Page<OrderSummary>>.FromFailure(sort.FailureDescriptor!);
        }

        return Result<Page<OrderSummary>>.Success(await read.ListForBuyerAsync(
            buyerId, new PageRequest(query.Page, query.Size), sort.Value, cancellationToken).ConfigureAwait(false));
    }
}
