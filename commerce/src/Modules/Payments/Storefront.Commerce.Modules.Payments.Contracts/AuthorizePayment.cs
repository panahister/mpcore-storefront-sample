namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>Charge the order's card. Sent once the stock is held. A local module message, never leaves the host.</summary>
public sealed record AuthorizePayment(Guid OrderId);
