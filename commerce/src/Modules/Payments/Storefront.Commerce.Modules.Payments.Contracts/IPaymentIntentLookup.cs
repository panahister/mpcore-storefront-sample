namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>
/// Lets checkout ask, before it answers the shopper, whether the payment intent it was given can pay for
/// an order. A read: it changes nothing, so it couples nothing a message could not later replace.
/// </summary>
/// <remarks>
/// The answer can change before the intent is used: another checkout may use it first. The Payments module
/// decides again when it registers the charge, and refuses the charge then.
/// </remarks>
public interface IPaymentIntentLookup
{
    /// <summary>Whether the intent exists, belongs to this shopper, is unused and has not expired.</summary>
    Task<bool> IsUsableAsync(Guid paymentIntentId, string buyerId, CancellationToken cancellationToken);
}
