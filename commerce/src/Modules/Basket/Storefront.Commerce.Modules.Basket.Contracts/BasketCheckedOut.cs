namespace Storefront.Commerce.Modules.Basket.Contracts;

/// <summary>
/// A shopper checked out. The message carries a snapshot of the basket as it was at that moment, so whoever
/// reacts needs nothing else from the Basket. A local module message: it never leaves the host.
/// </summary>
/// <param name="OrderId">
/// The identity the order will have. It is assigned at checkout, so the shopper can follow the order from
/// the first answer on, and every module that reacts is idempotent by it.
/// </param>
/// <param name="PaymentIntentId">
/// The payment intent the shopper pays with. The provider's token stays in the Payments module; this message
/// carries no secret.
/// </param>
public sealed record BasketCheckedOut(
    Guid OrderId,
    string BuyerId,
    string BuyerName,
    CheckoutAddress ShippingAddress,
    IReadOnlyList<CheckoutLine> Lines,
    decimal Total,
    string Currency,
    Guid PaymentIntentId,
    DateTimeOffset CheckedOutOnUtc)
{
    /// <summary>What a log may show of this message: which order, never the buyer or the address.</summary>
    public override string ToString() =>
        $"{nameof(BasketCheckedOut)} {{ OrderId = {OrderId}, Lines = {Lines.Count}, Total = {Total} {Currency} }}";
}
