using Storefront.Commerce.Modules.Payments.Domain;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Payments.Application.Ports;

public interface IPaymentRepository : IRepository<Payment, Guid>;
