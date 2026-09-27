namespace Storefront.Commerce.Modules.Catalog.Application.Ports;

public sealed record ProductFilter(string? Category, string? Search, bool IncludeDiscontinued);
