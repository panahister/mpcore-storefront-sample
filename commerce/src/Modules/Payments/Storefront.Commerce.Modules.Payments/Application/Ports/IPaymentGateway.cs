namespace Storefront.Commerce.Modules.Payments.Application.Ports;

/// <summary>The payment provider, as the application needs it: a port whose adapter lives in Infrastructure.</summary>
public interface IPaymentGateway
{
    /// <summary>Charges a card. <paramref name="idempotencyKey"/> makes a retried charge the same charge.</summary>
    Task<GatewayResult> AuthorizeAsync(string idempotencyKey, decimal amount, string currency, string paymentToken, CancellationToken cancellationToken);

    Task<GatewayResult> RefundAsync(string idempotencyKey, string providerReference, decimal amount, CancellationToken cancellationToken);
}
