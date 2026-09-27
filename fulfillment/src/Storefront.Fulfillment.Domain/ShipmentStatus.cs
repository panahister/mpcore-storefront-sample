namespace Storefront.Fulfillment.Domain;

public enum ShipmentStatus
{
    /// <summary>Paid for and waiting in the warehouse.</summary>
    Pending = 1,

    /// <summary>Handed to a carrier.</summary>
    Dispatched = 2
}
