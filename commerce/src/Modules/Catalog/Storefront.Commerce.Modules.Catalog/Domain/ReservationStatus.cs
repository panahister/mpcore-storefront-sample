namespace Storefront.Commerce.Modules.Catalog.Domain;

public enum ReservationStatus
{
    /// <summary>The units are held for the order.</summary>
    Held = 1,

    /// <summary>The order could not be served; nothing is held.</summary>
    Rejected = 2,

    /// <summary>The units went back to the shelf.</summary>
    Released = 3,

    /// <summary>The units left the warehouse with the shipment.</summary>
    Committed = 4
}
