using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Contracts;
using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using MPCore.Application.Time;

namespace Storefront.Commerce.Modules.Payments.Infrastructure;

public sealed class PaymentIntentRepository<TContext>(TContext database) : IPaymentIntentRepository where TContext : DbContext
{
    public async Task<PaymentIntent?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await database.Set<PaymentIntent>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken).ConfigureAwait(false);

    public void Add(PaymentIntent aggregate) => database.Set<PaymentIntent>().Add(aggregate);

    public void Remove(PaymentIntent aggregate) => database.Set<PaymentIntent>().Remove(aggregate);
}

/// <summary>The published <see cref="IPaymentIntentLookup"/>: a read, on the same context, that tracks nothing.</summary>
public sealed class PaymentIntentLookup<TContext>(TContext database, IClock clock) : IPaymentIntentLookup where TContext : DbContext
{
    public async Task<bool> IsUsableAsync(Guid paymentIntentId, string buyerId, CancellationToken cancellationToken)
    {
        var intent = await database.Set<PaymentIntent>().AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == paymentIntentId, cancellationToken).ConfigureAwait(false);
        return intent is not null && intent.IsUsableBy(buyerId, clock.UtcNow);
    }
}
