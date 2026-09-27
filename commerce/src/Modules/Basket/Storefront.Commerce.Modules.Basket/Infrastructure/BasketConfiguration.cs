using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BasketAggregate = Storefront.Commerce.Modules.Basket.Domain.Basket;

namespace Storefront.Commerce.Modules.Basket.Infrastructure;

public sealed class BasketConfiguration : IEntityTypeConfiguration<BasketAggregate>
{
    public const string Schema = "basket";

    public void Configure(EntityTypeBuilder<BasketAggregate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("baskets", Schema);
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("BuyerId").HasMaxLength(64).ValueGeneratedNever();
        builder.Ignore(b => b.BuyerId);
        builder.Ignore(b => b.Total);
        builder.Ignore(b => b.HasUnseenPriceChanges);
        builder.Property(b => b.Currency).HasMaxLength(3).IsRequired();
        builder.OwnsMany(b => b.Lines, lines =>
        {
            lines.ToTable("basket_lines", Schema);
            lines.WithOwner().HasForeignKey("BuyerId");
            // The child entity's identity is the SKU.
            lines.Property(l => l.Id).HasColumnName("Sku").HasMaxLength(32).IsRequired();
            lines.HasKey("BuyerId", "Id");
            lines.Ignore(l => l.Sku);
            lines.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
            lines.Property(l => l.UnitPrice).HasPrecision(18, 2);
            lines.Property(l => l.PreviousUnitPrice).HasPrecision(18, 2);
            lines.Ignore(l => l.LineTotal);

            // A price change fans out to every basket holding the product; this is its lookup.
            lines.HasIndex(l => l.Id).HasDatabaseName("ix_basket_lines_sku");
        });
        builder.Navigation(b => b.Lines).AutoInclude();
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
    }
}
