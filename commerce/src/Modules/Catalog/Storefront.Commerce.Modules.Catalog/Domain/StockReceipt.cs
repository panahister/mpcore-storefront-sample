using Storefront.Commerce.Modules.Catalog.Domain.Rules;
using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>The Catalog's memory of one delivery into the warehouse: which product, which delivery note, how many units.</summary>
/// <remarks>
/// <para>
/// A delivery note names a delivery once. The pair of SKU and reference is therefore a business key, and
/// it is what makes restocking idempotent: a repeated request finds the receipt and adds nothing. Gregor
/// Hohpe and Bobby Woolf call this the <i>Idempotent Receiver</i> (<i>Enterprise Integration Patterns</i>);
/// Pat Helland's <i>Idempotence Is Not a Medical Condition</i> gives the reason a caller's retry has to be
/// expected. The same choice already protects stock reservations, whose key is the order.
/// </para>
/// <para>
/// A business key is kept for as long as the receipt exists, where a request's <c>Idempotency-Key</c> is
/// remembered for a day. A delivery note that comes back next month is still the same delivery.
/// </para>
/// </remarks>
public sealed class StockReceipt : AggregateRoot<Guid>
{
    public const int MaximumReferenceLength = 64;

    private StockReceipt()
    {
        Sku = string.Empty;
        Reference = string.Empty;
    }

    private StockReceipt(Guid id, string sku, string reference, int quantity, DateTimeOffset now)
        : base(id)
    {
        Sku = sku;
        Reference = reference;
        Quantity = quantity;
        ReceivedOnUtc = now;
    }

    public string Sku { get; private set; }

    /// <summary>The delivery note or purchase order, as <see cref="NormalizeReference"/> writes it.</summary>
    public string Reference { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset ReceivedOnUtc { get; private set; }

    public static StockReceipt Record(Sku sku, string reference, int quantity, DateTimeOffset now)
    {
        var normalized = NormalizeReference(reference);
        ArgumentException.ThrowIfNullOrEmpty(normalized, nameof(reference));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(normalized.Length, MaximumReferenceLength, nameof(reference));
        CheckRule(new QuantityMustBePositive(quantity));

        return new StockReceipt(Guid.CreateVersion7(), sku.Value, normalized, quantity, now);
    }

    /// <summary>The same delivery, reported again. Rule C14: it must say what the receipt says.</summary>
    public void ConfirmRepeated(int quantity) => CheckRule(new DeliveryMustMatchItsReceipt(this, quantity));

    /// <summary>"dn-2026-001 " and "DN-2026-001" are one delivery note.</summary>
    public static string NormalizeReference(string? text) => (text ?? string.Empty).Trim().ToUpperInvariant();
}
