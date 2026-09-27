namespace Storefront.Commerce.Modules.Catalog.Application.Views;

public sealed record RestockAlertView(string Sku, string ProductName, int AvailableAtAlert, int Threshold, DateTimeOffset RaisedOnUtc);
