using System.Text.RegularExpressions;
using Storefront.Commerce.Modules.Basket.Contracts;
using FluentValidation;

namespace Storefront.Commerce.Modules.Basket.Application.Validators;

/// <summary>The shape of a delivery address, checked where the shopper can still be told.</summary>
/// <remarks>
/// <para>
/// What an address must look like is Ordering's rule, and Ordering checks it again when it builds the order.
/// By then the shopper has their answer, so a refusal there can only reach an operator. The Basket therefore
/// refuses at the edge everything Ordering would refuse later.
/// </para>
/// <para>
/// Two modules stating one format is what two services do: each validates what it receives. What keeps
/// them from drifting apart is a test, not a shared class: <c>CheckoutContractTests</c> feeds the addresses
/// this validator accepts to Ordering's value objects. Ian Robinson described the idea as
/// <i>consumer-driven contracts</i>.
/// </para>
/// </remarks>
public sealed partial class CheckoutAddressValidator : AbstractValidator<CheckoutAddress>
{
    /// <summary>The international format of ITU-T E.164: a plus sign, a country code and the number.</summary>
    public const string PhonePattern = @"^\+[1-9][0-9]{7,14}$";

    /// <summary>Three to twelve letters and digits, with a space or a hyphen inside at most.</summary>
    public const string PostalCodePattern = "^[A-Za-z0-9][A-Za-z0-9 -]{1,10}[A-Za-z0-9]$";

    public CheckoutAddressValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(static text => Phone().IsMatch(text.Trim())).WithErrorCode("PHONE_INVALID").WithMessage("basket.phone_invalid");
        RuleFor(x => x.Province).NotEmpty().MaximumLength(50);
        RuleFor(x => x.City).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Line).NotEmpty().MaximumLength(300);
        RuleFor(x => x.PostalCode).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(static text => PostalCode().IsMatch(text.Trim())).WithErrorCode("POSTAL_CODE_INVALID").WithMessage("basket.postal_code_invalid");
    }

    [GeneratedRegex(PhonePattern, RegexOptions.CultureInvariant)]
    private static partial Regex Phone();

    [GeneratedRegex(PostalCodePattern, RegexOptions.CultureInvariant)]
    private static partial Regex PostalCode();
}
