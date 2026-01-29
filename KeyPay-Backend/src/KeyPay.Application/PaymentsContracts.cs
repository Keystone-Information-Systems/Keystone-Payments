using System.Text.Json.Serialization;

namespace KeyPay.Application;

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
    // Legacy redirect POST target for final handoff (single URL for all outcomes)
    [property: JsonPropertyName("legacyPostUrl")] string? LegacyPostUrl = null,
    // Percentage for surcharge provided by legacy system (0-100). Used to compute surcharge amount immediately.
    [property: JsonPropertyName("surchargePercent")] int? SurchargePercent = null,
    // Minimum surcharge fee (minor units). Applied if computed surcharge is lower.
    [property: JsonPropertyName("minSurchargeFee")] long? MinSurchargeFee = null,
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
    [property: JsonPropertyName("cardHolderName")] string? CardHolderName = null,
    [property: JsonPropertyName("billingAddress")] BillingAddress? BillingAddress = null,
    [property: JsonPropertyName("phoneNumber")] string? PhoneNumber = null,
    [property: JsonPropertyName("email")] string? Email = null);

public record BillingAddress(
    [property: JsonPropertyName("street")] string? Street = null,
    [property: JsonPropertyName("houseNumberOrName")] string? HouseNumberOrName = null,
    [property: JsonPropertyName("city")] string? City = null,
    [property: JsonPropertyName("stateOrProvince")] string? StateOrProvince = null,
    [property: JsonPropertyName("postalCode")] string? PostalCode = null,
    [property: JsonPropertyName("country")] string? Country = null);

public record CreatePaymentResponse(string ResultCode, string? PspReference, object? Action, object Raw);

public record CostEstimateRequest(
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("transactionId")] Guid TransactionId,
    [property: JsonPropertyName("amount")] Amount Amount,
    [property: JsonPropertyName("encryptedCardNumber")] string EncryptedCardNumber,
    [property: JsonPropertyName("country")] string? Country = null,
    [property: JsonPropertyName("shopperInteraction")] string? ShopperInteraction = "Ecommerce",
    [property: JsonPropertyName("assumptions")] object? Assumptions = null);

public record Amount(
    [property: JsonPropertyName("value")] long Value,
    [property: JsonPropertyName("currency")] string Currency);

public record CostEstimateResponse(
    [property: JsonPropertyName("surchargeAmount")] long SurchargeAmount,
    [property: JsonPropertyName("totalWithSurcharge")] long TotalWithSurcharge,
    [property: JsonPropertyName("raw")] object Raw);

