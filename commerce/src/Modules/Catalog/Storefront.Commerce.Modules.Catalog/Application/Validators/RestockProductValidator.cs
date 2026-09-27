using Storefront.Commerce.Modules.Catalog.Application.Commands;
using Storefront.Commerce.Modules.Catalog.Domain;
using FluentValidation;

namespace Storefront.Commerce.Modules.Catalog.Application.Validators;

public sealed class RestockProductValidator : AbstractValidator<RestockProduct>
{
    public RestockProductValidator()
    {
        RuleFor(x => x.Sku).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(Sku.MaximumLength)
            .Must(Sku.IsValid).WithErrorCode("SKU_INVALID").WithMessage("catalog.sku_invalid");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(64);
    }
}
