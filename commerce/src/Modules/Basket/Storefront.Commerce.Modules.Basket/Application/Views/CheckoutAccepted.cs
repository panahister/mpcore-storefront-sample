namespace Storefront.Commerce.Modules.Basket.Application.Views;

/// <summary>
/// The answer to a checkout: it was accepted, and this is the order to follow. The order itself is created
/// by Ordering a moment later; until then <c>GET /v1/orders/{orderId}</c> answers 404.
/// </summary>
public sealed record CheckoutAccepted(Guid OrderId, decimal Total, string Currency, DateTimeOffset AcceptedOnUtc);
