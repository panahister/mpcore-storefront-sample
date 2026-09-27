using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Catalog.Domain.Rules;
using MPCore.Application.Results;

namespace Storefront.Commerce.Modules.Catalog.Application;

/// <summary>
/// The failures a Catalog handler returns as values. These are expected outcomes (not found, already
/// exists), not broken rules: a broken rule is thrown by the aggregate and reported under its own code.
/// </summary>
public static class CatalogFailures
{
    public const string Domain = CatalogRule.Domain;

    public static FailureDescriptor ProductNotFound(string sku) => new(
        new ErrorIdentity(Domain, "PRODUCT_NOT_FOUND"),
        ErrorCategory.NotFound,
        new FailureMessageDescriptor("catalog.product_not_found", new Dictionary<string, string> { ["sku"] = sku }),
        RetryDirective.Never,
        [new ResourceFailureDetail("product", sku)]);

    public static FailureDescriptor SkuTaken(Sku sku) => new(
        new ErrorIdentity(Domain, "SKU_TAKEN"),
        ErrorCategory.AlreadyExists,
        new FailureMessageDescriptor("catalog.sku_taken", new Dictionary<string, string> { ["sku"] = sku.Value }),
        RetryDirective.Never,
        [new ResourceFailureDetail("product", sku.Value)]);
}
