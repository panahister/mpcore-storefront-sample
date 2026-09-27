using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Catalog.Domain.Events;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Modules.Ordering.Domain.Events;
using Storefront.Commerce.Modules.Payments.Domain;
using Storefront.Commerce.Tests.Support;
using MPCore.Domain.Rules;
using BasketAggregate = Storefront.Commerce.Modules.Basket.Domain.Basket;

namespace Storefront.Commerce.Tests.Unit;

// A broken business rule throws BusinessRuleValidationException with the rule's own code (MP Core's
// IBusinessRule, after Kamil Grzybek's pattern). The tests assert the code, which is what a caller sees.

[Trait("Category", "Unit")]
public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("x")]
    [InlineData("TNT_ALV")]
    [InlineData("-TNT")]
    public void A_malformed_sku_breaks_the_format_rule(string sku) =>
        Assert.Equal("SKU_INVALID", Rules.Broken(() => Sku.Parse(sku)));

    [Fact]
    public void A_sku_is_normalised_to_upper_case_and_compared_by_value()
    {
        var sku = Sku.Parse(" tnt-alv-2p ");
        Assert.Equal("TNT-ALV-2P", sku.Value);
        Assert.Equal(Sku.Parse("TNT-ALV-2P"), sku);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_price_is_more_than_nothing(double amount) =>
        Assert.Equal("PRICE_NOT_POSITIVE", Rules.Broken(() => Price.Of((decimal)amount)));

    [Fact]
    public void A_price_has_cents_and_nothing_smaller()
    {
        Assert.Equal("PRICE_TOO_PRECISE", Rules.Broken(() => Price.Of(19.999m)));
        Assert.Equal(19.99m, Price.Of(19.99m).Amount);
        Assert.Equal("19.99 USD", Price.Of(19.99m).ToString());
    }

    [Fact]
    public void A_price_above_the_ceiling_is_a_typing_mistake() =>
        Assert.Equal("PRICE_ABOVE_CEILING", Rules.Broken(() => Price.Of(Price.Ceiling + 1)));

    [Theory]
    [InlineData("4155550123")]
    [InlineData("0014155550123")]
    [InlineData("+0155550123")]
    [InlineData("+1 415 555 0123")]
    [InlineData("+1234567")]
    public void A_phone_number_is_written_in_the_international_format(string phone) =>
        Assert.Equal("PHONE_INVALID", Rules.Broken(() => PhoneNumber.Parse(phone)));

    [Fact]
    public void A_postal_code_is_three_to_twelve_letters_and_digits()
    {
        Assert.Equal("POSTAL_CODE_INVALID", Rules.Broken(() => PostalCode.Parse("12")));
        Assert.Equal("POSTAL_CODE_INVALID", Rules.Broken(() => PostalCode.Parse("94103-")));
        Assert.Equal("POSTAL_CODE_INVALID", Rules.Broken(() => PostalCode.Parse("1234567890123")));
        Assert.Equal("94103", PostalCode.Parse(" 94103 ").Value);
        Assert.Equal("SW1A 1AA", PostalCode.Parse("SW1A 1AA").Value);
        Assert.Equal("1000-205", PostalCode.Parse("1000-205").Value);
    }

    [Fact]
    public void Two_addresses_with_the_same_parts_are_the_same_address()
    {
        Assert.Equal(Orders.Home(), Orders.Home());
        Assert.NotEqual(Orders.Home(), new ShippingAddress("Sara Ahmadi", PhoneNumber.Parse("+14155550123"), "California", "Oakland", "12 Harbour Street", PostalCode.Parse("94103")));
    }
}

