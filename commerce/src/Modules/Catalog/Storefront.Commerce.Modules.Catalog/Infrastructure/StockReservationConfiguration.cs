using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("stock_reservations", CatalogSchema.Name);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("OrderId").ValueGeneratedNever();
        builder.Ignore(r => r.OrderId);
        builder.Ignore(r => r.IsHeld);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.OwnsMany(r => r.Lines, lines =>
        {
            lines.ToTable("stock_reservation_lines", CatalogSchema.Name);
            lines.WithOwner().HasForeignKey("OrderId");
            lines.Property(l => l.Sku).HasMaxLength(32).IsRequired();
            lines.HasKey("OrderId", "Sku");
        });
        builder.Navigation(r => r.Lines).AutoInclude();
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
    }
}
