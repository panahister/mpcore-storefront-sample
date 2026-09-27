using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Basket.Application.Views;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Persistence.Abstractions;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Basket.Application.Commands;

/// <summary>
/// The shopper has seen the changed prices: stop marking them (rule B4). A command, because it changes the
/// basket. Idempotent: acknowledging twice, or with nothing to acknowledge, leaves the basket as it is.
/// </summary>
/// <remarks>
/// Reading and acknowledging are two messages on purpose. Bertrand Meyer's command-query separation says
/// an operation either changes state or returns data; one that does both behind a <c>GET</c> would let a
/// retry or a browser prefetch acknowledge a price the shopper never saw.
/// </remarks>
public sealed record AcknowledgeBasketPrices : ICommand<Result<BasketView>>;

public static class AcknowledgeBasketPricesHandler
{
    public static async Task<Result<BasketView>> Handle(
        AcknowledgeBasketPrices command,
        ICurrentActorAccessor actor,
        IBasketRepository baskets,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var buyerId = actor.Current.SubjectId;
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return Result<BasketView>.FromFailure(BasketFailures.BuyerRequired());
        }

        var basket = await baskets.GetAsync(buyerId, cancellationToken).ConfigureAwait(false);
        if (basket is null)
        {
            return Result<BasketView>.Success(BasketViews.Empty());
        }

        basket.AcknowledgePrices();
        return Result<BasketView>.Success(BasketViews.Of(basket));
    }
}
