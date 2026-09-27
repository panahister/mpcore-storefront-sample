using System.Text.RegularExpressions;
using Storefront.Commerce.Modules.Ordering.Domain.Rules;
using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>
/// A phone number the courier can dial from anywhere: the international format of ITU-T E.164, a plus sign, a
/// country code and the number, fifteen digits at most. A value object with its rule inside.
/// </summary>
public readonly partial record struct PhoneNumber
{
    /// <summary>The format, for whoever has to state it again at an edge of their own.</summary>
    public const string Format = @"^\+[1-9][0-9]{7,14}$";

    /// <summary>A plus sign and fifteen digits.</summary>
    public const int MaximumLength = 16;

    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static PhoneNumber Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        BusinessRules.Check(new PhoneNumberMustBeInternational(trimmed));
        return new PhoneNumber(trimmed);
    }

    public static bool IsValid(string? text) => Pattern().IsMatch(text?.Trim() ?? string.Empty);

    public static PhoneNumber FromTrusted(string value) => new(value);

    public override string ToString() => Value;

    [GeneratedRegex(Format, RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