[Trait("Category", "Unit")]
public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = FakeClock.At2026().UtcNow;

    [Fact]
    public void An_unknown_category_is_refused() =>
        Assert.Equal("CATEGORY_UNKNOWN", Rules.Broken(() =>
            Product.List(ProductId.New(), Sku.Parse("TNT-1"), "Tent", "", "kayaks", "Northface Works", Price.Of(1_000m), 1, 0)));

    [Fact]
    public void Changing_the_price_raises_the_integration_event_with_the_next_price_version()
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 10);

        product.ChangePrice(Price.Of(1_200m), Now);

        var changed = Assert.Single(product.IntegrationEvents.OfType<ProductPriceChanged>());
        Assert.Equal((1_000m, 1_200m, 2), (changed.OldPrice, changed.NewPrice, changed.PriceVersion));
        Assert.Equal("storefront.catalog.product-price-changed", changed.EventName);
        Assert.Contains(product.DomainEvents, e => e is ProductUpdated);
    }

    [Theory]
    [InlineData(1_501)]
    [InlineData(499)]
    public void A_single_price_change_may_not_move_the_price_by_more_than_half(int newPrice)
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 10);

        var broken = Assert.Throws<BusinessRuleValidationException>(() => product.ChangePrice(Price.Of(newPrice), Now));

        Assert.Equal("PRICE_JUMP_TOO_LARGE", broken.Rule.Code);
        Assert.Equal(("1000.00", "50"), (broken.Rule.MessageArguments["current"], broken.Rule.MessageArguments["max_move_percent"]));
        Assert.Empty(product.IntegrationEvents);
        Assert.Equal(1_000m, product.Price.Amount);
    }

    [Fact]
    public void Repricing_to_the_same_price_is_refused() =>
        Assert.Equal("PRICE_UNCHANGED", Rules.Broken(() => new FakeProducts().Seed("TNT-1", 1_000m, 10).ChangePrice(Price.Of(1_000m), Now)));

    [Fact]
    public void A_discontinued_product_cannot_be_repriced_restocked_or_reserved()
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 10);
        product.Discontinue();

        Assert.Equal("PRODUCT_DISCONTINUED", Rules.Broken(() => product.ChangePrice(Price.Of(1_100m), Now)));
        Assert.Equal("PRODUCT_DISCONTINUED", Rules.Broken(() => product.Restock(5)));
        Assert.Equal("PRODUCT_DISCONTINUED", Rules.Broken(() => product.Discontinue()));
        Assert.False(product.CanReserve(1));
        Assert.Equal("PRODUCT_DISCONTINUED", Rules.Broken(() => product.Reserve(1)));
    }

    [Fact]
    public void Crossing_the_reorder_threshold_raises_one_alert_not_one_per_sale()
    {
        var product = new FakeProducts().Seed("BOOT-42", 1_000m, stock: 6, threshold: 3);

        product.Reserve(2); // 4 left, above the line
        product.Reserve(1); // 3 left: crossed
        product.Reserve(1); // 2 left: already below

        var alert = Assert.Single(product.DomainEvents.OfType<StockFellBelowThreshold>());
        Assert.Equal(3, alert.Available);
    }

    [Fact]
    public void Reserving_releasing_and_shipping_keep_the_warehouse_numbers_consistent()
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 10);

        product.Reserve(4);
        Assert.Equal((10, 4, 6), (product.OnHand, product.Reserved, product.Available));

        product.Release(1);
        Assert.Equal((10, 3, 7), (product.OnHand, product.Reserved, product.Available));

        product.CommitShipment(3);
        Assert.Equal((7, 0, 7), (product.OnHand, product.Reserved, product.Available));
    }

    [Fact]
    public void Reserving_more_than_is_available_is_refused_and_changes_nothing()
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 2);

        var broken = Assert.Throws<BusinessRuleValidationException>(() => product.Reserve(3));

        Assert.Equal("INSUFFICIENT_STOCK", broken.Rule.Code);
        Assert.Equal(("3", "2"), (broken.Rule.MessageArguments["requested"], broken.Rule.MessageArguments["available"]));
        Assert.Equal(0, product.Reserved);
    }

    [Fact]
    public void Releasing_more_than_was_reserved_is_refused()
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 10);
        product.Reserve(2);
        Assert.Equal("RESERVATION_MISMATCH", Rules.Broken(() => product.Release(3)));
        Assert.Equal("QUANTITY_NOT_POSITIVE", Rules.Broken(() => product.Restock(0)));
    }
}

