using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Querying;
using MPCore.Application.Results;

namespace Storefront.Commerce.Modules.Ordering.Application.Queries;

/// <summary>Every order, for support (REST) and the warehouse (gRPC, paid orders only).</summary>
public sealed record ListOrders(string? Status, int Page = 1, int Size = PageRequest.DefaultSize, string? Sort = null, bool Descending = true)
    : IQuery<Result<Page<OrderSummary>>>;

public static class ListOrdersHandler
{
    public static async Task<Result<Page<OrderSummary>>> Handle(
        ListOrders query, IOrderReadModel read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(read);

        OrderStatus? status = null;
        if (query.Status is not null)
        {
            if (!Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Result<Page<OrderSummary>>.FromFailure(OrderingFailures.StatusUnknown());
            }

            status = parsed;
        }

        var sort = ListMyOrdersHandler.Sorts.Resolve(
            query.Sort is null ? null : new SortSpec(query.Sort, query.Descending ? SortDirection.Descending : SortDirection.Ascending),
            new SortSpec("placed", SortDirection.Descending));
        if (sort.IsFailure)
        {
            return Result<Page<OrderSummary>>.FromFailure(sort.FailureDescriptor!);
        }

        return Result<Page<OrderSummary>>.Success(await read.ListAsync(
            status, new PageRequest(query.Page, query.Size), sort.Value, cancellationToken).ConfigureAwait(false));
    }
}
