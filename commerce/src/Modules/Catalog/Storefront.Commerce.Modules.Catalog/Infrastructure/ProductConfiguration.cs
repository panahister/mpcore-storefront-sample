using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("products", CatalogSchema.Name, table =>
        {
            // The invariants the aggregate enforces, enforced again where a hand-written UPDATE would
            // otherwise slip past them.
            table.HasCheckConstraint("ck_products_price_positive", "\"Price\" > 0");
            table.HasCheckConstraint("ck_products_stock_consistent", "\"Reserved\" >= 0 AND \"OnHand\" >= \"Reserved\"");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, value => new ProductId(value)).ValueGeneratedNever();

        // Value objects are stored as their single value. FromTrusted skips the rule check on the way
        // back from the database: the value passed it when it was stored.
        builder.Property(p => p.Sku).HasConversion(sku => sku.Value, value => Sku.FromTrusted(value)).HasMaxLength(Sku.MaximumLength).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique().HasDatabaseName("ux_products_sku");
        builder.Property(p => p.Price).HasConversion(price => price.Amount, value => Price.FromTrusted(value)).HasPrecision(18, 2);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Category).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Brand).HasMaxLength(100);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(p => p.Available);
        builder.Ignore(p => p.IsSellable);
        builder.HasIndex(p => new { p.Category, p.Status }).HasDatabaseName("ix_products_category_status");

        // Two orders racing for the last unit: the second save fails here and is retried.
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
    }
}