[Trait("Category", "Unit")]
public sealed class BasketTests
{
    [Fact]
    public void Setting_a_quantity_adds_replaces_and_removes_a_line()
    {
        var basket = BasketAggregate.Open("sara", "USD");

        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 2);
        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 3);
        Assert.Equal(3_000m, basket.Total);

        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 0);
        Assert.Empty(basket.Lines);
    }

    [Fact]
    public void Removing_a_line_that_is_not_there_changes_nothing()
    {
        var basket = BasketAggregate.Open("sara", "USD");
        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 0);
        Assert.Empty(basket.Lines);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(-1)]
    public void A_line_holds_one_to_ten_units(int quantity) =>
        Assert.Equal("QUANTITY_OUT_OF_RANGE", Rules.Broken(() => BasketAggregate.Open("sara", "USD").SetQuantity("TNT-1", "Tent", 1_000m, 1, true, quantity)));

    [Fact]
    public void A_product_that_is_not_sellable_cannot_be_added() =>
        Assert.Equal("PRODUCT_NOT_SELLABLE", Rules.Broken(() => BasketAggregate.Open("sara", "USD").SetQuantity("TNT-1", "Tent", 1_000m, 1, false, 1)));

    [Fact]
    public void A_basket_holds_at_most_twenty_different_products()
    {
        var basket = BasketAggregate.Open("sara", "USD");
        for (var i = 0; i < BasketAggregate.MaximumLines; i++)
        {
            basket.SetQuantity($"SKU-{i}", "Thing", 1_000m, 1, true, 1);
        }

        Assert.Equal("TOO_MANY_LINES", Rules.Broken(() => basket.SetQuantity("ONE-MORE", "Thing", 1_000m, 1, true, 1)));
        Assert.Equal(BasketAggregate.MaximumLines, basket.Lines.Count);
    }

    [Fact]
    public void A_line_is_a_child_entity_identified_by_its_sku()
    {
        var basket = BasketAggregate.Open("sara", "USD");
        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 2);
        var line = Assert.Single(basket.Lines);
        Assert.Equal("TNT-1", line.Id);
        Assert.Equal(line.Id, line.Sku);
    }

    [Fact]
    public void A_price_change_is_applied_and_remembered_until_the_shopper_looks()
    {
        var basket = BasketAggregate.Open("sara", "USD");
        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 2);

        Assert.True(basket.ApplyPriceChange("TNT-1", 1_200m, 2));

        var line = Assert.Single(basket.Lines);
        Assert.Equal((1_200m, (decimal?)1_000m), (line.UnitPrice, line.PreviousUnitPrice));
        Assert.True(basket.HasUnseenPriceChanges);

        basket.AcknowledgePrices();
        Assert.False(basket.HasUnseenPriceChanges);
    }

    [Fact]
    public void A_stale_or_repeated_price_change_is_ignored()
    {
        var basket = BasketAggregate.Open("sara", "USD");
        basket.SetQuantity("TNT-1", "Tent", 1_000m, 3, true, 1);

        Assert.False(basket.ApplyPriceChange("TNT-1", 900m, 2));   // older than what the basket holds
        Assert.False(basket.ApplyPriceChange("TNT-1", 1_000m, 3)); // the same version again
        Assert.Equal(1_000m, basket.Lines[0].UnitPrice);
    }

    [Fact]
    public void Checkout_hands_the_lines_over_and_empties_the_basket()
    {
        var basket = BasketAggregate.Open("sara", "USD");
        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 2);

        var taken = basket.TakeForCheckout();

        Assert.Single(taken.Lines);
        Assert.Equal((2_000m, "USD"), (taken.Total, taken.Currency));
        Assert.Empty(basket.Lines);
        Assert.Empty(basket.TakeForCheckout().Lines);
    }
}

[Trait("Category", "Unit")]
public sealed class PaymentIntentTests
{
    private static readonly DateTimeOffset Now = FakeClock.At2026().UtcNow;

