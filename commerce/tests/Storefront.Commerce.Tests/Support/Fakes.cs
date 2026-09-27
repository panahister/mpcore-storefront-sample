using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Basket.Application.Views;
using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Domain;
using MPCore.Application.Querying;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Domain.Model;
using MPCore.Domain.Rules;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;
using MPCore.Security;
using BasketAggregate = Storefront.Commerce.Modules.Basket.Domain.Basket;

namespace Storefront.Commerce.Tests.Support;

// Hand-written fakes for the ports. Every handler in this repository takes its collaborators as
// parameters, so a business rule is tested in microseconds with no container, no database and no mock
// library.

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public static FakeClock At2026() => new(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero));
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("A handler must not call SaveChangesAsync; MP Core's transaction middleware saves.");
}

public sealed class FakePublisher : IMessagePublisher
{
    public List<object> Published { get; } = [];

    public ValueTask PublishAsync(object message, CancellationToken cancellationToken = default)
    {
        Published.Add(message);
        return ValueTask.CompletedTask;
    }

    public IEnumerable<T> OfType<T>() => Published.OfType<T>();
}

public sealed class FakeActor(CurrentActor current) : ICurrentActorAccessor
{
    public CurrentActor Current { get; } = current;

    public static FakeActor User(string subject, params string[] roles)
    {
        var builder = new CurrentActorBuilder(ActorKind.User) { SubjectId = subject, UserName = subject };
        foreach (var role in roles)
        {
            builder.AddRole(role);
        }

        return new FakeActor(builder.Build());
    }

    public static FakeActor Anonymous() => new(CurrentActor.Anonymous);
}

public sealed class FakeAudit : IBusinessAuditRecorder
{
    public List<(string Module, string Action, AuditOutcome Outcome, IReadOnlyDictionary<string, string>? Metadata)> Records { get; } = [];

    public ValueTask RecordAsync(string module, string action, string? entityType = null, string? entityId = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        Records.Add((module, action, AuditOutcome.Succeeded, metadata));
        return ValueTask.CompletedTask;
    }

    public ValueTask RecordAttemptAsync(string module, string action, AuditOutcome outcome, AuditFailure? failure = null,
        string? reason = null, string? entityType = null, string? entityId = null,
        IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        Records.Add((module, action, outcome, metadata));
        return ValueTask.CompletedTask;
    }
}

public class InMemoryStore<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    protected Dictionary<TId, TAggregate> Items { get; } = [];

    public IReadOnlyCollection<TAggregate> All => Items.Values;

    public Task<TAggregate?> GetAsync(TId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.GetValueOrDefault(id));

    public void Add(TAggregate aggregate) => Items[aggregate.Id] = aggregate;

    public void Remove(TAggregate aggregate) => Items.Remove(aggregate.Id);
}

public sealed class FakeProducts : InMemoryStore<Product, ProductId>, IProductRepository
{
    public Task<Product?> FindBySkuAsync(Sku sku, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Values.FirstOrDefault(p => p.Sku == sku));

    public Task<IReadOnlyList<Product>> FindBySkusAsync(IReadOnlyCollection<Sku> skus, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Product>>([.. Items.Values.Where(p => skus.Contains(p.Sku))]);

    public Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Values.Any(p => p.Sku == sku));

    public Product Seed(string sku, decimal price, int stock, int threshold = 0)
    {
        var product = Product.List(ProductId.New(), Sku.Parse(sku), sku + " name", "", "tents", "Northface Works", Price.Of(price), stock, threshold);
        product.ClearEvents();
        Add(product);
        return product;
    }
}

public sealed class FakeReservations : InMemoryStore<StockReservation, Guid>, IStockReservationRepository;

public sealed class FakeReceipts : InMemoryStore<StockReceipt, Guid>, IStockReceiptRepository
{
    public Task<StockReceipt?> FindAsync(Sku sku, string reference, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Values.FirstOrDefault(r => r.Sku == sku.Value && r.Reference == reference));
}

