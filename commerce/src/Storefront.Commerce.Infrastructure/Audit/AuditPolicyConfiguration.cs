using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Modules.Payments.Domain;
using MPCore.Audit;

namespace Storefront.Commerce.Infrastructure.Audit;

/// <summary>
/// Which entity changes the audit trail records. Default deny: an entity that is not declared here
/// leaves no entity-change trace, and a property that is not included is not captured.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds of record reach the trail. <b>Entity changes</b> are captured automatically by the
/// interceptor for the properties below, in the transaction that made them. <b>Business actions</b> —
/// "price-changed", "order-cancelled", "payment-refunded" — are recorded by handlers through
/// <c>IBusinessAuditRecorder</c> with the context a property diff cannot carry: the reason, the actor's
/// role, the provider reference.
/// </para>
/// <para>
/// <c>Payment.PaymentToken</c> is deliberately absent, and could not be added: MP Core refuses to
/// audit any property whose name contains "token", masked or not. Stock counters are absent because a
/// reservation changes them on every order; the reservation table itself is their history.
/// </para>
/// </remarks>
public static class AuditPolicyConfiguration
{
    /// <summary>Declares the audited entities.</summary>
    public static void Configure(AuditPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        policy.Entity<Product>("catalog")
            .Include(p => p.Price)
            .Include(p => p.Status);

        policy.Entity<Order>("ordering")
            .Include(o => o.Status)
            .Include(o => o.CancellationReason)
            .Include(o => o.TrackingCode);

        policy.Entity<Payment>("payments")
            .Include(p => p.Status)
            .Include(p => p.Amount)
            .Include(p => p.DeclineCode);
    }
}
