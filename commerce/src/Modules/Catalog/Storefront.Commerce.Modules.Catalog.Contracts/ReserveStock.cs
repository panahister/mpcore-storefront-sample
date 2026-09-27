namespace Storefront.Commerce.Modules.Catalog.Contracts;

/// <summary>
/// Hold the units of an order. The order identifier is the idempotency key: a redelivered request finds
/// the decision already taken and repeats the answer.
/// </summary>
/// <remarks>
/// Module messages travel on Wolverine's durable local queues inside this host and never leave it. They
/// are not integration events, carry no contract version, and change together with the modules that use
/// them.
/// </remarks>
public sealed record ReserveStock(Guid OrderId, IReadOnlyList<StockLine> Lines);
