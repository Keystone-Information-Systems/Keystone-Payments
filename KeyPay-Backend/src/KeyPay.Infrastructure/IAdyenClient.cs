using System.Threading;
using System.Threading.Tasks;
using KeyPay.Application;

namespace KeyPay.Infrastructure;

public interface IAdyenClient
{
    Task<object> GetPaymentMethodsAsync(PaymentMethodsRequest req, CancellationToken ct);
    Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest req, long baseAmount, long surcharge, string idempotencyKey, CancellationToken ct);
    Task<object> GetCostEstimateAsync(string reference, string encryptedCardNumber, long amountMinor, string currency, string country, string? shopperInteraction, object? assumptions, CancellationToken ct);
    Task<object> SubmitPaymentDetailsAsync(object details, CancellationToken ct);
}

