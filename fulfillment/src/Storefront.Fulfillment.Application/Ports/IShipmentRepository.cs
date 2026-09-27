using MPCore.Persistence.Abstractions;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Application.Ports;

public interface IShipmentRepository : IRepository<Shipment, Guid>;
