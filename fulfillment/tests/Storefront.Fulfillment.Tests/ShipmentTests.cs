using Microsoft.Extensions.Logging.Abstractions;
using Storefront.Fulfillment.Application.Commands;
using Storefront.Fulfillment.Application.Contracts;
using Storefront.Fulfillment.Application.Events;
using Storefront.Fulfillment.Application.Validators;
using Storefront.Fulfillment.Domain;
using Storefront.Fulfillment.Domain.Events;

namespace Storefront.Fulfillment.Tests;

[Trait("Category", "Unit")]
public sealed class ShipmentTests
{
    private static readonly DateTimeOffset Now = FakeClock.At2026().UtcNow;

    private static DeliveryAddress Address() => new("Sara Ahmadi", "+14155550101", "California", "San Francisco", "12 Market St.", "94103");

    private static Shipment Pending() =>
        Shipment.Receive(Guid.NewGuid(), "ORD-260927-ABC123", Address(), [new ShipmentLine("TNT-1", "Tent", 2)], Now);

    [Fact]
    public void A_received_order_waits_in_the_warehouse_under_the_orders_identity()
    {
        var orderId = Guid.NewGuid();
        var shipment = Shipment.Receive(orderId, "ORD-1", Address(), [new ShipmentLine("TNT-1", "Tent", 2), new ShipmentLine("STV-1", "Stove", 1)], Now);

        Assert.Equal((orderId, ShipmentStatus.Pending, 3), (shipment.Id, shipment.Status, shipment.ItemCount));
        Assert.Empty(shipment.IntegrationEvents);
    }

    [Fact]
    public void Dispatching_records_the_carrier_and_announces_it_once()
    {
        var shipment = Pending();

        shipment.Dispatch("Parcel Express", "PX-1001", Now);

        Assert.Equal((ShipmentStatus.Dispatched, "Parcel Express", "PX-1001"), (shipment.Status, shipment.Carrier, shipment.TrackingCode));
        var announced = Assert.IsType<ShipmentDispatched>(Assert.Single(shipment.IntegrationEvents));
        Assert.Equal((shipment.Id, "PX-1001", ShipmentDispatched.Name), (announced.OrderId, announced.TrackingCode, announced.EventName));
    }

    [Fact]
    public void A_parcel_leaves_once()
    {
        var shipment = Pending();
        shipment.Dispatch("Parcel Express", "PX-1001", Now);

        Assert.Equal("SHIPMENT_NOT_PENDING", Rules.Broken(() => shipment.Dispatch("Another", "X-1", Now)));
        Assert.Equal("PX-1001", shipment.TrackingCode);
    }

    [Fact]
    public void There_is_nothing_to_ship_without_a_line() =>
        Assert.Equal("SHIPMENT_EMPTY", Rules.Broken(() => Shipment.Receive(Guid.NewGuid(), "ORD-1", Address(), [], Now)));

    [Fact]
    public void Two_addresses_with_the_same_parts_are_the_same_address() =>
        Assert.Equal(Address(), new DeliveryAddress(" Sara Ahmadi ", "+14155550101", "California", "San Francisco", "12 Market St.", "94103"));
}

[Trait("Category", "Unit")]
public sealed class ShipmentHandlerTests
{
    private readonly FakeShipments shipments = new();
    private readonly FakeAudit audit = new();
    private readonly FakeClock clock = FakeClock.At2026();

    private OrderReadyToShip Announcement(Guid? orderId = null, Guid? eventId = null) => new(
        eventId ?? Guid.CreateVersion7(), orderId ?? Guid.NewGuid(), "ORD-260927-ABC123",
        new OrderReadyToShipAddress("Sara Ahmadi", "+14155550101", "California", "San Francisco", "12 Market St.", "94103"),
        [new OrderReadyToShipLine("TNT-1", "Tent", 2)], clock.UtcNow);

    private Task Receive(OrderReadyToShip message) =>
        OrderReadyToShipHandler.Handle(message, shipments, new FakeUnitOfWork(), clock, NullLogger<OrderReadyToShip>.Instance, CancellationToken.None);

    [Fact]
    public async Task An_order_that_is_ready_becomes_a_pending_shipment_with_what_the_shop_said()
    {
        var message = Announcement();

        await Receive(message);

        var shipment = Assert.Single(shipments.All);
        Assert.Equal((message.OrderId, "San Francisco", ShipmentStatus.Pending), (shipment.Id, shipment.Address.City, shipment.Status));
        Assert.Equal(new ShipmentLine("TNT-1", "Tent", 2), Assert.Single(shipment.Lines));
    }

    [Fact]
    public async Task The_same_order_announced_in_a_second_event_adds_nothing()
    {
        // The inbox stops the same event; this is the same order in another event, which only the
        // warehouse can recognise.
        var orderId = Guid.NewGuid();

        await Receive(Announcement(orderId));
        await Receive(Announcement(orderId));

        Assert.Single(shipments.All);
    }

    [Fact]
    public async Task Dispatching_answers_the_shipment_and_is_audited()
    {
        var message = Announcement();
        await Receive(message);

        var result = await DispatchShipmentHandler.Handle(
            new DispatchShipment(message.OrderId, " Parcel Express ", " PX-1001 "), shipments, audit, new FakeUnitOfWork(), clock, CancellationToken.None);

        Assert.Equal(("Dispatched", "Parcel Express", "PX-1001"), (result.Value.Status, result.Value.Carrier, result.Value.TrackingCode));
        Assert.Equal("shipment-dispatched", Assert.Single(audit.Records).Action);
    }

    [Fact]
    public async Task Dispatching_an_order_the_warehouse_never_heard_of_is_not_found()
    {
        var result = await DispatchShipmentHandler.Handle(
            new DispatchShipment(Guid.NewGuid(), "Parcel Express", "PX-1"), shipments, audit, new FakeUnitOfWork(), clock, CancellationToken.None);

        Assert.Equal("SHIPMENT_NOT_FOUND", result.FailureDescriptor!.Identity.Code);
        Assert.Empty(audit.Records);
    }

    [Fact]
    public async Task Dispatching_twice_breaks_the_rule()
    {
        var message = Announcement();
        await Receive(message);
        var dispatch = new DispatchShipment(message.OrderId, "Parcel Express", "PX-1001");
        await DispatchShipmentHandler.Handle(dispatch, shipments, audit, new FakeUnitOfWork(), clock, CancellationToken.None);

        var code = await Rules.BrokenAsync(() =>
            DispatchShipmentHandler.Handle(dispatch, shipments, audit, new FakeUnitOfWork(), clock, CancellationToken.None));

        Assert.Equal("SHIPMENT_NOT_PENDING", code);
    }

    [Fact]
    public void A_dispatch_names_its_carrier_and_its_tracking_code()
    {
        var failures = new DispatchShipmentValidator().Validate(new DispatchShipment(Guid.Empty, "", new string('x', 61))).Errors;
        Assert.Equal(["Carrier", "OrderId", "TrackingCode"], failures.Select(static f => f.PropertyName).Order());
    }

    [Fact]
    public void A_message_about_a_delivery_never_prints_the_recipient()
    {
        var printed = Announcement().ToString();
        Assert.DoesNotContain("Sara", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("+1415", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("Market", printed, StringComparison.Ordinal);
    }
}
