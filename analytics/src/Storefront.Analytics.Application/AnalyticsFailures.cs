using MPCore.Application.Results;

namespace Storefront.Analytics.Application;

/// <summary>The failures a handler returns as values.</summary>
public static class AnalyticsFailures
{
    public const string Domain = "storefront.analytics";

    /// <summary>The longest period one report covers.</summary>
    public static readonly TimeSpan MaximumPeriod = TimeSpan.FromDays(31);

    public static FailureDescriptor PeriodInvalid() => Invalid("from", "PERIOD_INVALID", "analytics.period_invalid");

    public static FailureDescriptor PeriodTooLong() => Invalid("to", "PERIOD_TOO_LONG", "analytics.period_too_long");

    private static FailureDescriptor Invalid(string fieldPath, string ruleCode, string messageKey) => new(
        new ErrorIdentity(Domain, "REPORT_INVALID"), ErrorCategory.Validation,
        new FailureMessageDescriptor(messageKey, new Dictionary<string, string> { ["max_days"] = "31" }), RetryDirective.Never,
        [new ValidationFailureDetail([new FieldViolation(fieldPath, ruleCode, new FailureMessageDescriptor(messageKey, new Dictionary<string, string> { ["max_days"] = "31" }))])]);
}
