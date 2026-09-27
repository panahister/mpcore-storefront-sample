using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Payments.Domain.Rules;

/// <summary>A Payments business rule, reported under the <c>storefront.payments</c> error domain. See the Catalog's rules for the pattern's origin.</summary>
public abstract class PaymentsRule(string code, string messageKey, IReadOnlyDictionary<string, string>? arguments = null)
    : BusinessRule(Domain, code, messageKey, arguments)
{
    public const string Domain = "storefront.payments";
}
