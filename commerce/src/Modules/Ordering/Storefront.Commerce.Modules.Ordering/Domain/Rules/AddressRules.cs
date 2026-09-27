namespace Storefront.Commerce.Modules.Ordering.Domain.Rules;

/// <summary>Rule O3: the courier calls a number that can be dialled from anywhere: <c>+</c>, country code, number.</summary>
public sealed class PhoneNumberMustBeInternational(string? text) : OrderingRule("PHONE_INVALID", "ordering.phone_invalid")
{
    public override bool IsBroken() => !PhoneNumber.IsValid(text);
}

/// <summary>Rule O3: a postal code is three to twelve letters and digits, with a space or a hyphen inside at most.</summary>
public sealed class PostalCodeMustBeWellFormed(string? text) : OrderingRule("POSTAL_CODE_INVALID", "ordering.postal_code_invalid")
{
    public override bool IsBroken() => !PostalCode.IsValid(text);
}
