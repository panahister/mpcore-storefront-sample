using MPCore.Domain.Rules;

namespace Storefront.Fulfillment.Domain.Rules;

/// <summary>
/// A Fulfillment business rule. Every rule has a stable code, is reported under the
/// <c>storefront.fulfillment</c> error domain, and names a message key the edge renders in the caller's language.
/// </summary>
/// <remarks>
/// The named-rule pattern comes from Kamil Grzybek's <i>Modular Monolith with DDD</i>. The aggregate checks
/// a rule before it changes state, so an aggregate is never invalid: Vladimir Khorikov's <i>always-valid
/// domain model</i>.
/// </remarks>
public abstract class FulfillmentRule(string code, string messageKey, IReadOnlyDictionary<string, string>? arguments = null)
    : BusinessRule(Domain, code, messageKey, arguments)
{
    public const string Domain = "storefront.fulfillment";
}

/// <summary>Rule F1: a shipment is dispatched once.</summary>
public sealed class ShipmentMustBePending(Shipment shipment) : FulfillmentRule(
    "SHIPMENT_NOT_PENDING", "fulfillment.shipment_not_pending",
    new Dictionary<string, string> { ["order_number"] = shipment.OrderNumber, ["status"] = shipment.Status.ToString() })
{
    public override bool IsBroken() => shipment.Status != ShipmentStatus.Pending;
}

/// <summary>Rule F2: there is something to ship.</summary>
public sealed class ShipmentMustHaveLines(int lineCount) : FulfillmentRule("SHIPMENT_EMPTY", "fulfillment.shipment_empty")
{
    public override bool IsBroken() => lineCount == 0;
}
