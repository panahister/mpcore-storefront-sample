using Storefront.Commerce.Modules.Ordering.Application.Commands;
using FluentValidation;

namespace Storefront.Commerce.Modules.Ordering.Application.Validators;

public sealed class CancelOrderValidator : AbstractValidator<CancelOrder>
{
    public CancelOrderValidator()
    {
        RuleFor(x => x.Note).MaximumLength(200);
    }
}
