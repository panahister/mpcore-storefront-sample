using Storefront.Commerce.Modules.Basket.Application.Commands;
using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Basket.Application.Queries;
using Storefront.Commerce.Modules.Basket.Application.Views;
using Storefront.Commerce.Modules.Basket.Contracts;
using Storefront.Commerce.Modules.Catalog.Application.Commands;
using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Queries;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Ordering.Application.Commands;
using Storefront.Commerce.Modules.Ordering.Application.Process;
using Storefront.Commerce.Modules.Ordering.Application.Queries;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Modules.Payments.Application.Commands;
using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Contracts;
using Storefront.Commerce.Modules.Payments.Domain;
using Storefront.Commerce.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using MPCore.Application.Querying;
using MPCore.Application.Results;
using MPCore.Audit;
using MPCore.Domain.Rules;
using BasketAggregate = Storefront.Commerce.Modules.Basket.Domain.Basket;

namespace Storefront.Commerce.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class StockReservationHandlerTests
{
    private readonly FakeProducts products = new();
    private readonly FakeReservations reservations = new();
    private readonly FakePublisher publisher = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeClock clock = FakeClock.At2026();

    private Task Reserve(Guid orderId, params StockLine[] lines) =>
        ReserveStockHandler.Handle(new ReserveStock(orderId, lines), products, reservations, publisher, unitOfWork, clock,
            NullLogger<ReserveStock>.Instance, CancellationToken.None);

    private Task Release(Guid orderId) =>
        ReleaseStockHandler.Handle(new ReleaseStock(orderId), products, reservations, unitOfWork, clock,
            NullLogger<ReleaseStock>.Instance, CancellationToken.None);

    [Fact]
    public async Task A_reservation_is_all_or_nothing()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);
        var jacket = products.Seed("JKT-1", 1_000m, 1);
        var orderId = Guid.NewGuid();

        await Reserve(orderId, new StockLine("TNT-1", 2), new StockLine("JKT-1", 3));

        var rejected = Assert.Single(publisher.OfType<StockReservationRejected>());
        Assert.Equal([new StockShortage("JKT-1", 3, 1)], rejected.Shortages);
        Assert.Equal((0, 0), (tent.Reserved, jacket.Reserved));
        Assert.Equal(ReservationStatus.Rejected, reservations.All.Single().Status);
    }

    [Fact]
    public async Task A_redelivered_request_repeats_the_answer_and_reserves_nothing_twice()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);
        var orderId = Guid.NewGuid();

        await Reserve(orderId, new StockLine("TNT-1", 2));
        await Reserve(orderId, new StockLine("TNT-1", 2));

        Assert.Equal(2, tent.Reserved);
        Assert.Equal(2, publisher.OfType<StockReserved>().Count());
    }

    [Fact]
    public async Task Duplicate_skus_in_one_request_are_merged()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);
        await Reserve(Guid.NewGuid(), new StockLine("TNT-1", 2), new StockLine("tnt-1", 1));
        Assert.Equal(3, tent.Reserved);
    }

    [Fact]
    public async Task An_unknown_or_malformed_sku_is_a_shortage_not_an_error()
    {
        products.Seed("TNT-1", 1_000m, 5);
        await Reserve(Guid.NewGuid(), new StockLine("TNT-1", 1), new StockLine("??", 1));
        var rejected = Assert.Single(publisher.OfType<StockReservationRejected>());
        Assert.Equal([new StockShortage("??", 1, 0)], rejected.Shortages);
    }

    [Fact]
    public async Task A_release_that_overtakes_its_reservation_voids_it()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);
        var orderId = Guid.NewGuid();

        await Release(orderId);                       // the order was cancelled first
        await Reserve(orderId, new StockLine("TNT-1", 2)); // the request arrives late

        Assert.Equal(0, tent.Reserved);
        Assert.Single(publisher.OfType<StockReservationRejected>());
    }

    [Fact]
    public async Task Releasing_twice_returns_the_units_once()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);
        var orderId = Guid.NewGuid();
        await Reserve(orderId, new StockLine("TNT-1", 2));

        await Release(orderId);
        await Release(orderId);

        Assert.Equal(0, tent.Reserved);
        Assert.Equal(ReservationStatus.Released, reservations.All.Single().Status);
    }
}

[Trait("Category", "Unit")]
public sealed class CatalogHandlerTests
{
    [Fact]
    public async Task A_refused_price_change_is_audited_as_rejected_rethrows_the_rule_and_changes_nothing()
    {
        var products = new FakeProducts();
        var product = products.Seed("TNT-1", 1_000m, 5);
        var audit = new FakeAudit();

        var code = await Rules.BrokenAsync(() => ChangeProductPriceHandler.Handle(
            new ChangeProductPrice("TNT-1", 5_000m, "typo"), products, audit, new FakeUnitOfWork(), FakeClock.At2026(), CancellationToken.None));

        Assert.Equal("PRICE_JUMP_TOO_LARGE", code);
        Assert.Equal(1_000m, product.Price.Amount);
        Assert.Equal(AuditOutcome.Rejected, Assert.Single(audit.Records).Outcome);
    }

