using Storefront.Commerce.Modules.Catalog.Application.Queries;
using Storefront.Commerce.Modules.Catalog.Domain;
using FluentValidation;

namespace Storefront.Commerce.Modules.Catalog.Application.Validators;

/// <summary>Paging is clamped by <c>PageRequest</c> itself; only the filter's shape is checked here.</summary>
public sealed class BrowseProductsValidator : AbstractValidator<BrowseProducts>
{
    public BrowseProductsValidator()
    {
        RuleFor(x => x.Category).Must(ProductCategories.IsKnown)
            .When(x => x.Category is not null)
            .WithErrorCode("CATEGORY_UNKNOWN").WithMessage("catalog.category_unknown");
        RuleFor(x => x.Search).MaximumLength(64);
        RuleFor(x => x.Sort).MaximumLength(32);
    }
}
