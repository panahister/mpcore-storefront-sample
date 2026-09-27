using Storefront.Commerce.Modules.Payments.Domain;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Payments.Application.Ports;

public interface IPaymentIntentRepository : IRepository<PaymentIntent, Guid>;
