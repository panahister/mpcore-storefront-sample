using MPCore.Audit;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Infrastructure.Audit;

/// <summary>
/// Which entity changes the audit trail records. Default deny: an entity that is not declared here
/// leaves no entity-change trace, and a property that is not included is not captured.
/// </summary>
/// <remarks>
/// The delivery address is personal data and is deliberately not audited: who dispatched which order with
/// which carrier is what a reviewer asks, and that is recorded.
/// </remarks>
public static class AuditPolicyConfiguration
{
    /// <summary>Declares the audited entities.</summary>
    public static void Configure(AuditPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        policy.Entity<Shipment>("fulfillment")
            .Include(s => s.Status)
            .Include(s => s.Carrier)
            .Include(s => s.TrackingCode);
    }
}
