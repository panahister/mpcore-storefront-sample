using Storefront.Commerce.Modules.Basket.Domain.Rules;
using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Basket.Domain;

/// <summary>
/// A shopper's basket: the products they intend to buy, at the prices they saw. One per shopper, keyed by
/// the shopper's identity, so a shopper never has two.
/// </summary>
/// <remarks>
/// The basket copies the price at the moment a product is added and keeps it current through
/// <see cref="ApplyPriceChange"/>. A price the shopper has not yet seen stays visible as
/// <see cref="BasketLine.PreviousUnitPrice"/> until they look at the basket again (rule B4). The Catalog
/// never reaches into the basket: it publishes a price change, and the basket decides what it means.
/// </remarks>
public sealed class Basket : AggregateRoot<string>
{
    public const int MaximumQuantityPerLine = 10;

    public const int MaximumLines = 20;

    private readonly List<BasketLine> lines = [];

    private Basket()
    {
        Currency = string.Empty;
    }

    private Basket(string buyerId, string currency)
        : base(buyerId)
    {
        Currency = currency;
    }

    public string BuyerId => Id;

    public string Currency { get; private set; }

    public IReadOnlyList<BasketLine> Lines => lines;

    public decimal Total => lines.Sum(static l => l.LineTotal);

    public bool HasUnseenPriceChanges => lines.Any(static l => l.PreviousUnitPrice is not null);

    public static Basket Open(string buyerId, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buyerId);
        return new Basket(buyerId, currency);
    }

    /// <summary>
    /// Sets how many of a product the basket holds. Zero removes the line; removing a line that is not
    /// there changes nothing, so the storefront's DELETE is idempotent.
    /// </summary>
    public void SetQuantity(string sku, string productName, decimal catalogPrice, int priceVersion, bool sellable, int quantity)
    {
        var existing = lines.FirstOrDefault(l => l.Sku == sku);
        if (quantity == 0)
        {
            if (existing is not null)
            {
                lines.Remove(existing);
            }

            return;
        }

        CheckRule(new QuantityMustBeWithinLineLimit(quantity));
        CheckRule(new ProductMustBeSellable(sku, sellable));

        if (existing is null)
        {
            CheckRule(new BasketMustHaveRoomForAnotherLine(lines.Count));
            lines.Add(new BasketLine(sku, productName, catalogPrice, priceVersion, quantity));
            return;
        }

        // The shopper is looking at this product right now, at the catalog's price: nothing unseen.
        existing.SetQuantity(quantity);
        existing.Reprice(catalogPrice, priceVersion, keepPrevious: false);
        existing.Acknowledge();
    }

    /// <summary>Applies a price change from the Catalog; ignores one that is older than, or the same as, what the line holds.</summary>
    public bool ApplyPriceChange(string sku, decimal newPrice, int priceVersion)
    {
        var line = lines.FirstOrDefault(l => l.Sku == sku);
        if (line is null || line.PriceVersion >= priceVersion)
        {
            return false;
        }

        line.Reprice(newPrice, priceVersion, keepPrevious: true);
        return true;
    }

    public void AcknowledgePrices()
    {
        foreach (var line in lines)
        {
            line.Acknowledge();
        }
    }

    /// <summary>Hands every line to checkout, with what they cost together, and empties the basket.</summary>
    public TakenBasket TakeForCheckout()
    {
        var taken = new TakenBasket(lines.ToList(), Total, Currency);
        lines.Clear();
        return taken;
    }
}
