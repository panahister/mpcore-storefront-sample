using Storefront.Commerce.Modules.Ordering.Domain;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Ordering.Application.Ports;

public interface IOrderRepository : IRepository<Order, OrderId>;
