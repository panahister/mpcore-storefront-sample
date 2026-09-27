using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public sealed class RestockAlertConfiguration : IEntityTypeConfiguration<RestockAlert>
{
    public void Configure(EntityTypeBuilder<RestockAlert> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("restock_alerts", CatalogSchema.Name);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Sku).HasMaxLength(32).IsRequired();
        builder.Property(a => a.ProductName).HasMaxLength(200).IsRequired();
        builder.HasIndex(a => a.RaisedOnUtc).HasDatabaseName("ix_restock_alerts_raised");
    }
}
