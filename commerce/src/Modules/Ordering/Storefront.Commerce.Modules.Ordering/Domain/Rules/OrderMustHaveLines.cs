namespace Storefront.Commerce.Modules.Ordering.Domain.Rules;

/// <summary>Rule O11: an order is for at least one thing.</summary>
public sealed class OrderMustHaveLines(int lineCount) : OrderingRule("NO_LINES", "ordering.no_lines")
{
    public override bool IsBroken() => lineCount == 0;
}
