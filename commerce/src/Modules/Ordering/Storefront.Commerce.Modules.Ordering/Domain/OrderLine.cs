namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>What was bought, at the price the shopper saw. Immutable once the order is placed: a value object as a record.</summary>
public sealed record OrderLine(string Sku, string ProductName, decimal UnitPrice, int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
