using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Basket.Application.Views;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Basket.Application.Queries;

/// <summary>
/// Shows the shopper their basket. A query: it reads and changes nothing, so the storefront's <c>GET</c>
/// is safe to repeat, retry or prefetch (RFC 9110). A changed price stays marked until the shopper says
/// they have seen it, which is a command of its own: <c>AcknowledgeBasketPrices</c>.
/// </summary>
public sealed record GetMyBasket : IQuery<Result<BasketView>>;

public static class GetMyBasketHandler
{
    public static async Task<Result<BasketView>> Handle(
        GetMyBasket query,
        ICurrentActorAccessor actor,
        IBasketReadModel baskets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(baskets);

        // Identity comes from the validated token, never from the request.
        var buyerId = actor.Current.SubjectId;
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return Result<BasketView>.FromFailure(BasketFailures.BuyerRequired());
        }

        var basket = await baskets.FindForBuyerAsync(buyerId, cancellationToken).ConfigureAwait(false);
        return Result<BasketView>.Success(basket ?? BasketViews.Empty());
    }
}
