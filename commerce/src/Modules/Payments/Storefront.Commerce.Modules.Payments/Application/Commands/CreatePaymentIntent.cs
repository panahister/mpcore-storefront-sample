using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Application.Views;
using Storefront.Commerce.Modules.Payments.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Persistence.Abstractions;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Payments.Application.Commands;

/// <summary>The shopper hands Payments the token the provider issued to their browser, and receives a reference to pay with.</summary>
/// <remarks>
/// Wolverine logs a message whose handling failed by printing it, and a record prints every property. The
/// token is a secret (rule P5), so this record says what it prints.
/// </remarks>
public sealed record CreatePaymentIntent(string PaymentToken) : ICommand<Result<PaymentIntentView>>
{
    public override string ToString() => nameof(CreatePaymentIntent);
}

public static class CreatePaymentIntentHandler
{
    public static Result<PaymentIntentView> Handle(
        CreatePaymentIntent command,
        ICurrentActorAccessor actor,
        IPaymentIntentRepository intents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);

        // Identity comes from the validated token, never from the request.
        var buyerId = actor.Current.SubjectId;
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return Result<PaymentIntentView>.FromFailure(PaymentFailures.BuyerRequired());
        }

        var intent = PaymentIntent.Create(buyerId, command.PaymentToken, clock.UtcNow);
        intents.Add(intent);
        return Result<PaymentIntentView>.Success(new PaymentIntentView(intent.Id, intent.ExpiresOnUtc));
    }
}
