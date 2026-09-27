using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MPCore.Resilience.Http;

namespace Storefront.Commerce.Modules.Payments.Infrastructure;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule<TContext>(this IServiceCollection services, DemoPayOptions provider)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);

        services.AddSingleton(provider);
        services.AddScoped<IPaymentRepository, PaymentRepository<TContext>>();
        services.AddScoped<IPaymentGateway, DemoPayGateway>();
        services.AddScoped<IPaymentIntentRepository, PaymentIntentRepository<TContext>>();

        // The published read checkout asks before it answers the shopper.
        services.AddScoped<IPaymentIntentLookup, PaymentIntentLookup<TContext>>();
        services.AddHostedService<PaymentIntentTokenEraser<TContext>>();

        services.AddMPCoreResilientHttpClient(
            DemoPayGateway.ClientName,
            client =>
            {
                client.BaseAddress = provider.BaseAddress;
                client.DefaultRequestHeaders.Add("X-Merchant-Id", provider.MerchantId);
            },
            resilience =>
            {
                // A card payment is worth waiting a little for, but a shopper is watching a spinner.
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                resilience.Retry.MaxRetryAttempts = 2;
                resilience.Retry.Delay = TimeSpan.FromMilliseconds(300);
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });
        return services;
    }
}