    [Fact]
    public void An_intent_gives_its_token_to_one_order_and_keeps_nothing()
    {
        var intent = PaymentIntent.Create("sara", "tok_visa_ok", Now);
        var order = Guid.NewGuid();

        Assert.Equal("tok_visa_ok", intent.UseFor(order, "sara", Now));
        Assert.Equal((null, order), (intent.PaymentToken, intent.UsedByOrderId));
        Assert.Equal("PAYMENT_INTENT_UNUSABLE", Rules.Broken(() => intent.UseFor(Guid.NewGuid(), "sara", Now)));
    }

    [Fact]
    public void Only_its_own_shopper_may_use_it_and_only_before_it_expires()
    {
        var intent = PaymentIntent.Create("sara", "tok_visa_ok", Now);

        Assert.Equal("PAYMENT_INTENT_UNUSABLE", Rules.Broken(() => intent.UseFor(Guid.NewGuid(), "reza", Now)));
        Assert.Equal("PAYMENT_INTENT_UNUSABLE", Rules.Broken(() => intent.UseFor(Guid.NewGuid(), "sara", Now + PaymentIntent.Lifetime)));
        Assert.True(intent.IsUsableBy("sara", Now + PaymentIntent.Lifetime - TimeSpan.FromSeconds(1)));
    }
}

[Trait("Category", "Unit")]
public sealed class StockReceiptTests
{
    private static readonly DateTimeOffset Now = FakeClock.At2026().UtcNow;

    [Fact]
    public void A_delivery_note_is_the_same_note_however_it_was_typed()
    {
        var receipt = StockReceipt.Record(Sku.Parse("tnt-1"), " dn-2026-001 ", 5, Now);

        Assert.Equal(("TNT-1", "DN-2026-001", 5), (receipt.Sku, receipt.Reference, receipt.Quantity));
        Assert.Equal(receipt.Reference, StockReceipt.NormalizeReference("DN-2026-001"));
    }

    [Fact]
    public void The_same_delivery_reported_again_with_the_same_quantity_breaks_nothing()
    {
        var receipt = StockReceipt.Record(Sku.Parse("TNT-1"), "DN-1", 5, Now);
        receipt.ConfirmRepeated(5);
    }

    [Fact]
    public void The_same_delivery_reported_with_another_quantity_breaks_rule_C14()
    {
        var receipt = StockReceipt.Record(Sku.Parse("TNT-1"), "DN-1", 5, Now);

        var broken = Assert.Throws<BusinessRuleValidationException>(() => receipt.ConfirmRepeated(6));

        Assert.Equal(("storefront.catalog", "DELIVERY_REFERENCE_REUSED"), (broken.Rule.ErrorDomain, broken.Rule.Code));
        Assert.Equal(("5", "6"), (broken.Rule.MessageArguments["received"], broken.Rule.MessageArguments["requested"]));
    }

    [Fact]
    public void A_receipt_is_for_a_positive_quantity() =>
        Assert.Equal("QUANTITY_NOT_POSITIVE", Rules.Broken(() => StockReceipt.Record(Sku.Parse("TNT-1"), "DN-1", 0, Now)));
}

