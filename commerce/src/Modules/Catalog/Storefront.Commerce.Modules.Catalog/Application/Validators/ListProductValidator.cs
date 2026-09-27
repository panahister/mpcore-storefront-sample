using Storefront.Commerce.Modules.Catalog.Application.Commands;
using Storefront.Commerce.Modules.Catalog.Domain;
using FluentValidation;

namespace Storefront.Commerce.Modules.Catalog.Application.Validators;

/// <summary>
/// The shape of the request: required fields, lengths, non-negative numbers. It runs before the handler.
/// The business rules (the price ceiling, the category list, the SKU format) belong to the value objects
/// and the aggregate; where a validator repeats one, it calls the domain's own check so the rule is
/// written once.
/// </summary>
public sealed class ListProductValidator : AbstractValidator<ListProduct>
{
    public ListProductValidator()
    {
        RuleFor(x => x.Sku).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(Sku.MaximumLength)
            .Must(Sku.IsValid).WithErrorCode("SKU_INVALID").WithMessage("catalog.sku_invalid");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Brand).MaximumLength(100);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0);
    }
}
