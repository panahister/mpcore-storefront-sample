using Storefront.Commerce.Modules.Catalog.Domain.Events;
using Storefront.Commerce.Modules.Catalog.Domain.Rules;
using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>
/// A product the store sells: its listing, its price, and its warehouse stock. The aggregate owns every
/// rule about these three things, and checks each rule before it changes anything.
/// </summary>
/// <remarks>
/// <para>
/// Stock is two numbers. <see cref="OnHand"/> is what is physically in the warehouse; <see cref="Reserved"/>
/// is what orders in progress have claimed. What the storefront may still sell is
/// <see cref="Available"/>, the difference. A shipment takes units out of both.
/// </para>
/// <para>
/// The product raises one integration event, <see cref="ProductPriceChanged"/>, because other systems
/// care about prices; and two domain events, <see cref="ProductUpdated"/> for the cache and
/// <see cref="StockFellBelowThreshold"/> for purchasing.
/// </para>
/// </remarks>
public sealed class Product : AggregateRoot<ProductId>
{
    /// <summary>The largest relative price move one repricing may make (rule C3).</summary>
    public const decimal MaximumPriceMove = 0.5m;

    private Product()
    {
        Name = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        Brand = string.Empty;
    }

    private Product(
        ProductId id, Sku sku, string name, string description, string category, string brand,
        Price price, int onHand, int reorderThreshold)
        : base(id)
    {
        Sku = sku;
        Name = name;
        Description = description;
        Category = category;
        Brand = brand;
        Price = price;
        OnHand = onHand;
        ReorderThreshold = reorderThreshold;
        Status = ProductStatus.Active;
        PriceVersion = 1;
        Touched();
    }

    public Sku Sku { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public string Category { get; private set; }

    public string Brand { get; private set; }

    public Price Price { get; private set; }

    /// <summary>Incremented on every repricing; the Basket uses it to ignore stale or repeated price events.</summary>
    public int PriceVersion { get; private set; }

    public ProductStatus Status { get; private set; }

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    /// <summary>When available stock falls to this line, purchasing is told once (rule C9).</summary>
    public int ReorderThreshold { get; private set; }

    public int Available => OnHand - Reserved;

    public bool IsSellable => Status == ProductStatus.Active;

    /// <summary>Lists a new product. Input shape was validated at the edge; the business rules are checked here.</summary>
    public static Product List(
        ProductId id, Sku sku, string name, string description, string category, string brand,
        Price price, int initialStock, int reorderThreshold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(initialStock);
        ArgumentOutOfRangeException.ThrowIfNegative(reorderThreshold);
        CheckRule(new CategoryMustBeKnown(category));

        return new Product(
            id, sku, name.Trim(), description?.Trim() ?? string.Empty, category, brand?.Trim() ?? string.Empty,
            price, initialStock, reorderThreshold);
    }

    public void ChangePrice(Price newPrice, DateTimeOffset now)
    {
        CheckRule(new ProductMustBeActive(this));
        CheckRule(new PriceMustChange(Price, newPrice));
        CheckRule(new PriceMoveMustBeGradual(Price, newPrice));

        var oldPrice = Price;
        Price = newPrice;
        PriceVersion++;
        Raise(new ProductPriceChanged(Id.Value, Sku.Value, Name, oldPrice.Amount, newPrice.Amount, Price.Currency, PriceVersion, now));
        Touched();
    }

    public void Restock(int quantity)
    {
        CheckRule(new ProductMustBeActive(this));
        CheckRule(new QuantityMustBePositive(quantity));

        OnHand += quantity;
        Touched();
    }

    public void Discontinue()
    {
        CheckRule(new ProductMustBeActive(this));

        Status = ProductStatus.Discontinued;
        Touched();
    }

    /// <summary>Answers without throwing, for a handler that must decide a whole order at once.</summary>
    public bool CanReserve(int quantity) => IsSellable && quantity > 0 && Available >= quantity;

    public void Reserve(int quantity)
    {
        CheckRule(new ProductMustBeActive(this));
        CheckRule(new QuantityMustBePositive(quantity));
        CheckRule(new StockMustBeAvailable(this, quantity));

        var before = Available;
        Reserved += quantity;

        // Raised once per crossing, not on every sale below the line: purchasing needs to hear that
        // the shelf is getting empty, not to be told again with every tent that leaves it.
        if (before > ReorderThreshold && Available <= ReorderThreshold)
        {
            Raise(new StockFellBelowThreshold(Id.Value, Sku.Value, Name, Available, ReorderThreshold));
        }

        Touched();
    }

    public void Release(int quantity)
    {
        CheckRule(new QuantityMustBePositive(quantity));
        CheckRule(new ReservedUnitsMustCover(this, quantity));

        Reserved -= quantity;
        Touched();
    }

    public void CommitShipment(int quantity)
    {
        CheckRule(new QuantityMustBePositive(quantity));
        CheckRule(new ReservedUnitsMustCover(this, quantity));

        Reserved -= quantity;
        OnHand -= quantity;
        Touched();
    }

    private void Touched() => Raise(new ProductUpdated(Id.Value, Sku.Value));
}
