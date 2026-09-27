using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>A note to purchasing: this product's available stock fell to its reorder threshold.</summary>
public sealed class RestockAlert : AggregateRoot<Guid>
{
    private RestockAlert()
    {
        Sku = string.Empty;
        ProductName = string.Empty;
    }

    private RestockAlert(Guid id, string sku, string productName, int available, int threshold, DateTimeOffset now)
        : base(id)
    {
        Sku = sku;
        ProductName = productName;
        AvailableAtAlert = available;
        Threshold = threshold;
        RaisedOnUtc = now;
    }

    public string Sku { get; private set; }

    public string ProductName { get; private set; }

    public int AvailableAtAlert { get; private set; }

    public int Threshold { get; private set; }

    public DateTimeOffset RaisedOnUtc { get; private set; }

    public static RestockAlert Raise(string sku, string productName, int available, int threshold, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), sku, productName, available, threshold, now);
}
