namespace Storefront.Commerce.Modules.Basket.Contracts;

/// <summary>Where the shopper wants the order delivered, as they typed it at checkout.</summary>
/// <remarks>
/// A record prints every property, and whatever handles a message prints the message when it fails. A name,
/// a phone number and a street are personal data, so this one prints where the parcel goes and no more.
/// </remarks>
public sealed record CheckoutAddress(string RecipientName, string Phone, string Province, string City, string Line, string PostalCode)
{
    public override string ToString() => $"{nameof(CheckoutAddress)} {{ Province = {Province}, City = {City} }}";
}
