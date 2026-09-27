using System.Text.RegularExpressions;
using Storefront.Commerce.Modules.Ordering.Domain.Rules;
using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>
/// A postal code, as countries write them: three to twelve letters and digits, with a space or a hyphen
/// inside at most ("94103", "SW1A 1AA", "1000-205"). A value object with its rule inside.
/// </summary>
public readonly partial record struct PostalCode
{
    /// <summary>The format, for whoever has to state it again at an edge of their own.</summary>
    public const string Format = "^[A-Za-z0-9][A-Za-z0-9 -]{1,10}[A-Za-z0-9]$";

    public const int MaximumLength = 12;

    private PostalCode(string value) => Value = value;

    public string Value { get; }

    public static PostalCode Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        BusinessRules.Check(new PostalCodeMustBeWellFormed(trimmed));
        return new PostalCode(trimmed);
    }

    public static bool IsValid(string? text) => Pattern().IsMatch(text?.Trim() ?? string.Empty);

    public static PostalCode FromTrusted(string value) => new(value);

    public override string ToString() => Value;

    [GeneratedRegex(Format, RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
