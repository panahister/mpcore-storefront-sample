using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Basket.Application.Views;
using Microsoft.EntityFrameworkCore;
using BasketAggregate = Storefront.Commerce.Modules.Basket.Domain.Basket;

namespace Storefront.Commerce.Modules.Basket.Infrastructure;

/// <summary>The adapter behind <see cref="IBasketReadModel"/>: reads without tracking and hands back a view.</summary>
public sealed class BasketReadModel<TContext>(TContext database) : IBasketReadModel where TContext : DbContext
{
    public async Task<BasketView?> FindForBuyerAsync(string buyerId, CancellationToken cancellationToken)
    {
        var basket = await database.Set<BasketAggregate>().AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == buyerId, cancellationToken).ConfigureAwait(false);
        return basket is null ? null : BasketViews.Of(basket);
    }
}
