using Storefront.Commerce.Infrastructure.Persistence;
using Storefront.Commerce.Modules.Basket.Domain;
using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MPCore.Domain.Events;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;

namespace Storefront.Commerce.Tests.Component;

/// <summary>
/// The real Entity Framework model, built against the real PostgreSQL provider with no connection.
/// </summary>
[Trait("Category", "Component")]
public sealed class PersistenceModelTests
{
    private static AppDbContext Context()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        PostgreSqlDbContextOptions.Apply(options, "Host=model-only;Database=none;Username=none;Password=none");
        return new AppDbContext(options.Options, TimeProvider.System, NullAggregateEventSink.Instance);
    }

    [Theory]
    [InlineData(typeof(Product), "catalog", "products")]
    [InlineData(typeof(StockReservation), "catalog", "stock_reservations")]
    [InlineData(typeof(RestockAlert), "catalog", "restock_alerts")]
    [InlineData(typeof(StockReceipt), "catalog", "stock_receipts")]
    [InlineData(typeof(Basket), "basket", "baskets")]
    [InlineData(typeof(Order), "ordering", "orders")]
    [InlineData(typeof(Payment), "payments", "payments")]
    [InlineData(typeof(PaymentIntent), "payments", "payment_intents")]
    public void Each_module_owns_its_own_schema(Type aggregate, string schema, string table)
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(aggregate)!;
        Assert.Equal((schema, table), (entity.GetSchema(), entity.GetTableName()));
    }

    [Theory]
    [InlineData(typeof(Product))]
    [InlineData(typeof(Basket))]
    [InlineData(typeof(Order))]
    [InlineData(typeof(Payment))]
    [InlineData(typeof(StockReservation))]
    [InlineData(typeof(PaymentIntent))]
    public void Every_contended_aggregate_uses_optimistic_concurrency(Type aggregate)
    {
        using var context = Context();
        var token = context.Model.FindEntityType(aggregate)!.GetProperties().Single(static p => p.IsConcurrencyToken);
        Assert.Equal("xmin", token.GetColumnName());
    }

    [Fact]
    public void A_sku_and_an_order_number_are_unique()
    {
        using var context = Context();
        Assert.Contains(context.Model.FindEntityType(typeof(Product))!.GetIndexes(), static i => i.IsUnique && i.GetDatabaseName() == "ux_products_sku");
        Assert.Contains(context.Model.FindEntityType(typeof(Order))!.GetIndexes(), static i => i.IsUnique && i.GetDatabaseName() == "ux_orders_number");
    }

    [Fact]
    public void A_delivery_note_is_received_once_per_product()
    {
        using var context = Context();
        var index = Assert.Single(context.Model.FindEntityType(typeof(StockReceipt))!.GetIndexes(), static i => i.IsUnique);
        Assert.Equal("ux_stock_receipts_sku_reference", index.GetDatabaseName());
        Assert.Equal([nameof(StockReceipt.Sku), nameof(StockReceipt.Reference)], index.Properties.Select(static p => p.Name));
    }

    [Fact]
    public void The_audit_trail_and_the_stored_translations_are_part_of_the_same_model_and_therefore_the_same_transaction()
    {
        using var context = Context();
        Assert.Contains(context.Model.GetEntityTypes(), static e => e.GetSchema() == "audit");
        Assert.Contains(context.Model.GetEntityTypes(), static e => e.GetSchema() == "localization" && e.GetTableName() == "translations");
    }

    [Theory]
    [InlineData("requests")]
    [InlineData("processed_messages")]
    public void Idempotency_keys_and_the_inbox_commit_with_the_change_they_protect(string table)
    {
        using var context = Context();
        Assert.Contains(context.Model.GetEntityTypes(), e => e.GetSchema() == "idempotency" && e.GetTableName() == table);
    }

    [Fact]
    public void No_foreign_key_crosses_a_schema()
    {
        // Each module owns a schema. A key from one into another would tie two modules together in the
        // database after the code has been taken apart.
        using var context = Context();
        Assert.All(
            context.Model.GetEntityTypes().SelectMany(static e => e.GetForeignKeys()),
            static key => Assert.Equal(key.PrincipalEntityType.GetSchema(), key.DeclaringEntityType.GetSchema()));
    }

    [Fact]
    public void A_basket_line_is_a_child_entity_keyed_by_its_sku()
    {
        using var context = Context();
        var line = context.Model.FindEntityType(typeof(BasketLine))!;
        Assert.Equal("basket_lines", line.GetTableName());
        Assert.Equal(["BuyerId", "Sku"], line.FindPrimaryKey()!.Properties.Select(static p => p.GetColumnName()));
    }

    [Fact]
    public void Value_objects_are_stored_as_their_single_value()
    {
        using var context = Context();
        var product = context.Model.FindEntityType(typeof(Product))!;
        Assert.Equal(typeof(string), product.FindProperty(nameof(Product.Sku))!.GetValueConverter()!.ProviderClrType);
        Assert.Equal(typeof(decimal), product.FindProperty(nameof(Product.Price))!.GetValueConverter()!.ProviderClrType);
        var address = context.Model.FindEntityType(typeof(ShippingAddress))!;
        Assert.Equal("ShipTo_Phone", address.FindProperty(nameof(ShippingAddress.Phone))!.GetColumnName());
    }

    [Fact]
    public void The_migrations_describe_the_model_exactly()
    {
        // LAB-D010: a model that compiles is not a database that can be built. This fails when an entity
        // changed and nobody ran `dotnet ef migrations add`.
        using var context = Context();
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
