namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>Why an order stopped. Stable codes: they are stored, audited and published.</summary>
public static class CancellationReasons
{
    public const string CustomerRequest = "CUSTOMER_REQUEST";

    public const string SupportDecision = "SUPPORT_DECISION";

    public const string OutOfStock = "OUT_OF_STOCK";

    public const string PaymentDeclined = "PAYMENT_DECLINED";

    /// <summary>The request for the order's stock was given up; nothing was held and nothing was charged.</summary>
    public const string ReservationFailed = "RESERVATION_FAILED";
}
