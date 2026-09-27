using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Catalog.Domain.Events;

/// <summary>Available stock crossed the reorder threshold downwards. Raised once per crossing (rule C9).</summary>
public sealed record StockFellBelowThreshold(
    Guid ProductId, string Sku, string ProductName, int Available, int Threshold) : DomainEvent;
