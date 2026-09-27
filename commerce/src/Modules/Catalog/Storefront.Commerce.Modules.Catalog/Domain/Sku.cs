using System.Text.RegularExpressions;
using Storefront.Commerce.Modules.Catalog.Domain.Rules;
using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>
/// A stock-keeping unit: upper-case letters, digits and hyphens, three to thirty-two characters. A value
/// object: two SKUs with the same text are the same SKU, and one can only exist in a valid form.
/// </summary>
/// <remarks>
/// A single-value object is a <c>readonly record struct</c>: equality by value comes for free, and it costs
/// nothing at run time. Its rule lives here, once, instead of in every handler that receives a string
/// (Martin Fowler and Kent Beck call the alternative <i>primitive obsession</i>).
/// </remarks>
public readonly partial record struct Sku
{
    public const int MaximumLength = 32;

    private Sku(string value) => Value = value;

    public string Value { get; }

    /// <summary>Normalizes and validates the text; throws the <c>SKU_INVALID</c> rule when it is malformed.</summary>
    public static Sku Parse(string? text)
    {
        var normalized = Normalize(text);
        BusinessRules.Check(new SkuMustMatchFormat(normalized));
        return new Sku(normalized);
    }

    public static bool TryParse(string? text, out Sku sku)
    {
        var normalized = Normalize(text);
        if (Pattern().IsMatch(normalized))
        {
            sku = new Sku(normalized);
            return true;
        }

        sku = default;
        return false;
    }

    /// <summary>Whether the text is a well-formed SKU. Validators use this so the rule is written once.</summary>
    public static bool IsValid(string? text) => Pattern().IsMatch(Normalize(text));

    public static string Normalize(string? text) => (text ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>For the persistence adapter: a value that was validated when it was stored.</summary>
    public static Sku FromTrusted(string value) => new(value);

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{1,30}[A-Z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
