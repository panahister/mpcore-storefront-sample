namespace Storefront.Commerce.Modules.Payments.Domain.Rules;

/// <summary>Rule P6: a payment is for a positive amount.</summary>
public sealed class AmountMustBePositive(decimal amount) : PaymentsRule("AMOUNT_NOT_POSITIVE", "payments.amount_not_positive")
{
    public override bool IsBroken() => amount <= 0m;
}
