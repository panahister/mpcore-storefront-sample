namespace Storefront.Commerce.Modules.Basket.Domain;

/// <summary>What a basket held at the moment it was taken for checkout. The basket itself is empty afterwards.</summary>
public sealed record TakenBasket(IReadOnlyList<BasketLine> Lines, decimal Total, string Currency);
