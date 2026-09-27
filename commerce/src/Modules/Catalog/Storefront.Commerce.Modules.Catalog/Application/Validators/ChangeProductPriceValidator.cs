using Storefront.Commerce.Modules.Catalog.Application.Commands;
using Storefront.Commerce.Modules.Catalog.Domain;
using FluentValidation;

namespace Storefront.Commerce.Modules.Catalog.Application.Validators;

public sealed class ChangeProductPriceValidator : AbstractValidator<ChangeProductPrice>
{
    public ChangeProductPriceValidator()
    {
        RuleFor(x => x.Sku).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(Sku.MaximumLength)
            .Must(Sku.IsValid).WithErrorCode("SKU_INVALID").WithMessage("catalog.sku_invalid");
        RuleFor(x => x.NewPrice).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}
