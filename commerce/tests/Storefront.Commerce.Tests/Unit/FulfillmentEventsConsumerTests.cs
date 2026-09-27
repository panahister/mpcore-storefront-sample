using Storefront.Commerce.Api.Hosting;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Tests.Support;
using MPCore.Application.Results;

namespace Storefront.Commerce.Tests.Unit;

/// <summary>
/// The seam between the warehouse service and the Ordering module: what the host does with the warehouse's
/// word that a parcel has left.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FulfillmentEventsConsumerTests
{
    private readonly FakeOrders orders = new();
    private readonly FakePublisher publisher = new();
    private readonly FakeAudit audit = new();

    private Order Paid()
    {
        var order = Orders.Placed();
        var now = FakeClock.At2026().UtcNow;
        order.ConfirmStock(now);
        order.MarkPaid("DMP-1", now);
        order.ClearEvents();
        orders.Add(order);
        return order;
    }

    private Task Consume(Guid orderId, string carrier = "Parcelway", string trackingCode = "PCW-1") =>
        FulfillmentEventsConsumer.Handle(
            new ShipmentDispatched(Guid.NewGuid(), orderId, "ORD-1", carrier, trackingCode, FakeClock.At2026().UtcNow),
            orders, publisher, audit, new FakeUnitOfWork(), FakeClock.At2026(), CancellationToken.None);

    [Fact]
    public async Task The_warehouses_word_ships_the_paid_order()
    {
        var order = Paid();

        await Consume(order.Id.Value);

        Assert.Equal((OrderStatus.Shipped, "Parcelway", "PCW-1"), (order.Status, order.Carrier, order.TrackingCode));
        Assert.Single(publisher.OfType<CommitStock>());
        Assert.Single(audit.Records);
    }

    [Fact]
    public async Task News_about_an_order_that_has_already_left_changes_nothing()
    {
        var order = Paid();
        order.Ship("Counter", "CTR-7", FakeClock.At2026().UtcNow);

        await Consume(order.Id.Value);

        Assert.Equal((OrderStatus.Shipped, "Counter", "CTR-7"), (order.Status, order.Carrier, order.TrackingCode));
        Assert.Empty(publisher.Published);
        Assert.Empty(audit.Records);
    }

    [Fact]
    public async Task An_order_nobody_knows_is_a_failure_the_error_policy_decides_about()
    {
        var thrown = await Assert.ThrowsAsync<ResultFailureException>(() => Consume(Guid.NewGuid()));

        Assert.Equal(ErrorCategory.NotFound, thrown.Failure.Category);
    }
}
