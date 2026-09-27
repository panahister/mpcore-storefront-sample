using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using MPCore.Application.Querying;

namespace Storefront.Commerce.Modules.Ordering.Infrastructure;

public sealed class OrderReadModel<TContext>(TContext database) : IOrderReadModel where TContext : DbContext
{
    public async Task<OrderOfBuyer?> FindAsync(OrderId id, CancellationToken cancellationToken)
    {
        // Read without tracking and mapped to a view before it leaves: a query never holds an aggregate.
        var order = await database.Set<Order>().AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken).ConfigureAwait(false);
        return order is null ? null : new OrderOfBuyer(order.BuyerId, OrderViews.Of(order));
    }

    public Task<Page<OrderSummary>> ListForBuyerAsync(string buyerId, PageRequest page, SortSpec sort, CancellationToken cancellationToken) =>
        ListAsync(database.Set<Order>().AsNoTracking().Where(o => o.BuyerId == buyerId), page, sort, cancellationToken);

    public Task<Page<OrderSummary>> ListAsync(OrderStatus? status, PageRequest page, SortSpec sort, CancellationToken cancellationToken)
    {
        var query = database.Set<Order>().AsNoTracking();
        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        return ListAsync(query, page, sort, cancellationToken);
    }

    private static async Task<Page<OrderSummary>> ListAsync(
        IQueryable<Order> query, PageRequest page, SortSpec sort, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(sort);

        var descending = sort.Direction == SortDirection.Descending;
        query = sort.Field == "total"
            ? (descending ? query.OrderByDescending(o => o.Total) : query.OrderBy(o => o.Total)).ThenByDescending(o => o.PlacedOnUtc)
            : descending ? query.OrderByDescending(o => o.PlacedOnUtc) : query.OrderBy(o => o.PlacedOnUtc);

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip(page.Skip).Take(page.Size)
            .Select(o => new OrderSummary(
                o.Id.Value, o.OrderNumber, o.Status.ToString(), o.Total, o.Currency,
                o.Lines.Sum(l => l.Quantity), o.PlacedOnUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new Page<OrderSummary>(items, page.Number, page.Size, total);
    }
}
