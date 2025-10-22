using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KeyPay.Application;
using KeyPay.Infrastructure.Resilience;

namespace KeyPay.Infrastructure;

public sealed class AdyenClient(HttpClient http, IConfiguration cfg, ILogger<AdyenClient> logger) : IAdyenClient
{
    private readonly string _merchantAccount = cfg["Adyen:MerchantAccount"] ?? "";
    private readonly string _apiVersion = cfg["Adyen:ApiVersion"] ?? "v71";
    private readonly string _binLookupVersion = cfg["Adyen:BinLookupVersion"] ?? "v54";
    private readonly string _palBaseUrl = cfg["Adyen:PalBaseUrl"] ?? "https://pal-test.adyen.com";
    private readonly bool _useMockPaymentMethods =
        bool.TryParse(cfg["Adyen:UseMockPaymentMethods"], out var b) && b;
    private readonly ILogger<AdyenClient> _logger = logger;

    public async Task<object> GetPaymentMethodsAsync(PaymentMethodsRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Getting payment methods for amount {Amount} {Currency}", req.AmountMinor, req.Currency);

        try
        {
            if (_useMockPaymentMethods)
            {
                _logger.LogInformation("Using mock payment methods data for testing");
                var mockResponse = new
                {
                    paymentMethods = new[]
                    {
                        new
                        {
                            type = "scheme",
                            name = "Credit or debit card",
                            brands = new[] { "visa", "mc", "amex" }
                        }
                    }
                };

                _logger.LogInformation("Successfully retrieved mock payment methods");
                return mockResponse;
            }

            var payload = new
            {
                merchantAccount = _merchantAccount,
                amount = new { value = req.AmountMinor, currency = req.Currency },
                countryCode = req.Country,
                channel = "Web"
            };

            using var res = await http.PostAsJsonAsync($"/{_apiVersion}/paymentMethods", payload, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"Adyen {res.StatusCode}: {body}");

            // Return a JsonElement so callers still get a structured object
            var json = JsonDocument.Parse(body).RootElement.Clone();

            _logger.LogInformation("Successfully retrieved payment methods");
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get payment methods for amount {Amount} {Currency}", req.AmountMinor, req.Currency);
            throw;
        }
    }

    public async Task<object> GetCostEstimateAsync(string reference, string encryptedCardNumber, long amountMinor, string currency, string country, string? shopperInteraction, object? assumptions, CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                merchantAccount = _merchantAccount,
                amount = new { value = amountMinor, currency },
                reference,
                countryCode = country,
                encryptedCardNumber,
                shopperInteraction = string.IsNullOrWhiteSpace(shopperInteraction) ? "Ecommerce" : shopperInteraction,
                assumptions
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"{_palBaseUrl}/pal/servlet/BinLookup/{_binLookupVersion}/getCostEstimate"))
            {
                Content = JsonContent.Create(payload)
            };
            var res = await http.SendAsync(message, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"Adyen {res.StatusCode}: {body}");

            var json = JsonDocument.Parse(body).RootElement.Clone();
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cost estimate for {Amount}{Currency}", amountMinor, currency);
            throw;
        }
    }

    public async Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest req, string idempotencyKey, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Creating payment {Reference} {Amount}{Currency}, Country: {Country}",
                req.Reference, req.AmountMinor, req.Currency, req.Country ?? "NULL");

            var countryCode = req.Country ?? "US";
            _logger.LogInformation("Country from request: {Country}, Using countryCode: {CountryCode}", req.Country ?? "NULL", countryCode);

            var payload = new
            {
                merchantAccount = _merchantAccount,
                amount = new { value = req.AmountMinor, currency = req.Currency },
                reference = req.Reference,
                returnUrl = req.ReturnUrl,
                channel = "Web",
                shopperInteraction = "Ecommerce",
                countryCode,
                paymentMethod = req.PaymentMethod,
                origin = req.Origin
            };

            _logger.LogInformation("Payload countryCode: {CountryCode}", payload.countryCode);

            using var message = new HttpRequestMessage(HttpMethod.Post, $"/{_apiVersion}/payments")
            {
                Content = JsonContent.Create(payload)
            };
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }

            using var res = await http.SendAsync(message, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"Adyen {res.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var resultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetString() ?? "Unknown" : "Unknown";
            var psp = root.TryGetProperty("pspReference", out var pr) ? pr.GetString() : null;

            object? action = null;
            if (root.TryGetProperty("action", out var act))
            {
                action = JsonSerializer.Deserialize<object>(act.GetRawText());
            }

            _logger.LogInformation("Payment result {ResultCode}, psp {PSP}", resultCode, psp);
            return new CreatePaymentResponse(resultCode, psp, action, root.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment {Reference}", req.Reference);
            throw;
        }
    }
}