    [Fact]
    public async Task An_accepted_price_change_is_audited_with_old_and_new_price()
    {
        var products = new FakeProducts();
        products.Seed("TNT-1", 1_000m, 5);
        var audit = new FakeAudit();

        var result = await ChangeProductPriceHandler.Handle(
            new ChangeProductPrice("TNT-1", 1_200m, "supplier price rise"), products, audit, new FakeUnitOfWork(),
            FakeClock.At2026(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var record = Assert.Single(audit.Records);
        Assert.Equal(("price-changed", "1000", "1200"), (record.Action, record.Metadata!["old_price"], record.Metadata["new_price"]));
    }

    [Fact]
    public async Task Listing_a_product_twice_is_an_already_exists_failure_not_a_rule()
    {
        var products = new FakeProducts();
        products.Seed("TNT-1", 1_000m, 5);

        var result = await ListProductHandler.Handle(
            new ListProduct("tnt-1", "Tent", "", "tents", "Northface Works", 1_000m, 1, 0), products, new FakeAudit(), new FakeUnitOfWork(), CancellationToken.None);

        Assert.Equal(("SKU_TAKEN", ErrorCategory.AlreadyExists), (result.FailureDescriptor!.Identity.Code, result.FailureDescriptor.Category));
    }

    [Fact]
    public async Task Sorting_by_an_unpublished_field_is_a_validation_failure()
    {
        var result = await BrowseProductsHandler.Handle(
            new BrowseProducts(null, null, Sort: "cost_price"), new ThrowingReadModel(), FakeActor.Anonymous(), CancellationToken.None);
        Assert.Equal(ErrorCategory.Validation, result.FailureDescriptor!.Category);
    }

    [Fact]
    public async Task A_malformed_sku_on_a_product_page_is_not_found_not_invalid()
    {
        var result = await GetProductStockHandler.Handle(new GetProductStock("??"), new ThrowingReadModel(), CancellationToken.None);
        Assert.Equal(ErrorCategory.NotFound, result.FailureDescriptor!.Category);
    }

    private sealed class ThrowingReadModel : ICatalogReadModel
    {
        public Task<Page<ProductSummary>> BrowseAsync(ProductFilter filter, PageRequest page, SortSpec sort, CancellationToken cancellationToken) => throw new InvalidOperationException("must not be reached");
        public Task<ProductDetails?> FindDetailsAsync(Sku sku, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<ProductStockView?> FindStockAsync(Sku sku, CancellationToken cancellationToken) => throw new InvalidOperationException("must not be reached");
        public Task<Page<RestockAlertView>> ListRestockAlertsAsync(PageRequest page, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
}

[Trait("Category", "Unit")]
public sealed class BasketHandlerTests
{
    private sealed class OneProductCatalog(ProductForSale? product) : ICatalogLookup
    {
        public Task<ProductForSale?> FindAsync(string sku, CancellationToken cancellationToken) => Task.FromResult(product);
    }

    [Fact]
    public async Task The_price_in_the_basket_comes_from_the_catalog()
    {
        var baskets = new FakeBaskets();
        var catalog = new OneProductCatalog(new ProductForSale("TNT-1", "Tent", 485.00m, "USD", 3, true));

        var result = await SetBasketItemHandler.Handle(
            new SetBasketItem("tnt-1", 2), FakeActor.User("sara", "customer"), baskets, catalog, new FakeUnitOfWork(), CancellationToken.None);

        Assert.Equal(970.00m, result.Value.Total);
        Assert.Equal(3, baskets.All.Single().Lines.Single().PriceVersion);
    }

    [Fact]
    public async Task A_product_that_is_not_for_sale_breaks_the_rule_and_no_basket_is_added()
    {
        var baskets = new FakeBaskets();
        var catalog = new OneProductCatalog(new ProductForSale("TNT-1", "Tent", 1_000m, "USD", 1, false));

        var code = await Rules.BrokenAsync(() => SetBasketItemHandler.Handle(
            new SetBasketItem("TNT-1", 1), FakeActor.User("sara", "customer"), baskets, catalog, new FakeUnitOfWork(), CancellationToken.None));

        Assert.Equal("PRODUCT_NOT_SELLABLE", code);
        Assert.Empty(baskets.All);
    }

    [Fact]
    public async Task A_caller_without_an_identity_has_no_basket()
    {
        var result = await SetBasketItemHandler.Handle(
            new SetBasketItem("TNT-1", 1), FakeActor.Anonymous(), new FakeBaskets(), new OneProductCatalog(null),
            new FakeUnitOfWork(), CancellationToken.None);
        Assert.Equal("BUYER_REQUIRED", result.FailureDescriptor!.Identity.Code);
    }

    private static (FakeBaskets Baskets, BasketAggregate Basket) BasketWithAnUnseenPriceChange()
    {
        var baskets = new FakeBaskets();
        var basket = BasketAggregate.Open("sara", "USD");
        basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 1);
        basket.ApplyPriceChange("TNT-1", 1_200m, 2);
        baskets.Add(basket);
        return (baskets, basket);
    }

    [Fact]
    public async Task Reading_the_basket_changes_nothing_however_often_it_is_read()
    {
        var (baskets, basket) = BasketWithAnUnseenPriceChange();
        var read = new FakeBasketReadModel(baskets);

        for (var i = 0; i < 3; i++)
        {
            var result = await GetMyBasketHandler.Handle(new GetMyBasket(), FakeActor.User("sara", "customer"), read, CancellationToken.None);
            Assert.True(result.Value.PricesChanged);
            Assert.Equal(1_000m, Assert.Single(result.Value.Lines).PreviousUnitPrice);
        }

        Assert.True(basket.HasUnseenPriceChanges);
    }

    [Fact]
    public async Task Acknowledging_the_prices_is_its_own_command_and_is_idempotent()
    {
        var (baskets, basket) = BasketWithAnUnseenPriceChange();

        for (var i = 0; i < 2; i++)
        {
            var result = await AcknowledgeBasketPricesHandler.Handle(
                new AcknowledgeBasketPrices(), FakeActor.User("sara", "customer"), baskets, new FakeUnitOfWork(), CancellationToken.None);
            Assert.False(result.Value.PricesChanged);
        }

        Assert.False(basket.HasUnseenPriceChanges);
        Assert.Equal(1_200m, basket.Lines.Single().UnitPrice);
    }

    [Fact]
    public async Task A_shopper_without_a_basket_reads_an_empty_one()
    {
        var result = await GetMyBasketHandler.Handle(
            new GetMyBasket(), FakeActor.User("reza", "customer"), new FakeBasketReadModel(new FakeBaskets()), CancellationToken.None);
        Assert.Empty(result.Value.Lines);
    }

    [Fact]
    public async Task A_catalog_price_change_reprices_every_basket_holding_the_product()
    {
        var baskets = new FakeBaskets();
        foreach (var buyer in new[] { "sara", "reza" })
        {
            var basket = BasketAggregate.Open(buyer, "USD");
            basket.SetQuantity("TNT-1", "Tent", 1_000m, 1, true, 1);
            baskets.Add(basket);
        }

        await ApplyCatalogPriceChangeHandler.Handle(new ApplyCatalogPriceChange("TNT-1", 1_100m, 2), baskets, new FakeUnitOfWork(), CancellationToken.None);

        Assert.All(baskets.All, b => Assert.Equal(1_100m, b.Lines.Single().UnitPrice));
    }
}

[Trait("Category", "Unit")]
public sealed class RestockHandlerTests
{
    private readonly FakeProducts products = new();
    private readonly FakeReceipts receipts = new();
    private readonly FakeAudit audit = new();

    private Task<Result<ProductStockView>> Restock(string sku, int quantity, string reference) =>
        RestockProductHandler.Handle(
            new RestockProduct(sku, quantity, reference), products, receipts, audit, new FakeUnitOfWork(), FakeClock.At2026(),
            CancellationToken.None);

    [Fact]
    public async Task A_delivery_adds_its_units_and_leaves_a_receipt()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);

        var result = await Restock("tnt-1", 10, "DN-2026-001");

        Assert.Equal(15, result.Value.OnHand);
        Assert.Equal(15, tent.OnHand);
        Assert.Equal(("TNT-1", "DN-2026-001", 10), Assert.Single(receipts.All) is var r ? (r.Sku, r.Reference, r.Quantity) : default);
        Assert.Equal("DN-2026-001", Assert.Single(audit.Records).Metadata!["reference"]);
    }

    [Fact]
    public async Task The_same_delivery_note_adds_the_stock_once_however_often_it_is_reported()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);

        await Restock("TNT-1", 10, "DN-2026-001");
        var repeated = await Restock("TNT-1", 10, " dn-2026-001 ");

        Assert.True(repeated.IsSuccess);
        Assert.Equal(15, repeated.Value.OnHand);
        Assert.Equal(15, tent.OnHand);
        Assert.Single(receipts.All);
        Assert.Single(audit.Records);
    }

