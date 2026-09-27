using MPCore.Application.Results;
using Storefront.Fulfillment.Domain.Rules;

namespace Storefront.Fulfillment.Application;

/// <summary>The failures a handler returns as values: expected outcomes, not broken rules.</summary>
public static class FulfillmentFailures
{
    public const string Domain = FulfillmentRule.Domain;

    public static FailureDescriptor ShipmentNotFound() => new(
        new ErrorIdentity(Domain, "SHIPMENT_NOT_FOUND"), ErrorCategory.NotFound,
        new FailureMessageDescriptor("fulfillment.shipment_not_found"));

    public static FailureDescriptor OrderIdInvalid() => Invalid("order_id", "NOT_A_UUID", "fulfillment.order_id_invalid");

    public static FailureDescriptor StatusUnknown() => Invalid("status", "UNKNOWN", "fulfillment.status_unknown");

    private static FailureDescriptor Invalid(string fieldPath, string ruleCode, string messageKey) => new(
        new ErrorIdentity(Domain, "SHIPMENT_INVALID"), ErrorCategory.Validation,
        new FailureMessageDescriptor(messageKey), RetryDirective.Never,
        [new ValidationFailureDetail([new FieldViolation(fieldPath, ruleCode, new FailureMessageDescriptor(messageKey))])]);
}
