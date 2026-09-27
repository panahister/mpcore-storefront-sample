using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using MPCore.Application.Querying;

namespace Storefront.Commerce.Modules.Ordering.Application.Ports;

/// <summary>The read side of Ordering: views and pages, never the aggregate. Every query reads through this port.</summary>
public interface IOrderReadModel
{
    Task<OrderOfBuyer?> FindAsync(OrderId id, CancellationToken cancellationToken);

    Task<Page<OrderSummary>> ListForBuyerAsync(string buyerId, PageRequest page, SortSpec sort, CancellationToken cancellationToken);

    Task<Page<OrderSummary>> ListAsync(OrderStatus? status, PageRequest page, SortSpec sort, CancellationToken cancellationToken);
}
