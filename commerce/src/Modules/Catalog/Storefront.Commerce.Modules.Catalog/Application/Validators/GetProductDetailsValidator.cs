using Storefront.Commerce.Modules.Catalog.Application.Queries;
using Storefront.Commerce.Modules.Catalog.Domain;
using FluentValidation;

namespace Storefront.Commerce.Modules.Catalog.Application.Validators;

public sealed class GetProductDetailsValidator : AbstractValidator<GetProductDetails>
{
    public GetProductDetailsValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(Sku.MaximumLength);
    }
}
