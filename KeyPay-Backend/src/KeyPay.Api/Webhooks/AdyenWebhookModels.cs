using System.Text.Json.Serialization;

namespace KeyPay.Api.Webhooks;

public sealed class NotificationRequest
{
    [JsonPropertyName("notificationItems")]
    public List<NotificationItemContainer>? NotificationItems { get; set; }
}

public sealed class NotificationItemContainer
{
    [JsonPropertyName("NotificationRequestItem")]
    public NotificationRequestItem? Item { get; set; }
}

public sealed class NotificationRequestItem
{
    [JsonPropertyName("eventCode")]
    public string? EventCode { get; set; }

    [JsonPropertyName("success")]
    public string? Success { get; set; }

    [JsonPropertyName("merchantAccountCode")]
    public string? MerchantAccountCode { get; set; }

    [JsonPropertyName("merchantReference")]
    public string? MerchantReference { get; set; }

    [JsonPropertyName("pspReference")]
    public string? PspReference { get; set; }

    [JsonPropertyName("originalReference")]
    public string? OriginalReference { get; set; }

    [JsonPropertyName("amount")]
    public NotificationAmount? Amount { get; set; }

    [JsonPropertyName("additionalData")]
    public NotificationAdditionalData? AdditionalData { get; set; }
}

public sealed class NotificationAmount
{
    [JsonPropertyName("value")]
    public long? Value { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

public sealed class NotificationAdditionalData
{
    [JsonPropertyName("hmacSignature")]
    public string? HmacSignature { get; set; }
}


