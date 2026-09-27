namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>
/// Register the charge of a new order, paid with the shopper's payment intent. Sent when the order is
/// created, before the warehouse is asked. A local module message: it never leaves the host, and it carries
/// no secret, only the intent's identity.
/// </summary>
/// <param name="BuyerId">Whose order it is; the intent must belong to the same shopper.</param>
public sealed record RegisterPayment(Guid OrderId, decimal Amount, string Currency, Guid PaymentIntentId, string BuyerId);
