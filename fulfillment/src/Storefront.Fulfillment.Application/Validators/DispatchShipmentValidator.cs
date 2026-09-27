using FluentValidation;
using Storefront.Fulfillment.Application.Commands;

namespace Storefront.Fulfillment.Application.Validators;

/// <summary>Runs before <c>DispatchShipmentHandler</c>.</summary>
public sealed class DispatchShipmentValidator : AbstractValidator<DispatchShipment>
{
    public DispatchShipmentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(60);
        RuleFor(x => x.TrackingCode).NotEmpty().MaximumLength(60);
    }
}
