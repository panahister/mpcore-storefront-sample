using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Infrastructure.Persistence;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public const string Schema = "fulfillment";

    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("shipments", Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("OrderId").ValueGeneratedNever();
        builder.Ignore(s => s.OrderId);
        builder.Ignore(s => s.ItemCount);
        builder.Property(s => s.OrderNumber).HasMaxLength(24).IsRequired();
        builder.HasIndex(s => s.OrderNumber).IsUnique().HasDatabaseName("ux_shipments_order_number");
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.Carrier).HasMaxLength(60);
        builder.Property(s => s.TrackingCode).HasMaxLength(60);
        builder.HasIndex(s => new { s.Status, s.ReceivedOnUtc }).HasDatabaseName("ix_shipments_status_received");

        // A value object stored in the shipment's own row; Entity Framework rebuilds it through its constructor.
        builder.OwnsOne(s => s.Address, address =>
        {
            address.Property(a => a.RecipientName).HasColumnName("ShipTo_RecipientName").HasMaxLength(100);
            address.Property(a => a.Phone).HasColumnName("ShipTo_Phone").HasMaxLength(20);
            address.Property(a => a.Province).HasColumnName("ShipTo_Province").HasMaxLength(50);
            address.Property(a => a.City).HasColumnName("ShipTo_City").HasMaxLength(50);
            address.Property(a => a.Line).HasColumnName("ShipTo_Line").HasMaxLength(300);
            address.Property(a => a.PostalCode).HasColumnName("ShipTo_PostalCode").HasMaxLength(16);
        });
        builder.Navigation(s => s.Address).IsRequired();

        builder.OwnsMany(s => s.Lines, lines =>
        {
            lines.ToTable("shipment_lines", Schema);
            lines.WithOwner().HasForeignKey("OrderId");
            lines.HasKey("OrderId", "Sku");
            lines.Property(l => l.Sku).HasMaxLength(32).IsRequired();
            lines.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
        });
        builder.Navigation(s => s.Lines).AutoInclude();

        // Two clerks dispatching one parcel: the second save fails on the row version.
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
    }
}
