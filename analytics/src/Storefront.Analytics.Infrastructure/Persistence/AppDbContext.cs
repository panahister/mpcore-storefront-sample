using Microsoft.EntityFrameworkCore;
using MPCore.Domain.Events;
using MPCore.Idempotency.EntityFrameworkCore;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;

namespace Storefront.Analytics.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    IAggregateEventSink eventSink)
    : MPCoreDbContext(options, timeProvider, eventSink)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // The inbox belongs to this context, so "read" and the fact commit together.
        modelBuilder.ApplyMPCoreIdempotency();
        base.OnModelCreating(modelBuilder);
    }
}
