using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Basket.Application.Views;
using Storefront.Commerce.Modules.Catalog.Contracts;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Persistence.Abstractions;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Basket.Application.Commands;

/// <summary>Puts a product in the shopper's basket at the Catalog's current price, or removes it with a quantity of zero.</summary>
public sealed record SetBasketItem(string Sku, int Quantity) : ICommand<Result<BasketView>>;

public static class SetBasketItemHandler
{
    public static async Task<Result<BasketView>> Handle(
        SetBasketItem command,
        ICurrentActorAccessor actor,
        IBasketRepository baskets,
        ICatalogLookup catalog,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);

        // Identity comes from the validated token, never from the request.
        var buyerId = actor.Current.SubjectId;
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return Result<BasketView>.FromFailure(BasketFailures.BuyerRequired());
        }

        var sku = command.Sku.Trim().ToUpperInvariant();
        var product = await catalog.FindAsync(sku, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result<BasketView>.FromFailure(BasketFailures.ProductNotFound(sku));
        }

        var basket = await baskets.GetAsync(buyerId, cancellationToken).ConfigureAwait(false);
        var isNew = basket is null;
        basket ??= Domain.Basket.Open(buyerId, product.Currency);

        // The aggregate checks rules B2 and B5 and throws the broken one; the edge reports it as 422 under
        // its own code. Nothing is tracked yet for a new basket, so nothing needs rolling back.
        basket.SetQuantity(product.Sku, product.Name, product.Price, product.PriceVersion, product.IsSellable, command.Quantity);

        if (isNew)
        {
            baskets.Add(basket);
        }

        return Result<BasketView>.Success(BasketViews.Of(basket));
    }
}
