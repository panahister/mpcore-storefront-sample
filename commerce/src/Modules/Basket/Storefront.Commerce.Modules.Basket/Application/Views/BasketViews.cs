namespace Storefront.Commerce.Modules.Basket.Application.Views;

public static class BasketViews
{
    public static BasketView Of(Domain.Basket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        return new BasketView(
            [.. basket.Lines.Select(static l => new BasketLineView(l.Sku, l.ProductName, l.UnitPrice, l.PreviousUnitPrice, l.Quantity, l.LineTotal))],
            basket.Total, basket.Currency, basket.HasUnseenPriceChanges);
    }

    public static BasketView Empty() => new([], 0m, "USD", false);
}
