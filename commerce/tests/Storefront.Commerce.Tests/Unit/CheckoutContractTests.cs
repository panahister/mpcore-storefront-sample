using Storefront.Commerce.Modules.Basket.Application.Validators;
using Storefront.Commerce.Modules.Basket.Contracts;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Tests.Support;

namespace Storefront.Commerce.Tests.Unit;

/// <summary>
/// The Basket accepts a checkout and Ordering builds the order from it a moment later, when the shopper can
/// no longer be told that something is wrong. So the Basket must refuse, at the edge, every address
/// Ordering would refuse. The two modules share no code; this test is what holds them together.
/// </summary>
/// <remarks>
/// Ian Robinson's <i>consumer-driven contracts</i>: the consumer's expectations (here Ordering's value
/// objects) are run against what the provider lets through (here the Basket's validator). Between two
/// services the same test is a Pact; between two modules of one host it is a unit test.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class CheckoutContractTests
{
    public static TheoryData<string> Phones() =>
        ["+14155550123", " +14155550123 ", "+442079460123", "4155550123", "0014155550123", "+0155550123", "+1234567", "+1234567890123456", "+1 415 555 0123", "+1415555O123", "+１４１５５５５０１２３", "", "   "];

    public static TheoryData<string> PostalCodes() =>
        ["94103", " 94103 ", "SW1A 1AA", "1000-205", "K1A 0B1", "12", "1234567890123", "-94103", "94103-", "94_103", "９４１０３", "", "   "];

    private static bool BasketAccepts(CheckoutAddress address) => new CheckoutAddressValidator().Validate(address).IsValid;

    private static CheckoutAddress Address(string phone = "+14155550123", string postalCode = "94103") =>
        new("Sara Ahmadi", phone, "California", "San Francisco", "12 Harbour Street", postalCode);

    [Theory]
    [MemberData(nameof(Phones))]
    public void The_basket_accepts_exactly_the_phone_numbers_ordering_accepts(string phone) =>
        Assert.Equal(PhoneNumber.IsValid(phone), BasketAccepts(Address(phone: phone)));

    [Theory]
    [MemberData(nameof(PostalCodes))]
    public void The_basket_accepts_exactly_the_postal_codes_ordering_accepts(string postalCode) =>
        Assert.Equal(PostalCode.IsValid(postalCode), BasketAccepts(Address(postalCode: postalCode)));

    [Fact]
    public void An_address_the_basket_accepts_is_an_address_ordering_can_build()
    {
        var accepted = Address(phone: " +14155550123 ", postalCode: " 94103 ");
        Assert.True(BasketAccepts(accepted));

        var address = new ShippingAddress(
            accepted.RecipientName, PhoneNumber.Parse(accepted.Phone), accepted.Province, accepted.City, accepted.Line,
            PostalCode.Parse(accepted.PostalCode));

        Assert.Equal(("+14155550123", "94103"), (address.Phone.Value, address.PostalCode.Value));
    }

    [Theory]
    [InlineData("", "California", "San Francisco", "12 Harbour Street")]
    [InlineData("Sara", " ", "San Francisco", "12 Harbour Street")]
    [InlineData("Sara", "California", "", "12 Harbour Street")]
    [InlineData("Sara", "California", "San Francisco", "  ")]
    public void A_blank_part_ordering_would_throw_on_never_leaves_the_basket(string recipient, string province, string city, string line) =>
        Assert.False(BasketAccepts(new CheckoutAddress(recipient, "+14155550123", province, city, line, "94103")));
}
