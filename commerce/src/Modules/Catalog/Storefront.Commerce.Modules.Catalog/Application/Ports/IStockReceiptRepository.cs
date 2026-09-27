using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Ports;

public interface IStockReceiptRepository : IRepository<StockReceipt, Guid>
{
    /// <summary>Finds the receipt of a delivery by its business key. The reference is given normalized.</summary>
    Task<StockReceipt?> FindAsync(Sku sku, string reference, CancellationToken cancellationToken);
}
