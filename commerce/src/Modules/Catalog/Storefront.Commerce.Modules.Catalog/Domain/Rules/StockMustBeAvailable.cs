using System.Globalization;

namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C6: a reservation never exceeds what is available (on hand minus already reserved).</summary>
public sealed class StockMustBeAvailable(Product product, int quantity) : CatalogRule(
    "INSUFFICIENT_STOCK", "catalog.insufficient_stock",
    new Dictionary<string, string>
    {
        ["sku"] = product.Sku.Value,
        ["requested"] = quantity.ToString(CultureInfo.InvariantCulture),
        ["available"] = Math.Max(product.Available, 0).ToString(CultureInfo.InvariantCulture)
    })
{
    public override bool IsBroken() => product.Available < quantity;
}
