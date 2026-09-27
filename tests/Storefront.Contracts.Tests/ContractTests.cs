using System.Text.Json;
using MPCore.Domain.Events;
using Published = Storefront.Commerce.Modules.Ordering.Domain.Events;
using Shipped = Storefront.Fulfillment.Domain.Events;
using ToAnalytics = Storefront.Analytics.Application.Contracts;
using ToFulfillment = Storefront.Fulfillment.Application.Contracts;

namespace Storefront.Contracts.Tests;

/// <summary>
/// The three backends share no assembly. Each declares the messages it reads in its own code, and what
/// holds a publisher and its readers together is a name, a version and the JSON between them. These tests
/// write each message as its publisher does and read it as each reader does.
/// </summary>
/// <remarks>
/// Ian Robinson described the idea as <i>consumer-driven contracts</i>: a reader states what it needs, and
/// the publisher is tested against that. Between services in separate repositories the same test is a
/// Pact; here, where the services live side by side, it is a unit test, and it runs with every build.
/// </remarks>
[Trait("Category", "Contract")]
public sealed class ContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Order = Guid.Parse("01a0e320-df7d-75de-9063-1b0170013226");

    public static TheoryData<string> Serializers() => ["default", "web"];

    private static JsonSerializerOptions Options(string name) =>
        name == "web" ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : new JsonSerializerOptions();

    private static TReader Read<TReader>(object published, string serializer)
    {
        var json = JsonSerializer.Serialize(published, published.GetType(), Options(serializer));
        return JsonSerializer.Deserialize<TReader>(json, Options(serializer))!;
    }

    private static void SameContract(IIntegrationEvent published, IIntegrationEvent read)
    {
        Assert.Equal((published.EventName, published.EventVersion), (read.EventName, read.EventVersion));
        Assert.Equal((published.EventId, published.OccurredOnUtc), (read.EventId, read.OccurredOnUtc));
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void The_warehouse_reads_an_order_that_is_ready_to_ship(string serializer)
    {
        var published = new Published.OrderReadyToShip(
            Order, "ORD-260927-013226", 3,
            new Published.OrderReadyToShipAddress("Sara Ahmadi", "+14155550101", "California", "San Francisco", "12 Market St.", "94103"),
            [new Published.OrderReadyToShipLine("TNT-ALV-2P", "Two-person tent", 1), new Published.OrderReadyToShipLine("CKG-SHL-STV", "Stove", 2)],
            Now);

        var read = Read<ToFulfillment.OrderReadyToShip>(published, serializer);

        SameContract(published, read);
        Assert.Equal((Order, "ORD-260927-013226"), (read.OrderId, read.OrderNumber));
        Assert.Equal(
            new ToFulfillment.OrderReadyToShipAddress("Sara Ahmadi", "+14155550101", "California", "San Francisco", "12 Market St.", "94103"),
            read.ShipTo);
        Assert.Equal(
            [new ToFulfillment.OrderReadyToShipLine("TNT-ALV-2P", "Two-person tent", 1), new ToFulfillment.OrderReadyToShipLine("CKG-SHL-STV", "Stove", 2)],
            read.Lines);
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void The_shop_reads_the_warehouses_word_that_a_parcel_has_left(string serializer)
    {
        var published = new Shipped.ShipmentDispatched(Order, "ORD-260927-013226", "Parcel Express", "PX-1001", Now);

        var read = Read<Storefront.Commerce.Api.Hosting.ShipmentDispatched>(published, serializer);

        SameContract(published, read);
        Assert.Equal((Order, "Parcel Express", "PX-1001"), (read.OrderId, read.Carrier, read.TrackingCode));
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void The_figures_read_the_three_order_events(string serializer)
    {
        var placed = new Published.OrderPlaced(
            Order, "ORD-1", 1, "sara", "USD", 300m,
            [new Published.OrderPlacedLine("TNT-1", 2, 100m), new Published.OrderPlacedLine("STV-1", 1, 100m)], "California", "San Francisco", Now);
        var paid = new Published.OrderPaid(Order, "ORD-1", 3, 300m, "USD", "DP-1", Now);
        var cancelled = new Published.OrderCancelled(Order, "ORD-1", 4, "CUSTOMER_REQUEST", true, Now);

        var readPlaced = Read<ToAnalytics.OrderPlaced>(placed, serializer);
        var readPaid = Read<ToAnalytics.OrderPaid>(paid, serializer);
        var readCancelled = Read<ToAnalytics.OrderCancelled>(cancelled, serializer);

        SameContract(placed, readPlaced);
        SameContract(paid, readPaid);
        SameContract(cancelled, readCancelled);
        Assert.Equal((Order, 300m, "USD", "California", "San Francisco", 3),
            (readPlaced.OrderId, readPlaced.Total, readPlaced.Currency, readPlaced.Province, readPlaced.City, readPlaced.Lines.Sum(static l => l.Quantity)));
        Assert.Equal((Order, 300m, "USD"), (readPaid.OrderId, readPaid.Total, readPaid.Currency));
        Assert.Equal((Order, "CUSTOMER_REQUEST"), (readCancelled.OrderId, readCancelled.Reason));
    }

    [Fact]
    public void A_publisher_and_its_readers_name_the_same_channel()
    {
        Assert.Equal(Storefront.Commerce.Api.Hosting.CommerceQueues.OrdersReadyToShip, Storefront.Fulfillment.Api.Hosting.FulfillmentQueues.OrdersReadyToShip);
        Assert.Equal(Storefront.Commerce.Api.Hosting.CommerceQueues.ShipmentsDispatched, Storefront.Fulfillment.Api.Hosting.FulfillmentQueues.ShipmentsDispatched);
        Assert.Equal(Storefront.Commerce.Api.Hosting.CommerceTopics.OrderPlaced, Storefront.Analytics.Api.Hosting.AnalyticsTopics.OrderPlaced);
        Assert.Equal(Storefront.Commerce.Api.Hosting.CommerceTopics.OrderPaid, Storefront.Analytics.Api.Hosting.AnalyticsTopics.OrderPaid);
        Assert.Equal(Storefront.Commerce.Api.Hosting.CommerceTopics.OrderCancelled, Storefront.Analytics.Api.Hosting.AnalyticsTopics.OrderCancelled);
    }

    [Fact]
    public void A_channel_carries_the_contract_its_name_says()
    {
        // One channel per contract, named after the contract and its version.
        Assert.EndsWith("order-placed.v1", Storefront.Commerce.Api.Hosting.CommerceTopics.OrderPlaced, StringComparison.Ordinal);
        Assert.Equal(Published.OrderPlaced.Name + ".v1", Storefront.Commerce.Api.Hosting.CommerceTopics.OrderPlaced);
        Assert.Equal(Published.OrderPaid.Name + ".v1", Storefront.Commerce.Api.Hosting.CommerceTopics.OrderPaid);
        Assert.Equal(Published.OrderCancelled.Name + ".v1", Storefront.Commerce.Api.Hosting.CommerceTopics.OrderCancelled);
        Assert.Equal((Published.OrderReadyToShip.Name, Shipped.ShipmentDispatched.Name),
            (ToFulfillment.OrderReadyToShip.Name, Storefront.Commerce.Api.Hosting.ShipmentDispatched.Name));
    }
}
