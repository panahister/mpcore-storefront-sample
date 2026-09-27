using Storefront.Commerce.Api.Hosting;
using Storefront.Commerce.Modules.Basket.Application.Commands;
using Storefront.Commerce.Modules.Basket.Application.Validators;
using Storefront.Commerce.Modules.Basket.Contracts;
using Storefront.Commerce.Modules.Catalog.Application.Commands;
using Storefront.Commerce.Modules.Catalog.Application.Validators;
using Storefront.Commerce.Modules.Payments.Application.Commands;
using Storefront.Commerce.Modules.Payments.Application.Validators;
using FluentValidation;
using MPCore.Application.Results;
using MPCore.Validation.FluentValidation;

namespace Storefront.Commerce.Tests.Unit;

/// <summary>
/// Input validators check the shape of a request before the handler runs (Jeremy Skinner's FluentValidation,
/// as Wolverine middleware). What reaches the caller is MP Core's failure model: a field path, a rule code
/// and a message key, which these tests assert through the same conversion the host uses.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ValidatorTests
{
    private static IReadOnlyList<FieldViolation> Violations<T>(IValidator<T> validator, T instance) =>
        [.. validator.Validate(instance).Errors.Select(ValidationFailures.ToViolation)];

    private static CheckoutAddress Address(string phone = "+14155550123", string postalCode = "94103") =>
        new("Sara Ahmadi", phone, "California", "San Francisco", "12 Harbour Street", postalCode);

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("")]
    public void Anything_but_a_provider_token_is_refused_before_a_payment_intent_exists(string token)
    {
        var violation = Assert.Single(Violations(new CreatePaymentIntentValidator(), new CreatePaymentIntent(token)));

        Assert.Equal("payment_token", violation.FieldPath);
        Assert.Equal(token.Length == 0 ? "NOT_EMPTY" : "PAYMENT_TOKEN_INVALID", violation.RuleCode);
    }

    [Fact]
    public void A_checkout_names_the_payment_intent_it_pays_with()
    {
        Assert.Equal("payment_intent_id", Assert.Single(Violations(new CheckoutValidator(), new Checkout(Address(), Guid.Empty, 1_000m))).FieldPath);
    }

    [Fact]
    public void A_nested_address_field_is_reported_with_its_path_and_the_rules_code()
    {
        var violations = Violations(new CheckoutValidator(), new Checkout(Address(phone: "4155550123", postalCode: "12"), Guid.NewGuid(), 1_000m));

        Assert.Equal(("PHONE_INVALID", "basket.phone_invalid"), Single(violations, "shipping_address.phone"));
        Assert.Equal(("POSTAL_CODE_INVALID", "basket.postal_code_invalid"), Single(violations, "shipping_address.postal_code"));
    }

    [Fact]
    public void A_missing_address_is_one_violation_not_six()
    {
        var violations = Violations(new CheckoutValidator(), new Checkout(null!, Guid.NewGuid(), 1_000m));
        Assert.Equal(("NOT_NULL", "validation.not_null"), Single(violations, "shipping_address"));
        Assert.Single(violations);
    }

    [Fact]
    public void A_new_product_needs_a_well_formed_sku_and_non_negative_stock()
    {
        var violations = Violations(new ListProductValidator(), new ListProduct("bad sku", "", "", "tents", "", 0m, -1, 0));

        Assert.Equal(("SKU_INVALID", "catalog.sku_invalid"), Single(violations, "sku"));
        Assert.Equal(("NOT_EMPTY", "validation.not_empty"), Single(violations, "name"));
        Assert.Equal(("GREATER_THAN", "validation.greater_than"), Single(violations, "price"));
        Assert.Equal("0", Assert.Single(violations, v => v.FieldPath == "initial_stock").Message.Arguments["comparison_value"]);
    }

    [Fact]
    public void A_basket_quantity_may_be_zero_to_remove_but_never_negative()
    {
        Assert.Empty(Violations(new SetBasketItemValidator(), new SetBasketItem("TNT-1", 0)));
        Assert.Equal("quantity", Assert.Single(Violations(new SetBasketItemValidator(), new SetBasketItem("TNT-1", -1))).FieldPath);
    }

    [Fact]
    public void A_translation_needs_a_known_culture_and_a_well_formed_key()
    {
        var violations = Violations(new SetTranslationValidator(), new SetTranslation("Ordering.Basket", "xx-notreal", ""));

        Assert.Equal(("KEY_INVALID", "localization.key_invalid"), Single(violations, "key"));
        Assert.Equal(("CULTURE_UNKNOWN", "localization.culture_unknown"), Single(violations, "culture"));
        Assert.Equal("NOT_EMPTY", Assert.Single(violations, v => v.FieldPath == "text").RuleCode);
        Assert.Empty(Violations(new SetTranslationValidator(), new SetTranslation("basket.empty", "fa", "متن")));
    }

    private static (string Code, string Key) Single(IReadOnlyList<FieldViolation> violations, string fieldPath)
    {
        var violation = Assert.Single(violations, v => v.FieldPath == fieldPath);
        return (violation.RuleCode, violation.Message.Key);
    }
}
