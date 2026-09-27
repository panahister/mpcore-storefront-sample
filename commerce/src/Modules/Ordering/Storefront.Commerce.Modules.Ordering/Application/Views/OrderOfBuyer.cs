namespace Storefront.Commerce.Modules.Ordering.Application.Views;

/// <summary>
/// An order as the read side returns it, together with whose it is. The application needs the owner to
/// decide who may see the order; the owner's identifier itself is never sent to a caller.
/// </summary>
public sealed record OrderOfBuyer(string BuyerId, OrderView Order);
