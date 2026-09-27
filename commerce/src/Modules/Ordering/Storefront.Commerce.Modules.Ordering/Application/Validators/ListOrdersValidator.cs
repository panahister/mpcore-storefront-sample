using Storefront.Commerce.Modules.Ordering.Application.Queries;
using Storefront.Commerce.Modules.Ordering.Domain;
using FluentValidation;

namespace Storefront.Commerce.Modules.Ordering.Application.Validators;

public sealed class ListOrdersValidator : AbstractValidator<ListOrders>
{
    public ListOrdersValidator()
    {
        RuleFor(x => x.Status)
            .Must(static status => Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .When(x => x.Status is not null)
            .WithErrorCode("UNKNOWN").WithMessage("ordering.status_unknown");
        RuleFor(x => x.Sort).MaximumLength(32);
    }
}
