using Storefront.Commerce.Modules.Ordering.Application.Commands;
using FluentValidation;

namespace Storefront.Commerce.Modules.Ordering.Application.Validators;

public sealed class ShipOrderValidator : AbstractValidator<ShipOrder>
{
    public ShipOrderValidator()
    {
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TrackingCode).NotEmpty().MaximumLength(64);
    }
}
