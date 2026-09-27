namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>The order's life: Submitted → AwaitingPayment → Paid → Shipped, or Cancelled from anywhere but Shipped.</summary>
public enum OrderStatus
{
    /// <summary>Placed; the warehouse has been asked to hold the stock.</summary>
    Submitted = 1,

    /// <summary>Stock is held; the card is being charged.</summary>
    AwaitingPayment = 2,

    /// <summary>Charged; waiting for the warehouse to ship.</summary>
    Paid = 3,

    /// <summary>Gone. Final.</summary>
    Shipped = 4,

    /// <summary>Stopped, by the shopper, support, the warehouse or the bank. Final.</summary>
    Cancelled = 5
}
