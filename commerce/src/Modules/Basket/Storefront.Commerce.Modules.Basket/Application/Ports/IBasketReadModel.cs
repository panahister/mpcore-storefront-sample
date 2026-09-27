using Storefront.Commerce.Modules.Basket.Application.Views;

namespace Storefront.Commerce.Modules.Basket.Application.Ports;

/// <summary>
/// The read side of the basket: it returns a view, never the aggregate, and tracks nothing. Queries read
/// through this port; commands change the basket through <see cref="IBasketRepository"/>.
/// </summary>
public interface IBasketReadModel
{
    /// <summary>The shopper's basket as the storefront shows it, or null when they have none.</summary>
    Task<BasketView?> FindForBuyerAsync(string buyerId, CancellationToken cancellationToken);
}
