using Storefront.Commerce.Infrastructure.Audit;
using Storefront.Commerce.Infrastructure.Persistence;
using Storefront.Commerce.Modules.Basket.Infrastructure;
using Storefront.Commerce.Modules.Catalog.Infrastructure;
using Storefront.Commerce.Modules.Ordering.Infrastructure;
using Storefront.Commerce.Modules.Payments.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MPCore.Audit.EntityFrameworkCore;
using MPCore.Caching.Hybrid;
using MPCore.Idempotency.EntityFrameworkCore;
using MPCore.Localization.EntityFrameworkCore;
using MPCore.Messaging.Wolverine;
using MPCore.Persistence.EntityFrameworkCore.PostgreSql;

namespace Storefront.Commerce.Infrastructure;

public static class DependencyInjection
{
    public const string CacheKeyPrefix = "storefront-commerce:";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string cacheConnectionString,
        Uri paymentProviderBaseAddress,
        string paymentProviderMerchantId)
    {
        ArgumentNullException.ThrowIfNull(services);

        // In-process first level, Redis second, stampede-protected. See GetProductDetailsHandler for what
        // this buys and what it costs with more than one instance.
        services.AddMPCoreHybridCache(cacheConnectionString, options => options.KeyPrefix = CacheKeyPrefix);

        // Registered through Wolverine's integration: a handler that declares IUnitOfWork runs inside this
        // context's transaction, and the messages it publishes are committed with it (transactional outbox).
        // The audit interceptor runs inside the same SaveChanges, so an audited change and its trail commit
        // together. The idempotency interceptor does the same for a request's key and its answer.
        services.AddMPCoreWolverineDbContext<AppDbContext>((provider, options) =>
            PostgreSqlDbContextOptions.Apply(options, connectionString).UseMPCoreAudit(provider).UseMPCoreIdempotency(provider));
        services.AddMPCoreAudit<AppDbContext>(AuditPolicyConfiguration.Configure);

        // Idempotency-Key on requests that must not run twice, and the inbox for integration events that
        // arrive twice (ADR-013). Keys are remembered for a day, processed events for a week.
        services.AddMPCoreIdempotency<AppDbContext>();

        // Translations support edits at run time live in this context and are refreshed on every instance.
        services.AddMPCoreMessageTranslations<AppDbContext>();

        // One explicit line per module. Nothing is scanned.
        services.AddCatalogModule<AppDbContext>();
        services.AddBasketModule<AppDbContext>();
        services.AddOrderingModule<AppDbContext>();
        services.AddPaymentsModule<AppDbContext>(new DemoPayOptions
        {
            BaseAddress = paymentProviderBaseAddress,
            MerchantId = paymentProviderMerchantId
        });
        return services;
    }
}
