using Storefront.Commerce.Modules.Basket.Application.Commands;
using FluentValidation;

namespace Storefront.Commerce.Modules.Basket.Application.Validators;

/// <summary>The shape of the request. How many units a line may hold is the aggregate's rule, not this one.</summary>
public sealed class SetBasketItemValidator : AbstractValidator<SetBasketItem>
{
    public SetBasketItemValidator()
    {
        RuleFor(x => x.Sku).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
    }
}