[Trait("Category", "Unit")]
public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = FakeClock.At2026().UtcNow;

    [Fact]
    public void Placing_an_order_raises_order_placed_without_the_street_address()
    {
        var order = Order.Place(OrderId.New(), "sara", "sara", Orders.Home(), "USD",
            [new OrderLine("TNT-1", "Tent", 1_000m, 2)], Now);

        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.StartsWith("ORD-260927-", order.OrderNumber, StringComparison.Ordinal);
        var placed = Assert.Single(order.IntegrationEvents.OfType<OrderPlaced>());
        Assert.Equal((2_000m, "San Francisco"), (placed.Total, placed.City));
    }

    [Fact]
    public void An_order_without_lines_is_refused() =>
        Assert.Equal("NO_LINES", Rules.Broken(() => Order.Place(OrderId.New(), "sara", "sara", Orders.Home(), "USD", [], Now)));

    [Fact]
    public void The_happy_path_is_submitted_awaiting_payment_paid_shipped()
    {
        var order = Orders.Placed();

        order.ConfirmStock(Now);
        order.MarkPaid("DMP-1", Now);
        order.Ship("Post", "PST-123", Now);

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal(4, order.Version);
        Assert.Single(order.IntegrationEvents.OfType<OrderPaid>());
        Assert.Single(order.IntegrationEvents.OfType<OrderShipped>());
        Assert.Equal(["Submitted", "AwaitingPayment", "Paid", "Shipped"], order.History.Select(h => h.Status));
    }

    [Fact]
    public void A_payment_cannot_be_recorded_before_the_stock_is_confirmed()
    {
        var order = Orders.Placed();

        var broken = Assert.Throws<BusinessRuleValidationException>(() => order.MarkPaid("DMP-1", Now));

        Assert.Equal("ORDER_STATUS_MISMATCH", broken.Rule.Code);
        Assert.Equal(("AwaitingPayment", "Submitted"), (broken.Rule.MessageArguments["expected"], broken.Rule.MessageArguments["actual"]));
        Assert.Equal(OrderStatus.Submitted, order.Status);
    }

    [Fact]
    public void Cancelling_a_paid_order_requires_a_refund()
    {
        var order = Orders.Placed();
        order.ConfirmStock(Now);
        order.MarkPaid("DMP-1", Now);

        var refund = order.Cancel(CancellationReasons.CustomerRequest, null, Now);

        Assert.True(refund);
        Assert.True(Assert.Single(order.IntegrationEvents.OfType<OrderCancelled>()).RefundRequired);
    }

    [Fact]
    public void A_shipped_order_cannot_be_cancelled_and_a_cancelled_one_is_final()
    {
        var shipped = Orders.Placed();
        shipped.ConfirmStock(Now);
        shipped.MarkPaid("DMP-1", Now);
        shipped.Ship("Post", "PST-1", Now);
        Assert.Equal("ALREADY_SHIPPED", Rules.Broken(() => shipped.Cancel(CancellationReasons.CustomerRequest, null, Now)));

        var cancelled = Orders.Placed();
        cancelled.Cancel(CancellationReasons.CustomerRequest, null, Now);
        Assert.Equal("ALREADY_CANCELLED", Rules.Broken(() => cancelled.Cancel(CancellationReasons.SupportDecision, "again", Now)));
        Assert.Equal("ALREADY_CANCELLED", Rules.Broken(() => cancelled.ConfirmStock(Now)));
        Assert.False(cancelled.CanCancel);
    }

    [Fact]
    public void A_refund_is_recorded_once_however_often_it_is_reported()
    {
        var order = Orders.Placed();
        Assert.True(order.RecordRefund("SPR-1", Now));
        Assert.False(order.RecordRefund("SPR-1", Now));
        Assert.Single(order.History, h => h.Status == "Refunded");
    }
}

[Trait("Category", "Unit")]
public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = FakeClock.At2026().UtcNow;

    [Fact]
    public void The_token_is_erased_as_soon_as_the_provider_answers()
    {
        var payment = Payment.Register(Guid.NewGuid(), 1_000m, "USD", "tok_visa_ok", Now);
        payment.MarkAuthorized("DMP-1", Now);
        Assert.Null(payment.PaymentToken);
    }

    [Fact]
    public void Only_an_authorized_payment_can_be_refunded()
    {
        var payment = Payment.Register(Guid.NewGuid(), 1_000m, "USD", "tok_visa_ok", Now);
        Assert.Equal("PAYMENT_NOT_AUTHORIZED", Rules.Broken(() => payment.MarkRefunded("SPR-1", Now)));
        payment.MarkAuthorized("DMP-1", Now);
        payment.MarkRefunded("SPR-1", Now);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void A_voided_payment_is_never_charged()
    {
        var payment = Payment.Register(Guid.NewGuid(), 1_000m, "USD", "tok_visa_ok", Now);
        payment.Void(Now);
        Assert.Equal("PAYMENT_NOT_PENDING", Rules.Broken(() => payment.MarkAuthorized("DMP-1", Now)));
    }

    [Fact]
    public void A_payment_is_for_a_positive_amount() =>
        Assert.Equal("AMOUNT_NOT_POSITIVE", Rules.Broken(() => Payment.Register(Guid.NewGuid(), 0m, "USD", "tok_visa_ok", Now)));
}
