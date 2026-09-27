using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storefront.Commerce.Modules.Payments.Infrastructure;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public const string Schema = "payments";

    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("payments", Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("OrderId").ValueGeneratedNever();
        builder.Ignore(p => p.OrderId);
        builder.Ignore(p => p.IsPending);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.PaymentToken).HasMaxLength(128);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.ProviderReference).HasMaxLength(64);
        builder.Property(p => p.DeclineCode).HasMaxLength(64);
        builder.Property(p => p.RefundReference).HasMaxLength(64);
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
    }
}
