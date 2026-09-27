using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Basket.Domain;

/// <summary>
/// One product in a basket. A child entity of the <see cref="Basket"/> aggregate: it has an identity, the
/// SKU, because its quantity and price change over time while it stays the same line; it is reached only
/// through its basket, which is the consistency boundary (Evans; Vernon, <i>Implementing DDD</i>).
/// </summary>
public sealed class BasketLine : Entity<string>
{
    private BasketLine()
    {
        ProductName = string.Empty;
    }

    internal BasketLine(string sku, string productName, decimal unitPrice, int priceVersion, int quantity)
        : base(sku)
    {
        ProductName = productName;
        UnitPrice = unitPrice;
        PriceVersion = priceVersion;
        Quantity = quantity;
    }

    public string Sku => Id;

    public string ProductName { get; private set; }

    /// <summary>The price the shopper currently sees, as the Catalog last told it.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>The Catalog's price version behind <see cref="UnitPrice"/>; older or repeated changes are ignored.</summary>
    public int PriceVersion { get; private set; }

    /// <summary>The price before an unseen change, until the shopper looks at the basket (rule B4).</summary>
    public decimal? PreviousUnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    internal void SetQuantity(int quantity) => Quantity = quantity;

    internal void Reprice(decimal unitPrice, int priceVersion, bool keepPrevious)
    {
        if (keepPrevious && unitPrice != UnitPrice)
        {
            PreviousUnitPrice ??= UnitPrice;
        }

        UnitPrice = unitPrice;
        PriceVersion = priceVersion;
        if (PreviousUnitPrice == UnitPrice)
        {
            PreviousUnitPrice = null;
        }
    }

    internal void Acknowledge() => PreviousUnitPrice = null;
}