    [Fact]
    public async Task The_same_delivery_note_with_another_quantity_breaks_the_rule_and_changes_nothing()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);
        await Restock("TNT-1", 10, "DN-2026-001");

        var code = await Rules.BrokenAsync(() => Restock("TNT-1", 12, "DN-2026-001"));

        Assert.Equal("DELIVERY_REFERENCE_REUSED", code);
        Assert.Equal(15, tent.OnHand);
    }

    [Fact]
    public async Task One_delivery_note_may_name_two_products()
    {
        var tent = products.Seed("TNT-1", 1_000m, 5);
        var stove = products.Seed("STV-1", 1_000m, 1);

        await Restock("TNT-1", 10, "DN-2026-001");
        await Restock("STV-1", 4, "DN-2026-001");

        Assert.Equal((15, 5), (tent.OnHand, stove.OnHand));
        Assert.Equal(2, receipts.All.Count);
    }

    [Fact]
    public async Task A_delivery_for_a_product_that_does_not_exist_leaves_no_receipt()
    {
        var result = await Restock("TNT-404", 10, "DN-2026-001");

        Assert.Equal("PRODUCT_NOT_FOUND", result.FailureDescriptor!.Identity.Code);
        Assert.Empty(receipts.All);
    }
}

[Trait("Category", "Unit")]
public sealed class CheckoutHandlerTests
{
    private readonly FakeBaskets baskets = new();
    private readonly FakePublisher publisher = new();
    private readonly FakeClock clock = FakeClock.At2026();
    private readonly FakePaymentIntents intents = new();
    private readonly Guid intentId;

