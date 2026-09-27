using Storefront.Commerce.Modules.Ordering.Domain.Rules;
using MPCore.Application.Results;

namespace Storefront.Commerce.Modules.Ordering.Application;

/// <summary>
/// The failures an Ordering handler returns as values: expected outcomes. A broken business rule is thrown
/// by the aggregate and reported under its own code instead.
/// </summary>
public static class OrderingFailures
{
    public const string Domain = OrderingRule.Domain;

    public static FailureDescriptor BuyerRequired() => new(
        new ErrorIdentity(Domain, "BUYER_REQUIRED"), ErrorCategory.Forbidden,
        new FailureMessageDescriptor("ordering.buyer_required"));

    /// <summary>No such order, or not one the caller may see. The two are deliberately indistinguishable.</summary>
    public static FailureDescriptor OrderNotFound() => new(
        new ErrorIdentity(Domain, "ORDER_NOT_FOUND"), ErrorCategory.NotFound,
        new FailureMessageDescriptor("ordering.order_not_found"));

    /// <summary>Support cancels only with a reason; a shopper needs none. Depends on the caller, so it is not a validator.</summary>
    public static FailureDescriptor NoteRequired() => Invalid("note", "REQUIRED", "ordering.note_required");

    public static FailureDescriptor OrderIdInvalid() => Invalid("order_id", "NOT_A_UUID", "ordering.order_id_invalid");

    public static FailureDescriptor StatusUnknown() => Invalid("status", "UNKNOWN", "ordering.status_unknown");

    private static FailureDescriptor Invalid(string fieldPath, string ruleCode, string messageKey) => new(
        new ErrorIdentity(Domain, "ORDER_INVALID"), ErrorCategory.Validation,
        new FailureMessageDescriptor(messageKey), RetryDirective.Never,
        [new ValidationFailureDetail([new FieldViolation(fieldPath, ruleCode, new FailureMessageDescriptor(messageKey))])]);
}
