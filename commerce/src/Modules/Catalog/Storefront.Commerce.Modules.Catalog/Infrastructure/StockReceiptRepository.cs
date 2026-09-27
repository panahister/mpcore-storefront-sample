using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public sealed class StockReceiptRepository<TContext>(TContext database) : IStockReceiptRepository where TContext : DbContext
{
    public async Task<StockReceipt?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await database.Set<StockReceipt>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<StockReceipt?> FindAsync(Sku sku, string reference, CancellationToken cancellationToken)
    {
        var text = sku.Value;
        return await database.Set<StockReceipt>()
            .FirstOrDefaultAsync(r => r.Sku == text && r.Reference == reference, cancellationToken).ConfigureAwait(false);
    }

    public void Add(StockReceipt aggregate) => database.Set<StockReceipt>().Add(aggregate);

    public void Remove(StockReceipt aggregate) => database.Set<StockReceipt>().Remove(aggregate);
}
