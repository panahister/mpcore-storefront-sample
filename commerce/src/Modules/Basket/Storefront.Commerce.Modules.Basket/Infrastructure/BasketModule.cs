using Storefront.Commerce.Modules.Basket.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Storefront.Commerce.Modules.Basket.Infrastructure;

public static class BasketModule
{
    /// <summary>Registers the module's adapters, by type: Wolverine builds a handler's dependencies inline and refuses a lambda.</summary>
    public static IServiceCollection AddBasketModule<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IBasketRepository, BasketRepository<TContext>>();
        services.AddScoped<IBasketReadModel, BasketReadModel<TContext>>();
        return services;
    }
}
