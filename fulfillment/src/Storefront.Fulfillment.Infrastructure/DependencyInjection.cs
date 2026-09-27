using Microsoft.Extensions.DependencyInjection;
using MPCore.Audit.EntityFrameworkCore;
using MPCore.Idempotency.EntityFrameworkCore;
using MPCore.Messaging.Wolverine;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Infrastructure.Audit;
using Storefront.Fulfillment.Infrastructure.Persistence;

namespace Storefront.Fulfillment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered through Wolverine's integration: a handler that declares IUnitOfWork runs inside this
        // context's transaction, and the events its aggregates raise are committed with it (transactional
        // outbox). The audit and idempotency interceptors run inside the same save.
        services.AddMPCoreWolverineDbContext<AppDbContext>((provider, options) =>
            PostgreSqlDbContextOptions.Apply(options, connectionString).UseMPCoreAudit(provider).UseMPCoreIdempotency(provider));
        services.AddMPCoreAudit<AppDbContext>(AuditPolicyConfiguration.Configure);

        // The inbox for integration events that arrive twice (ADR-013).
        services.AddMPCoreIdempotency<AppDbContext>();

        // By type, never with a lambda: Wolverine builds a handler's dependencies inline.
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IShipmentReadModel, ShipmentReadModel>();
        return services;
    }
}
