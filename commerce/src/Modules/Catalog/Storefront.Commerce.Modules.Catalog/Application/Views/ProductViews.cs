using Storefront.Commerce.Modules.Catalog.Domain;

namespace Storefront.Commerce.Modules.Catalog.Application.Views;

public static class ProductViews
{
    public static ProductStockView Stock(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new ProductStockView(
            product.Sku.Value, product.Name, product.Price.Amount, product.PriceVersion, product.Status.ToString(),
            product.OnHand, product.Reserved, product.Available, product.ReorderThreshold);
    }
}
