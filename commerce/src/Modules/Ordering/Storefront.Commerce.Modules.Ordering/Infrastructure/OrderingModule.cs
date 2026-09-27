using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Storefront.Commerce.Modules.Ordering.Infrastructure;

public static class OrderingModule
{
    public static IServiceCollection AddOrderingModule<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IOrderRepository, OrderRepository<TContext>>();
        services.AddScoped<IOrderReadModel, OrderReadModel<TContext>>();
        return services;
    }
}
