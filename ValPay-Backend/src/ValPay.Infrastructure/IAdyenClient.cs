using System.Threading;
using System.Threading.Tasks;
using ValPay.Application;

namespace ValPay.Infrastructure;

public interface IAdyenClient
{
    Task<object> GetPaymentMethodsAsync(PaymentMethodsRequest req, CancellationToken ct);
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest req, string idempotencyKey, CancellationToken ct);
    Task<object> GetCostEstimateAsync(string reference, string encryptedCardNumber, long amountMinor, string currency, string country, string? shopperInteraction, object? assumptions, CancellationToken ct);
}

