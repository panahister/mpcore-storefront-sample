using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Ports;

public interface IRestockAlertRepository : IRepository<RestockAlert, Guid>;
