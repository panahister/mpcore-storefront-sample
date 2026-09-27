using MPCore.Persistence.Abstractions;
using Storefront.Analytics.Domain;

namespace Storefront.Analytics.Application.Ports;

public interface IOrderFactRepository : IRepository<OrderFact, Guid>;
