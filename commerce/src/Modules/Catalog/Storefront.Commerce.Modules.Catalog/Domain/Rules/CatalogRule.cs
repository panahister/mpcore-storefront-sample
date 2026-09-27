using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>
/// A Catalog business rule. Every rule has a stable code, is reported under the <c>storefront.catalog</c>
/// error domain, and names a message key the edge renders in the caller's language.
/// </summary>
/// <remarks>
/// The named-rule pattern comes from Kamil Grzybek's <i>Modular Monolith with DDD</i>. The aggregate checks
/// a rule before it changes state, so an aggregate is never invalid: Vladimir Khorikov's <i>always-valid
/// domain model</i>, built on Eric Evans's and Vaughn Vernon's placement of invariants inside the aggregate.
/// </remarks>
public abstract class CatalogRule(string code, string messageKey, IReadOnlyDictionary<string, string>? arguments = null)
    : BusinessRule(Domain, code, messageKey, arguments)
{
    public const string Domain = "storefront.catalog";
}
