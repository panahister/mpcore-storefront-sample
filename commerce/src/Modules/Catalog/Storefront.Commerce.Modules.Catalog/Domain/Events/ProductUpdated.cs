using MPCore.Domain.Events;

namespace Storefront.Commerce.Modules.Catalog.Domain.Events;

/// <summary>Something about the product changed. A domain event, handled in-process after the commit.</summary>
public sealed record ProductUpdated(Guid ProductId, string Sku) : DomainEvent;
