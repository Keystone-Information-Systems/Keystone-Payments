using System.Text.Json.Serialization;

namespace ValPay.Application;

public record LineItemRequest(
    [property: JsonPropertyName("accountNumber")] string AccountNumber,
    [property: JsonPropertyName("billNumber")] string? BillNumber,
    [property: JsonPropertyName("description")] string? Description,
    // Accepts minor units; external field name is "amount"
    [property: JsonPropertyName("amount")] long AmountValue);

public record PaymentMethodsRequest(
    [property: JsonPropertyName("amountMinor")] long? AmountMinor, 
    [property: JsonPropertyName("currency")] string? Currency, 
    [property: JsonPropertyName("country")] string? Country, 
    [property: JsonPropertyName("merchantAccount")] string MerchantAccount, 
    [property: JsonPropertyName("orderId")] string OrderId,
    [property: JsonPropertyName("username")] string? Username = null,
    [property: JsonPropertyName("email")] string? Email = null,
    [property: JsonPropertyName("cardHolderName")] string? CardHolderName = null,
    [property: JsonPropertyName("lineItems")] IReadOnlyList<LineItemRequest>? LineItems = null);
public record PaymentMethodsResponse(string PaymentUrl, string SessionId);

public record CreatePaymentRequest(
    string Reference,
    long AmountMinor,
    string Currency,
    string ReturnUrl,
    object PaymentMethod, // Adyen encrypted blob
    string? Country = null,
    string? Origin = null,
    [property: JsonPropertyName("cardHolderName")] string? CardHolderName = null);

public record CreatePaymentResponse(string ResultCode, string? PspReference, object? Action, object Raw);

