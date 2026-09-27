namespace Storefront.Commerce.Modules.Catalog.Domain;

public enum ProductStatus
{
    /// <summary>For sale.</summary>
    Active = 1,

    /// <summary>Withdrawn for good: never sold, repriced or restocked again (rule C5).</summary>
    Discontinued = 2
}
