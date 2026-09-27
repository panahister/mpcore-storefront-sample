namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>The identity of a product. A typed identifier cannot be confused with an order's.</summary>
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
