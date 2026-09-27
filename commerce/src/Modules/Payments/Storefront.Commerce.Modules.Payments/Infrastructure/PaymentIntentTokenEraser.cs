using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Storefront.Commerce.Modules.Payments.Infrastructure;

/// <summary>
/// Erases the token of every payment intent that expired unused. An intent that an order used has already
/// given its token away; this covers the shopper who never checked out (rule P5).
/// </summary>
/// <remarks>
/// A hosted service, not a handler: it runs outside Wolverine and may create its own scope. One UPDATE per
/// round, so several instances running it at once do no harm.
/// </remarks>
public sealed class PaymentIntentTokenEraser<TContext>(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<PaymentIntentTokenEraser<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    /// <summary>How often expired tokens are erased: a token outlives its intent by at most this long.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await EraseExpiredAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Expired payment tokens could not be erased; trying again in {Interval}", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Erases the tokens of the intents that expired unused. Returns how many.</summary>
    public async Task<int> EraseExpiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var now = clock.GetUtcNow();
        var erased = await scope.ServiceProvider.GetRequiredService<TContext>().Set<PaymentIntent>()
            .Where(i => i.PaymentToken != null && i.ExpiresOnUtc <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.PaymentToken, (string?)null), cancellationToken)
            .ConfigureAwait(false);
        if (erased > 0)
        {
            logger.LogInformation("Erased the tokens of {Count} expired payment intent(s)", erased);
        }

        return erased;
    }
}
