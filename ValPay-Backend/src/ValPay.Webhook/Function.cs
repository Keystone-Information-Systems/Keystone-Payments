using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using System.Diagnostics;
using Dapper;
using ValPay.Infrastructure;

//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace ValPay.Webhook;

public class Function
{
    private readonly string _hmacKey;
    private readonly bool _enableDbWrites;
    private readonly bool _requireValidHmac;
    private readonly string? _postgresConnectionString;

    public Function()
    {
        _hmacKey = Environment.GetEnvironmentVariable("Adyen__HmacKey") ?? "";
        _postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");

        var enableDb = Environment.GetEnvironmentVariable("Webhooks__EnableDbWrites");
        _enableDbWrites = bool.TryParse(enableDb, out var parsedEnableDb) && parsedEnableDb;

        var requireHmac = Environment.GetEnvironmentVariable("Webhooks__RequireValidHmac");
        _requireValidHmac = !bool.TryParse(requireHmac, out var parsedRequireHmac) || parsedRequireHmac;
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest req, ILambdaContext ctx)
    {
        var correlationId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        
        ctx.Logger.LogInformation("Webhook request started. CorrelationId: {CorrelationId}", correlationId);
        ctx.Logger.LogInformation("Request details - Method: {Method}, Path: {Path}, Headers: {Headers}", 
            req.HttpMethod, req.Path, string.Join(", ", req.Headers?.Select(h => $"{h.Key}={h.Value}") ?? new string[0]));
        
        try
        {
            var payload = req.Body ?? "{}";

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var notification = JsonSerializer.Deserialize<NotificationRequest>(payload, options);

            if (notification?.NotificationItems == null || notification.NotificationItems.Count == 0)
            {
                ctx.Logger.LogWarning("No notificationItems in payload. CorrelationId: {CorrelationId}", correlationId);
                stopwatch.Stop();
                return new APIGatewayProxyResponse 
                { 
                    StatusCode = 200, 
                    Body = "[accepted]",
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                };
            }

            for (int i = 0; i < (notification.NotificationItems?.Count ?? 0); i++)
            {
                var container = (notification.NotificationItems != null && notification.NotificationItems.Count > i) ? notification.NotificationItems[i] : null;
                var item = container?.Item;
                if (item == null)
                {
                    ctx.Logger.LogWarning("Notification item missing. CorrelationId: {CorrelationId}", correlationId);
                    continue;
                }

                var isValid = AdyenHmacValidator.Verify(item, _hmacKey);
                if (!isValid)
                {
                    ctx.Logger.LogWarning("Invalid HMAC for event {EventCode} PSP {PspReference}. CorrelationId: {CorrelationId}", item.EventCode, item.PspReference, correlationId);
                    if (_requireValidHmac)
                    {
                        // Skip processing but still acknowledge overall
                        continue;
                    }
                    // Proceeding because RequireValidHmac=false
                }

                ctx.Logger.LogInformation(
                    "Valid webhook item. EventCode: {EventCode}, Success: {Success}, PSP: {PspReference}, Original: {OriginalReference}, MerchantRef: {MerchantReference}. CorrelationId: {CorrelationId}",
                    item.EventCode, item.Success, item.PspReference, item.OriginalReference, item.MerchantReference, correlationId);

                if (!_enableDbWrites)
                {
                    continue; // log-only mode
                }

                if (string.IsNullOrEmpty(_postgresConnectionString))
                {
                    ctx.Logger.LogWarning("DB writes enabled but no ConnectionStrings__Postgres provided. CorrelationId: {CorrelationId}", correlationId);
                    continue;
                }

                if (!string.IsNullOrEmpty(item.MerchantReference) && !string.IsNullOrEmpty(item.EventCode))
                {
                    var db = new Db(_postgresConnectionString);
                    using var connection = db.Open();
                    var transaction = await connection.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT transactionId, tenantId FROM transactions WHERE merchantReference = @ref",
                        new { @ref = item.MerchantReference });

                    if (transaction != null)
                    {
                        var txId = (Guid)transaction.transactionid;
                        var tenantId = (Guid)transaction.tenantid;

                        var status = item.EventCode switch
                        {
                            "AUTHORISATION" when string.Equals(item.Success, "true", StringComparison.OrdinalIgnoreCase) => "Authorised",
                            "AUTHORISATION" when string.Equals(item.Success, "false", StringComparison.OrdinalIgnoreCase) => "Refused",
                            "CAPTURE" => "Captured",
                            "CANCELLATION" => "Cancelled",
                            "REFUND" => "Refunded",
                            _ => null
                        };

                        if (status != null)
                        {
                            await db.UpdateStatusWithOperationAsync(
                                txId,
                                status,
                                item.PspReference,
                                item.EventCode,
                                null,
                                tenantId,
                                $"WEBHOOK_{item.EventCode}",
                                null,
                                null,
                                payload,
                                default);

                            ctx.Logger.LogInformation("Transaction {TxId} updated to status {Status} from webhook", txId, status);
                        }
                    }
                }
            }

            stopwatch.Stop();
            ctx.Logger.LogInformation("Webhook processed in {Duration}ms. CorrelationId: {CorrelationId}", 
                stopwatch.ElapsedMilliseconds, correlationId);

            return new APIGatewayProxyResponse 
            { 
                StatusCode = 200, 
                Body = "[accepted]",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ctx.Logger.LogError(ex, "Webhook processing failed after {Duration}ms. CorrelationId: {CorrelationId}", 
                stopwatch.ElapsedMilliseconds, correlationId);
            
            // Always acknowledge to avoid retries; errors are logged for investigation
            return new APIGatewayProxyResponse 
            { 
                StatusCode = 200, 
                Body = "[accepted]",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }
    }
}

