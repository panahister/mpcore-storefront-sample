using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Storefront.Commerce.Modules.Payments.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Storefront.Commerce.Modules.Payments.Infrastructure;

/// <summary>
/// The adapter for the (simulated) DemoPay provider. Every request carries an <c>Idempotency-Key</c>;
/// without it, the resilient client's own retries could charge twice. 201 is approved, 402 is declined,
/// anything else is "unavailable", which the application treats as retryable.
/// </summary>
public sealed class DemoPayGateway(IHttpClientFactory clients, DemoPayOptions options, ILogger<DemoPayGateway> logger) : IPaymentGateway
{
    public const string ClientName = "demo-pay";

    public Task<GatewayResult> AuthorizeAsync(
        string idempotencyKey, decimal amount, string currency, string paymentToken, CancellationToken cancellationToken) =>
        SendAsync("v1/authorizations", idempotencyKey,
            new AuthorizationRequest(options.MerchantId, idempotencyKey, amount, currency, paymentToken), cancellationToken);

    public Task<GatewayResult> RefundAsync(
        string idempotencyKey, string providerReference, decimal amount, CancellationToken cancellationToken) =>
        SendAsync("v1/refunds", idempotencyKey,
            new RefundRequest(options.MerchantId, providerReference, amount), cancellationToken);

    private async Task<GatewayResult> SendAsync<TRequest>(
        string path, string idempotencyKey, TRequest body, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var answer = response.Content.Headers.ContentLength is > 0
                ? await response.Content.ReadFromJsonAsync<ProviderAnswer>(cancellationToken).ConfigureAwait(false)
                : null;
            return response.StatusCode switch
            {
                HttpStatusCode.Created or HttpStatusCode.OK when answer?.Reference is not null => GatewayResult.Approved(answer.Reference),
                HttpStatusCode.PaymentRequired => GatewayResult.Declined(answer?.Code ?? "DECLINED"),
                _ => Unavailable(path, (int)response.StatusCode)
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutException
                                              or Polly.CircuitBreaker.BrokenCircuitException
                                              or Polly.Timeout.TimeoutRejectedException
                                              or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
        {
            logger.LogWarning(exception, "DemoPay did not answer {Path}", path);
            return GatewayResult.Unavailable;
        }
    }

    private GatewayResult Unavailable(string path, int status)
    {
        logger.LogWarning("DemoPay answered {Path} with {Status}", path, status.ToString(CultureInfo.InvariantCulture));
        return GatewayResult.Unavailable;
    }

    private sealed record AuthorizationRequest(
        [property: JsonPropertyName("merchantId")] string MerchantId,
        [property: JsonPropertyName("orderId")] string OrderId,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("paymentToken")] string PaymentToken);

    private sealed record RefundRequest(
        [property: JsonPropertyName("merchantId")] string MerchantId,
        [property: JsonPropertyName("authorizationReference")] string AuthorizationReference,
        [property: JsonPropertyName("amount")] decimal Amount);

    private sealed record ProviderAnswer(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("reference")] string? Reference,
        [property: JsonPropertyName("code")] string? Code);
}
