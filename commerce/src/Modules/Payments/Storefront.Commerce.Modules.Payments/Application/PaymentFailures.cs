using Storefront.Commerce.Modules.Payments.Domain.Rules;
using MPCore.Application.Results;

namespace Storefront.Commerce.Modules.Payments.Application;

public static class PaymentFailures
{
    public const string Domain = PaymentsRule.Domain;

    public static readonly TimeSpan ProviderRetryAfter = TimeSpan.FromSeconds(5);

    public static FailureDescriptor BuyerRequired() => new(
        new ErrorIdentity(Domain, "BUYER_REQUIRED"), ErrorCategory.Forbidden,
        new FailureMessageDescriptor("payments.buyer_required"));

    /// <summary>Retryable: the host's error policy redelivers the message with a cooldown, then dead-letters it.</summary>
    public static FailureDescriptor ProviderUnavailable() => new(
        new ErrorIdentity(Domain, "PROVIDER_UNAVAILABLE"), ErrorCategory.DependencyUnavailable,
        new FailureMessageDescriptor("payments.provider_unavailable"),
        RetryDirective.After(ProviderRetryAfter));
}
