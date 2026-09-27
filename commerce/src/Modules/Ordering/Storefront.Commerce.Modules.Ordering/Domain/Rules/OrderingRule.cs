using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Ordering.Domain.Rules;

/// <summary>An Ordering business rule, reported under the <c>storefront.ordering</c> error domain. See the Catalog's rules for the pattern's origin.</summary>
public abstract class OrderingRule(string code, string messageKey, IReadOnlyDictionary<string, string>? arguments = null)
    : BusinessRule(Domain, code, messageKey, arguments)
{
    public const string Domain = "storefront.ordering";
}
