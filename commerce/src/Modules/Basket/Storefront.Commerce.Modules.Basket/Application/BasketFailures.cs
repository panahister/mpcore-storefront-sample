using Storefront.Commerce.Modules.Basket.Domain.Rules;
using MPCore.Application.Results;

namespace Storefront.Commerce.Modules.Basket.Application;

/// <summary>The failures a Basket handler returns as values: expected outcomes, not broken rules.</summary>
public static class BasketFailures
{
    public const string Domain = BasketRule.Domain;

    public static FailureDescriptor BuyerRequired() => new(
        new ErrorIdentity(Domain, "BUYER_REQUIRED"), ErrorCategory.Forbidden,
        new FailureMessageDescriptor("basket.buyer_required"));

    public static FailureDescriptor BasketEmpty() => new(
        new ErrorIdentity(Domain, "BASKET_EMPTY"), ErrorCategory.Precondition,
        new FailureMessageDescriptor("basket.empty"), RetryDirective.Never,
        [new PreconditionFailureDetail([new PreconditionViolation("basket", "basket", "BASKET_EMPTY",
            new FailureMessageDescriptor("basket.empty"))])]);

    /// <summary>The basket no longer costs what the shopper expects; they must look at it again before paying.</summary>
    public static FailureDescriptor TotalChanged() => new(
        new ErrorIdentity(Domain, "BASKET_TOTAL_CHANGED"), ErrorCategory.Precondition,
        new FailureMessageDescriptor("basket.total_changed"), RetryDirective.Never,
        [new PreconditionFailureDetail([new PreconditionViolation("basket", "total", "BASKET_TOTAL_CHANGED",
            new FailureMessageDescriptor("basket.total_changed"))])]);

    /// <summary>The payment intent is unknown, another shopper's, used or expired: the shopper enters the card again.</summary>
    public static FailureDescriptor PaymentIntentUnusable() => new(
        new ErrorIdentity(Domain, "PAYMENT_INTENT_UNUSABLE"), ErrorCategory.Precondition,
        new FailureMessageDescriptor("basket.payment_intent_unusable"), RetryDirective.Never,
        [new PreconditionFailureDetail([new PreconditionViolation("payment_intent", "payment_intent_id", "PAYMENT_INTENT_UNUSABLE",
            new FailureMessageDescriptor("basket.payment_intent_unusable"))])]);

    public static FailureDescriptor ProductNotFound(string sku) => new(
        new ErrorIdentity(Domain, "PRODUCT_NOT_FOUND"), ErrorCategory.NotFound,
        new FailureMessageDescriptor("basket.product_not_found", new Dictionary<string, string> { ["sku"] = sku }),
        RetryDirective.Never,
        [new ResourceFailureDetail("product", sku)]);
}
