using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Storefront.Commerce.Modules.Payments.Infrastructure;

public sealed class PaymentRepository<TContext>(TContext database) : IPaymentRepository where TContext : DbContext
{
    public async Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await database.Set<Payment>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

    public void Add(Payment aggregate) => database.Set<Payment>().Add(aggregate);

    public void Remove(Payment aggregate) => database.Set<Payment>().Remove(aggregate);
}