    public CheckoutHandlerTests()
    {
        var intent = PaymentIntent.Create("sara", "tok_visa_ok", clock.UtcNow);
        intents.Add(intent);
        intentId = intent.Id;
    }

    private static CheckoutAddress Address() =>
        new("Sara Ahmadi", "+14155550123", "California", "San Francisco", "12 Harbour Street", "94103");

    private Task<Result<CheckoutAccepted>> CheckOut(decimal expectedTotal, FakeActor? actor = null, Guid? intent = null) =>
        CheckoutHandler.Handle(
            new Checkout(Address(), intent ?? intentId, expectedTotal), actor ?? FakeActor.User("sara", "customer"),
            baskets, new FakePaymentIntentLookup(intents, clock), publisher, new FakeUnitOfWork(), clock, CancellationToken.None);

    private BasketAggregate BasketWith(decimal price, int quantity)
    {
        var basket = BasketAggregate.Open("sara", "USD");
        basket.SetQuantity("TNT-1", "Tent", price, 1, true, quantity);
        baskets.Add(basket);
        return basket;
    }

    [Fact]
    public async Task Checkout_empties_the_basket_and_publishes_what_it_held()
    {
        var basket = BasketWith(1_000m, 2);

        var result = await CheckOut(2_000m);

        Assert.True(result.IsSuccess);
        Assert.Empty(basket.Lines);
        var message = Assert.Single(publisher.OfType<BasketCheckedOut>());
        Assert.Same(message, Assert.Single(publisher.Published));
        Assert.Equal((result.Value.OrderId, "sara", 2_000m, "USD"), (message.OrderId, message.BuyerId, message.Total, message.Currency));
        Assert.Equal([new CheckoutLine("TNT-1", "Tent", 1_000m, 2)], message.Lines);
        Assert.Equal(Address(), message.ShippingAddress);
        Assert.Equal(clock.UtcNow, message.CheckedOutOnUtc);
        Assert.Equal(intentId, message.PaymentIntentId);
    }

