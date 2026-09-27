using System.Globalization;

namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C14: a delivery note is received once; reported again, it must name the same quantity.</summary>
public sealed class DeliveryMustMatchItsReceipt(StockReceipt receipt, int quantity) : CatalogRule(
    "DELIVERY_REFERENCE_REUSED", "catalog.delivery_reference_reused",
    new Dictionary<string, string>
    {
        ["sku"] = receipt.Sku,
        ["reference"] = receipt.Reference,
        ["received"] = receipt.Quantity.ToString(CultureInfo.InvariantCulture),
        ["requested"] = quantity.ToString(CultureInfo.InvariantCulture)
    })
{
    public override bool IsBroken() => quantity != receipt.Quantity;
}
