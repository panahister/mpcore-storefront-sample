using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public static class CatalogModule
{
    /// <summary>
    /// Registers the module's adapters, by type. Wolverine generates each handler's code with its
    /// dependencies constructed inline and refuses a lambda it could only resolve by service location.
    /// </summary>
    public static IServiceCollection AddCatalogModule<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IProductRepository, ProductRepository<TContext>>();
        services.AddScoped<IStockReservationRepository, StockReservationRepository<TContext>>();
        services.AddScoped<IStockReceiptRepository, StockReceiptRepository<TContext>>();
        services.AddScoped<IRestockAlertRepository, RestockAlertRepository<TContext>>();
        services.AddScoped<ICatalogReadModel, CatalogReadModel<TContext>>();

        // The published lookup other modules use. Same context, same transaction: when the Basket
        // asks for a price inside its own handler, it reads the committed catalog.
        services.AddScoped<ICatalogLookup, CatalogReadModel<TContext>>();
        return services;
    }
}
