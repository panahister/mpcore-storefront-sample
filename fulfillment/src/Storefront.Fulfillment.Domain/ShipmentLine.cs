namespace Storefront.Fulfillment.Domain;

/// <summary>What to pick from the shelf: which product, and how many.</summary>
public sealed record ShipmentLine(string Sku, string ProductName, int Quantity);
