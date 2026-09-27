using System.Globalization;
using Storefront.Commerce.Modules.Catalog.Domain.Rules;
using MPCore.Domain.Rules;

namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>
/// A catalog price: a positive amount in dollars and cents, below the ceiling. A value object, so a price
/// cannot exist in a form the business would refuse.
/// </summary>
/// <remarks>
/// The store sells in one currency. Money in several currencies is a value object of amount and currency
/// together (Martin Fowler's <i>Money</i> pattern); one currency needs the amount only.
/// </remarks>
public readonly record struct Price
{
    public const string Currency = "USD";

    /// <summary>Above this, the number is a typing mistake, not a price (rule C2).</summary>
    public const decimal Ceiling = 100_000m;

    private Price(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    /// <summary>Checks the price rules and returns the price; throws the broken rule otherwise.</summary>
    public static Price Of(decimal amount)
    {
        BusinessRules.Check(new PriceMustBePositive(amount));
        BusinessRules.Check(new PriceMustBeInCents(amount));
        BusinessRules.Check(new PriceMustNotExceedCeiling(amount));
        return new Price(amount);
    }

    public static bool IsValid(decimal amount) =>
        amount > 0m && decimal.Round(amount, 2) == amount && amount <= Ceiling;

    /// <summary>For the persistence adapter: a value that was validated when it was stored.</summary>
    public static Price FromTrusted(decimal amount) => new(amount);

    public override string ToString() => Amount.ToString("0.00", CultureInfo.InvariantCulture) + " " + Currency;
}
