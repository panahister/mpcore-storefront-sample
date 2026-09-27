using System.Globalization;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Modules.Payments.Contracts;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Domain.Rules;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Ordering.Application.Commands;

/// <summary>The shopper stops their own order, or support stops any order with a reason.</summary>
public sealed record CancelOrder(Guid OrderId, string? Note) : ICommand<Result<OrderView>>;

public static class CancelOrderHandler
{
    public static async Task<Result<OrderView>> Handle(
        CancelOrder command,
        ICurrentActorAccessor actor,
        IOrderRepository orders,
        IMessagePublisher publisher,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(audit);

        var caller = actor.Current;
        var bySupport = caller.HasRole(OrderingRoles.Support);
        if (bySupport && string.IsNullOrWhiteSpace(command.Note))
        {
            return Result<OrderView>.FromFailure(OrderingFailures.NoteRequired());
        }

        var order = await orders.GetAsync(new OrderId(command.OrderId), cancellationToken).ConfigureAwait(false);
        if (order is null || !(bySupport || OrderAccess.IsBuyer(caller, order)))
        {
            return Result<OrderView>.FromFailure(OrderingFailures.OrderNotFound());
        }

        var reason = bySupport ? CancellationReasons.SupportDecision : CancellationReasons.CustomerRequest;
        bool refundRequired;
        try
        {
            refundRequired = order.Cancel(reason, command.Note?.Trim(), clock.UtcNow);
        }
        catch (BusinessRuleValidationException refused)
        {
            // Refused by a rule, so nothing changed. The attempt is still recorded, detached from this
            // transaction, and the rule travels on to the caller as a 422 under its own code.
            await audit.RecordAttemptAsync(
                "ordering", "order-cancel", AuditOutcome.Rejected,
                new AuditFailure(refused.Rule.ErrorDomain, refused.Rule.Code),
                reason: reason, entityType: nameof(Order), entityId: order.OrderNumber,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Compensations. Both are idempotent at the receiving end, so sending them unconditionally is
        // simpler and safer than predicting which state the other module reached.
        await publisher.PublishAsync(new ReleaseStock(order.Id.Value), cancellationToken).ConfigureAwait(false);
        await publisher.PublishAsync(
            refundRequired ? new RefundPayment(order.Id.Value) : new VoidPayment(order.Id.Value),
            cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            "ordering", "order-cancelled", nameof(Order), order.OrderNumber,
            new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["refund_required"] = refundRequired.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);

        OrderingMetrics.Transitions.Add(1,
            new KeyValuePair<string, object?>("status", "cancelled"), new KeyValuePair<string, object?>("reason", reason));
        return Result<OrderView>.Success(OrderViews.Of(order));
    }
}
