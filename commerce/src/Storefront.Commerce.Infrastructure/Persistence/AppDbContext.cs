using Microsoft.EntityFrameworkCore;
using MPCore.Audit.EntityFrameworkCore;
using MPCore.Domain.Events;
using MPCore.Idempotency.EntityFrameworkCore;
using MPCore.Localization.EntityFrameworkCore;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;

namespace Storefront.Commerce.Infrastructure.Persistence;

/// <summary>
/// The one context of the host. Each module owns a schema and brings its own mappings; the host stitches
/// them into one model so one transaction can span a checkout. MP Core's base drains aggregate events
/// into the outbox when the change is saved.
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    IAggregateEventSink eventSink)
    : MPCoreDbContext(options, timeProvider, eventSink)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // One line per module: its Infrastructure folder holds the EF configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(Modules.Catalog.AssemblyReference.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(Modules.Basket.AssemblyReference.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(Modules.Ordering.AssemblyReference.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(Modules.Payments.AssemblyReference.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // The audit trail, the stored translations and the idempotency keys belong to the host: one of each,
        // covering every module. Being in this model is what puts them in the business transaction.
        modelBuilder.ApplyMPCoreAudit();
        modelBuilder.ApplyMPCoreLocalization();
        modelBuilder.ApplyMPCoreIdempotency();
        base.OnModelCreating(modelBuilder);
    }
}
