using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Ports;

public interface IStockReservationRepository : IRepository<StockReservation, Guid>;
