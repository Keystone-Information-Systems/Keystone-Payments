using System.Threading;
using System.Threading.Tasks;
using ValPay.Application;

namespace ValPay.Infrastructure;

public interface IAdyenClient
{
    Task<object> GetPaymentMethodsAsync(PaymentMethodsRequest req, CancellationToken ct);
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest req, string idempotencyKey, CancellationToken ct);
}

