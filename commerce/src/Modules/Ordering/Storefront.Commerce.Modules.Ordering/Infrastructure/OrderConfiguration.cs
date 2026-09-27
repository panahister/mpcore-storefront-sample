using Storefront.Commerce.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storefront.Commerce.Modules.Ordering.Infrastructure;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public const string Schema = "ordering";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("orders", Schema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasConversion(id => id.Value, value => new OrderId(value)).ValueGeneratedNever();
        builder.Property(o => o.OrderNumber).HasMaxLength(24).IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique().HasDatabaseName("ux_orders_number");
        builder.Property(o => o.BuyerId).HasMaxLength(64).IsRequired();
        builder.Property(o => o.BuyerName).HasMaxLength(100).IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        builder.Property(o => o.Total).HasPrecision(18, 2);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.PaymentReference).HasMaxLength(64);
        builder.Property(o => o.CancellationReason).HasMaxLength(32);
        builder.Property(o => o.Carrier).HasMaxLength(50);
        builder.Property(o => o.TrackingCode).HasMaxLength(64);
        builder.Ignore(o => o.IsCancelled);
        builder.Ignore(o => o.CanConfirmStock);
        builder.Ignore(o => o.CanMarkPaid);
        builder.Ignore(o => o.CanCancel);

        builder.HasIndex(o => new { o.BuyerId, o.PlacedOnUtc }).HasDatabaseName("ix_orders_buyer_placed");
        builder.HasIndex(o => new { o.Status, o.PlacedOnUtc }).HasDatabaseName("ix_orders_status_placed");

        // A value object stored in the order's own row. Entity Framework rebuilds it through its constructor,
        // whose parameters match the properties; the two single-value objects inside go through converters.
        builder.OwnsOne(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.RecipientName).HasColumnName("ShipTo_RecipientName").HasMaxLength(100);
            address.Property(a => a.Phone).HasColumnName("ShipTo_Phone").HasMaxLength(PhoneNumber.MaximumLength)
                .HasConversion(phone => phone.Value, value => PhoneNumber.FromTrusted(value));
            address.Property(a => a.Province).HasColumnName("ShipTo_Province").HasMaxLength(50);
            address.Property(a => a.City).HasColumnName("ShipTo_City").HasMaxLength(50);
            address.Property(a => a.Line).HasColumnName("ShipTo_Line").HasMaxLength(300);
            address.Property(a => a.PostalCode).HasColumnName("ShipTo_PostalCode").HasMaxLength(PostalCode.MaximumLength)
                .HasConversion(code => code.Value, value => PostalCode.FromTrusted(value));
        });
        builder.Navigation(o => o.ShippingAddress).IsRequired();

        builder.OwnsMany(o => o.Lines, lines =>
        {
            lines.ToTable("order_lines", Schema);
            lines.WithOwner().HasForeignKey("OrderId");
            lines.HasKey("OrderId", "Sku");
            lines.Property(l => l.Sku).HasMaxLength(32).IsRequired();
            lines.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
            lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
            lines.Ignore(l => l.LineTotal);
        });

        builder.OwnsMany(o => o.History, history =>
        {
            history.ToTable("order_history", Schema);
            history.WithOwner().HasForeignKey("OrderId");
            history.Property<long>("Id").UseIdentityAlwaysColumn();
            history.HasKey("Id");
            history.Property(h => h.Status).HasMaxLength(32).IsRequired();
            history.Property(h => h.Note).HasMaxLength(300);
        });

        builder.Navigation(o => o.Lines).AutoInclude();
        builder.Navigation(o => o.History).AutoInclude();
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
    }
}