public sealed class FakeBaskets : InMemoryStore<BasketAggregate, string>, IBasketRepository
{
    public Task<IReadOnlyList<BasketAggregate>> FindContainingAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BasketAggregate>>([.. Items.Values.Where(b => b.Lines.Any(l => l.Sku == sku))]);
}

public sealed class FakeOrders : InMemoryStore<Order, OrderId>, IOrderRepository;

/// <summary>The read side over the same in-memory orders: views out, never the aggregate.</summary>
public sealed class FakeOrderReadModel(FakeOrders orders) : IOrderReadModel
{
    public async Task<OrderOfBuyer?> FindAsync(OrderId id, CancellationToken cancellationToken) =>
        await orders.GetAsync(id, cancellationToken) is { } order ? new OrderOfBuyer(order.BuyerId, OrderViews.Of(order)) : null;

    public Task<Page<OrderSummary>> ListForBuyerAsync(string buyerId, PageRequest page, SortSpec sort, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Page<OrderSummary>> ListAsync(OrderStatus? status, PageRequest page, SortSpec sort, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>The read side over the same in-memory baskets.</summary>
public sealed class FakeBasketReadModel(FakeBaskets baskets) : IBasketReadModel
{
    public async Task<BasketView?> FindForBuyerAsync(string buyerId, CancellationToken cancellationToken) =>
        await baskets.GetAsync(buyerId, cancellationToken) is { } basket ? BasketViews.Of(basket) : null;
}

public sealed class FakePayments : InMemoryStore<Payment, Guid>, IPaymentRepository;

public sealed class FakePaymentIntents : InMemoryStore<PaymentIntent, Guid>, IPaymentIntentRepository;

/// <summary>The published lookup over the same in-memory intents.</summary>
public sealed class FakePaymentIntentLookup(FakePaymentIntents intents, FakeClock clock) : Storefront.Commerce.Modules.Payments.Contracts.IPaymentIntentLookup
{
    public async Task<bool> IsUsableAsync(Guid paymentIntentId, string buyerId, CancellationToken cancellationToken) =>
        await intents.GetAsync(paymentIntentId, cancellationToken) is { } intent && intent.IsUsableBy(buyerId, clock.UtcNow);
}

public sealed class FakeGateway(GatewayResult answer) : IPaymentGateway
{
    public GatewayResult Answer { get; set; } = answer;

    public List<string> IdempotencyKeys { get; } = [];

    public Task<GatewayResult> AuthorizeAsync(string idempotencyKey, decimal amount, string currency, string paymentToken, CancellationToken cancellationToken)
    {
        IdempotencyKeys.Add(idempotencyKey);
        return Task.FromResult(Answer);
    }

    public Task<GatewayResult> RefundAsync(string idempotencyKey, string providerReference, decimal amount, CancellationToken cancellationToken)
    {
        IdempotencyKeys.Add(idempotencyKey);
        return Task.FromResult(GatewayResult.Approved("SPR-TEST"));
    }
}

public static class Orders
{
    public static ShippingAddress Home() =>
        new("Sara Ahmadi", PhoneNumber.Parse("+14155550123"), "California", "San Francisco", "12 Harbour Street", PostalCode.Parse("94103"));

    public static Order Placed(string buyer = "sara", decimal price = 485.00m, int quantity = 1)
    {
        var order = Order.Place(
            OrderId.New(), buyer, buyer, Home(), "USD",
            [new OrderLine("TNT-ALV-2P", "Ridgeline 2-person tent", price, quantity)], FakeClock.At2026().UtcNow);
        order.ClearEvents();
        return order;
    }
}

public static class Rules
{
    /// <summary>The code of the business rule the action breaks; fails the test when no rule breaks.</summary>
    public static string Broken(Action action) => Assert.Throws<BusinessRuleValidationException>(action).Rule.Code;

    public static async Task<string> BrokenAsync(Func<Task> action) =>
        (await Assert.ThrowsAsync<BusinessRuleValidationException>(action)).Rule.Code;
}