    [Fact]
    public async Task A_payment_intent_that_cannot_pay_is_refused_before_the_basket_is_touched()
    {
        var basket = BasketWith(1_000m, 2);
        var others = PaymentIntent.Create("reza", "tok_visa_ok", clock.UtcNow);
        intents.Add(others);

        foreach (var intent in new[] { Guid.NewGuid(), others.Id })
        {
            var result = await CheckOut(2_000m, intent: intent);
            Assert.Equal("PAYMENT_INTENT_UNUSABLE", result.FailureDescriptor!.Identity.Code);
        }

        clock.UtcNow += PaymentIntent.Lifetime;
        Assert.Equal("PAYMENT_INTENT_UNUSABLE", (await CheckOut(2_000m)).FailureDescriptor!.Identity.Code);
        Assert.Single(basket.Lines);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Checkout_writes_nothing_but_the_basket()
    {
        // The handler's parameters are everything it can reach. Orders, payments and stock are not among
        // them: it could not write another module's data if it wanted to. It may read one thing of Payments.
        var parameters = typeof(CheckoutHandler).GetMethod(nameof(CheckoutHandler.Handle))!.GetParameters()
            .Select(static p => p.ParameterType.Name).ToArray();

        Assert.Equal(
            ["Checkout", "ICurrentActorAccessor", "IBasketRepository", "IPaymentIntentLookup", "IMessagePublisher", "IUnitOfWork", "IClock", "CancellationToken"],
            parameters);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Checkout_refuses_when_the_basket_no_longer_costs_what_the_shopper_saw()
    {
        BasketWith(1_000m, 2);

        var result = await CheckOut(1_800m);

        // The basket was already taken in the change tracker. In the host, MP Core's rollback policy sees
        // this failure on top of pending changes and rolls the transaction back, so the basket survives;
        // scenario S2 shows it against the real database. Here: nothing was published.
        Assert.Equal(("storefront.basket", "BASKET_TOTAL_CHANGED"), (result.FailureDescriptor!.Identity.Domain, result.FailureDescriptor.Identity.Code));
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task An_empty_basket_cannot_be_checked_out()
    {
        Assert.Equal("BASKET_EMPTY", (await CheckOut(0m)).FailureDescriptor!.Identity.Code);

        BasketWith(1_000m, 1).TakeForCheckout();
        Assert.Equal("BASKET_EMPTY", (await CheckOut(0m)).FailureDescriptor!.Identity.Code);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task A_caller_without_an_identity_checks_nothing_out()
    {
        BasketWith(1_000m, 1);
        var result = await CheckOut(1_000m, FakeActor.Anonymous());
        Assert.Equal("BUYER_REQUIRED", result.FailureDescriptor!.Identity.Code);
    }
}

[Trait("Category", "Unit")]
public sealed class CheckoutProcessTests
{
    private readonly FakeOrders orders = new();
    private readonly FakePayments payments = new();
    private readonly FakePaymentIntents intents = new();
    private readonly FakePublisher publisher = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeClock clock = FakeClock.At2026();

    private BasketCheckedOut CheckedOut(string postalCode = "94103") => new(
        Guid.CreateVersion7(), "sara", "Sara Ahmadi",
        new CheckoutAddress("Sara Ahmadi", "+14155550123", "California", "San Francisco", "12 Harbour Street", postalCode),
        [new CheckoutLine("TNT-1", "Tent", 1_000m, 2)], 2_000m, "USD", Guid.Parse("11111111-1111-1111-1111-111111111111"), clock.UtcNow);

    private Task Place(BasketCheckedOut message) =>
        OrderProcessHandler.Handle(message, orders, publisher, unitOfWork, NullLogger<BasketCheckedOut>.Instance, CancellationToken.None);

    private Task Registered(Guid orderId) =>
        OrderProcessHandler.Handle(new PaymentRegistered(orderId), orders, publisher, unitOfWork,
            NullLogger<PaymentRegistered>.Instance, CancellationToken.None);

    [Fact]
    public async Task A_checkout_becomes_an_order_under_the_identity_the_basket_gave_it()
    {
        var message = CheckedOut();

        await Place(message);

        var order = Assert.Single(orders.All);
        Assert.Equal((message.OrderId, "sara", 2_000m, OrderStatus.Submitted), (order.Id.Value, order.BuyerId, order.Total, order.Status));
        Assert.Equal(message.CheckedOutOnUtc, order.PlacedOnUtc);
        Assert.Equal(new RegisterPayment(message.OrderId, 2_000m, "USD", message.PaymentIntentId, "sara"), Assert.Single(publisher.Published));
    }

    [Fact]
    public async Task A_redelivered_checkout_creates_no_second_order_and_asks_for_nothing_again()
    {
        var message = CheckedOut();

        await Place(message);
        await Place(message);

        Assert.Single(orders.All);
        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task An_address_the_basket_should_never_have_let_through_stops_the_message()
    {
        // Not a repair and not a silent skip: the broken rule throws, and the host's error policy parks
        // the message for an operator. CheckoutContractTests is what keeps this from happening.
        var code = await Rules.BrokenAsync(() => Place(CheckedOut(postalCode: "12")));

        Assert.Equal("POSTAL_CODE_INVALID", code);
        Assert.Empty(orders.All);
        Assert.Empty(publisher.Published);
    }

    private Task Register(RegisterPayment command) =>
        RegisterPaymentHandler.Handle(command, payments, intents, publisher, unitOfWork, clock,
            NullLogger<RegisterPayment>.Instance, CancellationToken.None);

    private PaymentIntent IntentOf(string buyer)
    {
        var intent = PaymentIntent.Create(buyer, "tok_visa_ok", clock.UtcNow);
        intents.Add(intent);
        return intent;
    }

    [Fact]
    public async Task The_charge_is_registered_once_with_the_intents_token_and_the_answer_is_repeated()
    {
        var intent = IntentOf("sara");
        var command = new RegisterPayment(Guid.NewGuid(), 2_000m, "USD", intent.Id, "sara");

        await Register(command);
        await Register(command);

        var payment = Assert.Single(payments.All);
        Assert.Equal((command.OrderId, PaymentStatus.Pending, 2_000m, "tok_visa_ok"), (payment.OrderId, payment.Status, payment.Amount, payment.PaymentToken));
        Assert.Equal(2, publisher.OfType<PaymentRegistered>().Count());
        Assert.Equal((null, command.OrderId), (intent.PaymentToken, intent.UsedByOrderId));
    }

    [Theory]
    [InlineData("used")]
    [InlineData("another shopper's")]
    [InlineData("expired")]
    [InlineData("unknown")]
    public async Task An_intent_that_cannot_pay_declines_the_payment_at_once_and_charges_nothing(string why)
    {
        var intent = IntentOf(why == "another shopper's" ? "reza" : "sara");
        if (why == "used")
        {
            intent.UseFor(Guid.NewGuid(), "sara", clock.UtcNow);
        }

        if (why == "expired")
        {
            clock.UtcNow += PaymentIntent.Lifetime;
        }

        var command = new RegisterPayment(Guid.NewGuid(), 2_000m, "USD", why == "unknown" ? Guid.NewGuid() : intent.Id, "sara");
        await Register(command);
        await Register(command);

        var payment = Assert.Single(payments.All);
        Assert.Equal((PaymentStatus.Declined, "PAYMENT_INTENT_UNUSABLE", (string?)null), (payment.Status, payment.DeclineCode, payment.PaymentToken));
        Assert.Equal(2, publisher.OfType<PaymentDeclined>().Count());
        Assert.Empty(publisher.OfType<PaymentRegistered>());
    }

    [Fact]
    public async Task Once_the_charge_is_registered_the_warehouse_is_asked_for_the_stock()
    {
        var message = CheckedOut();
        await Place(message);

        await Registered(message.OrderId);

        var request = Assert.Single(publisher.OfType<ReserveStock>());
        Assert.Equal((message.OrderId, new StockLine("TNT-1", 2)), (request.OrderId, Assert.Single(request.Lines)));
        Assert.Equal(OrderStatus.Submitted, orders.All.Single().Status);
    }

    [Fact]
    public async Task A_charge_registered_for_a_cancelled_order_is_voided_and_no_stock_is_asked_for()
    {
        var message = CheckedOut();
        await Place(message);
        orders.All.Single().Cancel(CancellationReasons.CustomerRequest, null, clock.UtcNow);

        await Registered(message.OrderId);

        Assert.Single(publisher.OfType<VoidPayment>());
        Assert.Empty(publisher.OfType<ReserveStock>());
    }

    [Fact]
    public async Task A_late_answer_from_payments_asks_for_no_stock_twice()
    {
        var message = CheckedOut();
        await Place(message);
        orders.All.Single().ConfirmStock(clock.UtcNow);

        await Registered(message.OrderId);

        Assert.Empty(publisher.OfType<ReserveStock>());
    }
}

[Trait("Category", "Unit")]
public sealed class OrderProcessTests
{
    private readonly FakeOrders orders = new();
    private readonly FakePublisher publisher = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeClock clock = FakeClock.At2026();

    private Order Seed(Action<Order>? advance = null)
    {
        var order = Orders.Placed();
        advance?.Invoke(order);
        orders.Add(order);
        return order;
    }

    [Fact]
    public async Task Stock_reserved_moves_the_order_on_and_asks_for_the_charge()
    {
        var order = Seed();
        await OrderProcessHandler.Handle(new StockReserved(order.Id.Value), orders, publisher, unitOfWork, clock,
            NullLogger<StockReserved>.Instance, CancellationToken.None);

        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        Assert.Single(publisher.OfType<AuthorizePayment>());
    }

    [Fact]
    public async Task Stock_reserved_for_a_cancelled_order_is_released_again()
    {
        var order = Seed(o => o.Cancel(CancellationReasons.CustomerRequest, null, FakeClock.At2026().UtcNow));
        await OrderProcessHandler.Handle(new StockReserved(order.Id.Value), orders, publisher, unitOfWork, clock,
            NullLogger<StockReserved>.Instance, CancellationToken.None);

        Assert.Single(publisher.OfType<ReleaseStock>());
        Assert.Empty(publisher.OfType<AuthorizePayment>());
    }

    [Fact]
    public async Task A_charge_approved_after_cancellation_is_refunded()
    {
        var order = Seed(o =>
        {
            o.ConfirmStock(FakeClock.At2026().UtcNow);
            o.Cancel(CancellationReasons.CustomerRequest, null, FakeClock.At2026().UtcNow);
        });

        await OrderProcessHandler.Handle(new PaymentAuthorized(order.Id.Value, "DMP-1"), orders, publisher, unitOfWork, clock,
            NullLogger<PaymentAuthorized>.Instance, CancellationToken.None);

        Assert.Single(publisher.OfType<RefundPayment>());
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task A_declined_card_cancels_the_order_and_releases_the_stock()
    {
        var order = Seed(o => o.ConfirmStock(FakeClock.At2026().UtcNow));
        await OrderProcessHandler.Handle(new PaymentDeclined(order.Id.Value, "INSUFFICIENT_FUNDS"), orders, publisher, unitOfWork, clock,
            NullLogger<PaymentDeclined>.Instance, CancellationToken.None);

        Assert.Equal((OrderStatus.Cancelled, CancellationReasons.PaymentDeclined), (order.Status, order.CancellationReason));
        Assert.Single(publisher.OfType<ReleaseStock>());
    }

    [Fact]
    public async Task A_duplicate_answer_is_ignored_and_breaks_no_rule()
    {
        var order = Seed();
        for (var i = 0; i < 2; i++)
        {
            await OrderProcessHandler.Handle(new StockReserved(order.Id.Value), orders, publisher, unitOfWork, clock,
                NullLogger<StockReserved>.Instance, CancellationToken.None);
        }

        Assert.Single(publisher.OfType<AuthorizePayment>());
        Assert.Equal(2, order.Version);
    }

    [Fact]
    public async Task A_stock_request_that_was_given_up_cancels_the_order_and_voids_both_sides()
    {
        var order = Seed();
        await OrderProcessHandler.Handle(new StockReservationAbandoned(order.Id.Value), orders, publisher, unitOfWork, clock,
            NullLogger<StockReservationAbandoned>.Instance, CancellationToken.None);

        Assert.Equal((OrderStatus.Cancelled, CancellationReasons.ReservationFailed), (order.Status, order.CancellationReason));
        Assert.Single(publisher.OfType<ReleaseStock>());
        Assert.Single(publisher.OfType<VoidPayment>());
        Assert.Empty(publisher.OfType<AuthorizePayment>());
    }

    [Fact]
    public async Task A_given_up_stock_request_stops_nothing_once_the_order_has_moved_on()
    {
        // The message was replayed from the error queue and answered before this arrived, or arrives twice.
        var moved = Seed(o => o.ConfirmStock(FakeClock.At2026().UtcNow));
        var cancelled = Seed(o => o.Cancel(CancellationReasons.CustomerRequest, null, FakeClock.At2026().UtcNow));

        foreach (var order in new[] { moved, cancelled })
        {
            await OrderProcessHandler.Handle(new StockReservationAbandoned(order.Id.Value), orders, publisher, unitOfWork, clock,
                NullLogger<StockReservationAbandoned>.Instance, CancellationToken.None);
        }

        Assert.Empty(publisher.Published);
        Assert.Equal(OrderStatus.AwaitingPayment, moved.Status);
        Assert.Equal(CancellationReasons.CustomerRequest, cancelled.CancellationReason);
    }

    [Fact]
    public void Giving_up_a_stock_request_is_an_answer_and_giving_up_anything_else_is_not()
    {
        var orderId = Guid.NewGuid();

        Assert.Equal(new StockReservationAbandoned(orderId), Storefront.Commerce.Api.Hosting.GivenUpMessages.AnswerFor(new ReserveStock(orderId, [])));
        Assert.Null(Storefront.Commerce.Api.Hosting.GivenUpMessages.AnswerFor(new StockReserved(orderId)));
        Assert.Null(Storefront.Commerce.Api.Hosting.GivenUpMessages.AnswerFor(null));
    }

    [Fact]
    public async Task A_rejection_for_an_already_cancelled_order_is_ignored()
    {
        var order = Seed(o => o.Cancel(CancellationReasons.CustomerRequest, null, FakeClock.At2026().UtcNow));
        await OrderProcessHandler.Handle(new StockReservationRejected(order.Id.Value, []), orders, publisher, unitOfWork, clock,
            NullLogger<StockReservationRejected>.Instance, CancellationToken.None);
        Assert.Empty(publisher.Published);
    }
}

[Trait("Category", "Unit")]
public sealed class OrderAccessTests
{
    [Fact]
    public async Task Another_shoppers_order_is_not_found_rather_than_forbidden()
    {
        var orders = new FakeOrders();
        var order = Orders.Placed(buyer: "sara");
        orders.Add(order);

        var result = await GetOrderHandler.Handle(new GetOrder(order.Id.Value), FakeActor.User("reza", "customer"), new FakeOrderReadModel(orders), CancellationToken.None);

        Assert.Equal(ErrorCategory.NotFound, result.FailureDescriptor!.Category);
    }

    [Fact]
    public async Task Support_must_say_why_it_cancels()
    {
        var orders = new FakeOrders();
        var order = Orders.Placed();
        orders.Add(order);

        var result = await CancelOrderHandler.Handle(
            new CancelOrder(order.Id.Value, null), FakeActor.User("ali", "support-agent"), orders, new FakePublisher(),
            new FakeAudit(), new FakeUnitOfWork(), FakeClock.At2026(), CancellationToken.None);

        Assert.Equal(ErrorCategory.Validation, result.FailureDescriptor!.Category);
        Assert.Equal(OrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task Cancelling_a_paid_order_asks_for_a_refund_and_returns_the_stock()
    {
        var orders = new FakeOrders();
        var order = Orders.Placed();
        order.ConfirmStock(FakeClock.At2026().UtcNow);
        order.MarkPaid("DMP-1", FakeClock.At2026().UtcNow);
        orders.Add(order);
        var publisher = new FakePublisher();

        var result = await CancelOrderHandler.Handle(
            new CancelOrder(order.Id.Value, "changed my mind"), FakeActor.User("sara", "customer"), orders, publisher,
            new FakeAudit(), new FakeUnitOfWork(), FakeClock.At2026(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(publisher.OfType<RefundPayment>());
        Assert.Single(publisher.OfType<ReleaseStock>());
        Assert.Empty(publisher.OfType<VoidPayment>());
    }

    [Fact]
    public async Task Cancelling_a_shipped_order_breaks_the_rule_and_the_attempt_is_audited()
    {
        var orders = new FakeOrders();
        var order = Orders.Placed();
        var now = FakeClock.At2026().UtcNow;
        order.ConfirmStock(now);
        order.MarkPaid("DMP-1", now);
        order.Ship("Post", "PST-1", now);
        orders.Add(order);
        var audit = new FakeAudit();
        var publisher = new FakePublisher();

        var code = await Rules.BrokenAsync(() => CancelOrderHandler.Handle(
            new CancelOrder(order.Id.Value, "too late"), FakeActor.User("sara", "customer"), orders, publisher,
            audit, new FakeUnitOfWork(), FakeClock.At2026(), CancellationToken.None));

        Assert.Equal("ALREADY_SHIPPED", code);
        Assert.Equal(AuditOutcome.Rejected, Assert.Single(audit.Records).Outcome);
        Assert.Empty(publisher.Published);
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }
}

[Trait("Category", "Unit")]
public sealed class PaymentHandlerTests
{
    private static async Task<(FakePayments, FakePublisher)> Authorize(GatewayResult answer)
    {
        var payments = new FakePayments();
        var orderId = Guid.NewGuid();
        payments.Add(Payment.Register(orderId, 1_000m, "USD", "tok_visa_ok", FakeClock.At2026().UtcNow));
        var publisher = new FakePublisher();
        await AuthorizePaymentHandler.Handle(new AuthorizePayment(orderId), payments, new FakeGateway(answer), publisher,
            new FakeAudit(), new FakeUnitOfWork(), FakeClock.At2026(), NullLogger<AuthorizePayment>.Instance, CancellationToken.None);
        return (payments, publisher);
    }

    [Fact]
    public async Task An_approved_charge_is_recorded_and_announced()
    {
        var (payments, publisher) = await Authorize(GatewayResult.Approved("DMP-1"));
        Assert.Equal(PaymentStatus.Authorized, payments.All.Single().Status);
        Assert.Equal("DMP-1", Assert.Single(publisher.OfType<PaymentAuthorized>()).ProviderReference);
    }

    [Fact]
    public async Task A_decline_is_a_business_answer_not_a_retry()
    {
        var (payments, publisher) = await Authorize(GatewayResult.Declined("INSUFFICIENT_FUNDS"));
        Assert.Equal(PaymentStatus.Declined, payments.All.Single().Status);
        Assert.Single(publisher.OfType<PaymentDeclined>());
    }

    [Fact]
    public async Task An_unavailable_provider_raises_a_retryable_failure_and_changes_nothing()
    {
        var failure = await Assert.ThrowsAsync<ResultFailureException>(() => Authorize(GatewayResult.Unavailable));
        Assert.True(failure.Failure.Retry.IsRetryable);
        Assert.Equal(ErrorCategory.DependencyUnavailable, failure.Failure.Category);
    }

    [Fact]
    public async Task The_order_id_is_the_idempotency_key_sent_to_the_provider()
    {
        var payments = new FakePayments();
        var orderId = Guid.NewGuid();
        payments.Add(Payment.Register(orderId, 1_000m, "USD", "tok_visa_ok", FakeClock.At2026().UtcNow));
        var gateway = new FakeGateway(GatewayResult.Approved("DMP-1"));

        await AuthorizePaymentHandler.Handle(new AuthorizePayment(orderId), payments, gateway, new FakePublisher(),
            new FakeAudit(), new FakeUnitOfWork(), FakeClock.At2026(), NullLogger<AuthorizePayment>.Instance, CancellationToken.None);

        Assert.Equal([orderId.ToString()], gateway.IdempotencyKeys);
    }

    [Fact]
    public async Task Voiding_a_settled_payment_is_ignored_not_a_broken_rule()
    {
        var payments = new FakePayments();
        var orderId = Guid.NewGuid();
        var payment = Payment.Register(orderId, 1_000m, "USD", "tok_visa_ok", FakeClock.At2026().UtcNow);
        payment.MarkAuthorized("DMP-1", FakeClock.At2026().UtcNow);
        payments.Add(payment);

        await VoidPaymentHandler.Handle(new VoidPayment(orderId), payments, new FakeUnitOfWork(), FakeClock.At2026(), CancellationToken.None);

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }
}
