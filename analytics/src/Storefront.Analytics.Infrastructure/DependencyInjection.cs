using Microsoft.Extensions.DependencyInjection;
using MPCore.Caching.Memory;
using MPCore.Idempotency.EntityFrameworkCore;
using MPCore.Messaging.Wolverine;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;
using Storefront.Analytics.Application.Ports;
using Storefront.Analytics.Infrastructure.Persistence;

namespace Storefront.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddMPCoreMemoryCache();
        // Registered through Wolverine's integration: a handler that declares IUnitOfWork runs inside this
        // context's transaction. The idempotency interceptor runs inside the same save.
        services.AddMPCoreWolverineDbContext<AppDbContext>((provider, options) =>
            PostgreSqlDbContextOptions.Apply(options, connectionString).UseMPCoreIdempotency(provider));

        // The inbox for integration events that are read twice (ADR-013).
        services.AddMPCoreIdempotency<AppDbContext>();

        // By type, never with a lambda: Wolverine builds a handler's dependencies inline.
        services.AddScoped<IOrderFactRepository, OrderFactRepository>();
        services.AddScoped<ISalesReadModel, SalesReadModel>();
        return services;
    }
}
