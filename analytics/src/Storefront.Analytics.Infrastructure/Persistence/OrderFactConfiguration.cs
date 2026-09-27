using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storefront.Analytics.Domain;

namespace Storefront.Analytics.Infrastructure.Persistence;

public sealed class OrderFactConfiguration : IEntityTypeConfiguration<OrderFact>
{
    public const string Schema = "analytics";

    public const string Table = "order_facts";

    /// <summary>The column the hypertable is partitioned on.</summary>
    public const string TimeColumn = "occurred_on_utc";

    public void Configure(EntityTypeBuilder<OrderFact> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, Schema);

        // TimescaleDB partitions a hypertable on its time column, and every unique index has to contain
        // that column. The event's identity alone is unique; together with the time it is the key.
        builder.HasKey(f => new { f.Id, f.OccurredOnUtc });
        builder.Property(f => f.Id).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(f => f.OccurredOnUtc).HasColumnName(TimeColumn);
        builder.Property(f => f.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        builder.Property(f => f.OrderId).HasColumnName("order_id");
        builder.Property(f => f.OrderNumber).HasColumnName("order_number").HasMaxLength(24).IsRequired();
        builder.Property(f => f.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(f => f.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(f => f.ItemCount).HasColumnName("item_count");
        builder.Property(f => f.Region).HasColumnName("region").HasMaxLength(50);
        builder.Property(f => f.City).HasColumnName("city").HasMaxLength(50);
        builder.Property(f => f.Reason).HasColumnName("reason").HasMaxLength(32);
        builder.Property(f => f.CreatedOnUtc).HasColumnName("recorded_on_utc");
        builder.Property(f => f.ModifiedOnUtc).HasColumnName("modified_on_utc");

        builder.HasIndex(f => new { f.Kind, f.OccurredOnUtc }).HasDatabaseName("ix_order_facts_kind_time");
    }
}
