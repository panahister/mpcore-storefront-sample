using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public sealed class StockReceiptConfiguration : IEntityTypeConfiguration<StockReceipt>
{
    public void Configure(EntityTypeBuilder<StockReceipt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("stock_receipts", CatalogSchema.Name);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Sku).HasMaxLength(Sku.MaximumLength).IsRequired();
        builder.Property(r => r.Reference).HasMaxLength(StockReceipt.MaximumReferenceLength).IsRequired();

        // The business key. Two requests racing with the same delivery note both find no receipt; the
        // database lets one of them commit, and the host retries the other, which then finds the receipt.
        builder.HasIndex(r => new { r.Sku, r.Reference }).IsUnique().HasDatabaseName("ux_stock_receipts_sku_reference");
    }
}
