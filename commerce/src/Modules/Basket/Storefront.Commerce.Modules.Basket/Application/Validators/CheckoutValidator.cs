using Storefront.Commerce.Modules.Basket.Application.Commands;
using FluentValidation;

namespace Storefront.Commerce.Modules.Basket.Application.Validators;

/// <summary>Runs before <c>CheckoutHandler</c>: an invalid request never touches the basket.</summary>
public sealed class CheckoutValidator : AbstractValidator<Checkout>
{
    public CheckoutValidator()
    {
        RuleFor(x => x.ShippingAddress).NotNull();
        RuleFor(x => x.ShippingAddress!).SetValidator(new CheckoutAddressValidator()).When(x => x.ShippingAddress is not null);
        RuleFor(x => x.PaymentIntentId).NotEmpty();
        RuleFor(x => x.ExpectedTotal).GreaterThanOrEqualTo(0);
    }
}
