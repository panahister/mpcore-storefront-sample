using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Storefront.Commerce.Modules.Payments.Infrastructure;

public sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("payment_intents", PaymentConfiguration.Schema);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.BuyerId).HasMaxLength(128).IsRequired();
        builder.Property(i => i.PaymentToken).HasMaxLength(128);
        builder.HasIndex(i => i.ExpiresOnUtc).HasDatabaseName("ix_payment_intents_expires");

        // Two orders racing for one intent: the loser's save fails here and its retry finds the intent used.
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");
    }
}
