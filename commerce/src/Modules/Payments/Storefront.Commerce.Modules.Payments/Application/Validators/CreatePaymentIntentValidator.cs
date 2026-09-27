using Storefront.Commerce.Modules.Payments.Application.Commands;
using FluentValidation;

namespace Storefront.Commerce.Modules.Payments.Application.Validators;

/// <summary>Runs before <c>CreatePaymentIntentHandler</c>.</summary>
public sealed class CreatePaymentIntentValidator : AbstractValidator<CreatePaymentIntent>
{
    /// <summary>A provider token, never a card number: the card is the provider's business, not ours.</summary>
    public const string PaymentTokenPattern = "^tok_[a-z0-9_]{3,60}$";

    public CreatePaymentIntentValidator()
    {
        RuleFor(x => x.PaymentToken).Cascade(CascadeMode.Stop).NotEmpty()
            .Matches(PaymentTokenPattern).WithErrorCode("PAYMENT_TOKEN_INVALID").WithMessage("payments.payment_token_invalid");
    }
}
