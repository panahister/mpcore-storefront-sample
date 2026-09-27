using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Basket.Domain.Rules;

/// <summary>A Basket business rule, reported under the <c>storefront.basket</c> error domain. See the Catalog's rules for the pattern's origin.</summary>
public abstract class BasketRule(string code, string messageKey, IReadOnlyDictionary<string, string>? arguments = null)
    : BusinessRule(Domain, code, messageKey, arguments)
{
    public const string Domain = "storefront.basket";
}
