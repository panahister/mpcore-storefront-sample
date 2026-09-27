namespace Storefront.Commerce.Modules.Payments.Domain;

public enum PaymentStatus
{
    /// <summary>Registered at checkout; the card has not been charged.</summary>
    Pending = 1,

    /// <summary>The provider approved the charge.</summary>
    Authorized = 2,

    /// <summary>The provider refused the charge.</summary>
    Declined = 3,

    /// <summary>Cancelled before any charge.</summary>
    Voided = 4,

    /// <summary>Charged, then paid back.</summary>
    Refunded = 5
}
