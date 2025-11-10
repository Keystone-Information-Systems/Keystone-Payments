using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using System.Diagnostics;
using Dapper;
using KeyPay.Infrastructure;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Adyen.Model.Notification;
using Adyen.Util;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace KeyPay.Webhook;

public class Function
{
    private readonly string _hmacKey;
    private readonly bool _enableDbWrites;
    private readonly bool _requireValidHmac;
    private readonly string? _postgresConnectionString;
    private static readonly IAmazonSecretsManager _sm = new AmazonSecretsManagerClient();
    private readonly Dictionary<string, string> _hmacCache = new(StringComparer.OrdinalIgnoreCase);

    public Function()
    {
        _hmacKey = Environment.GetEnvironmentVariable("Adyen__HmacKey") ?? "";

        var rdsSecretName = Environment.GetEnvironmentVariable("Rds__SecretName");
        if (!string.IsNullOrWhiteSpace(rdsSecretName))
        {
            try { _postgresConnectionString = BuildPostgresConnectionStringFromSecretAsync(rdsSecretName).GetAwaiter().GetResult(); }
            catch { _postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"); }
        }
        else
        {
            _postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        }

        var enableDb = Environment.GetEnvironmentVariable("Webhooks__EnableDbWrites");
        _enableDbWrites = bool.TryParse(enableDb, out var parsedEnableDb) && parsedEnableDb;

        var requireHmac = Environment.GetEnvironmentVariable("Webhooks__RequireValidHmac");
        _requireValidHmac = !bool.TryParse(requireHmac, out var parsedRequireHmac) || parsedRequireHmac;
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest req, ILambdaContext ctx)
    {
        var correlationId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        
        Log.Info(ctx, $"Webhook request started. CorrelationId: {correlationId}");
        Log.Info(ctx, $"Request details - Method: {req.HttpMethod}, Path: {req.Path}");
        
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
                Log.Warn(ctx, $"No notificationItems in payload. CorrelationId: {correlationId}");
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
                    Log.Warn(ctx, $"Notification item missing. CorrelationId: {correlationId}");
                    continue;
                }

                // Choose per-tenant HMAC if available
                var merchantAcct = item.MerchantAccount ?? item.MerchantAccountCode;
                var hmacToUse = await GetTenantHmacKeyAsync(merchantAcct ?? string.Empty) ?? _hmacKey;
                // Validate HMAC using Adyen SDK HmacValidator with SDK signing rules (HEX key)
                static string Escape(string? v)
                {
                    if (string.IsNullOrEmpty(v)) return string.Empty;
                    return v.Replace(@"\", @"\\").Replace(":", @"\:");
                }
                var amountValue = item.Amount?.Value?.ToString() ?? string.Empty;
                var amountCurrency = item.Amount?.Currency ?? string.Empty;
                var successStr = string.Equals(item.Success, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
                var macString = string.Join(":", new[]
                {
                    Escape(item.PspReference),
                    Escape(item.OriginalReference),
                    Escape(item.MerchantAccountCode ?? item.MerchantAccount),
                    Escape(item.MerchantReference),
                    Escape(amountValue),
                    Escape(amountCurrency),
                    Escape(item.EventCode),
                    Escape(successStr)
                });
                var expected = new HmacValidator().CalculateHmac(macString, hmacToUse);
                var provided = item.AdditionalData?.HmacSignature ?? string.Empty;
                var isValid = string.Equals(expected, provided, StringComparison.Ordinal);
                if (!isValid)
                {
                    Log.Warn(ctx, $"Invalid HMAC for event {item.EventCode} PSP {item.PspReference} merchant {merchantAcct}. CorrelationId: {correlationId}");
                    if (_requireValidHmac)
                    {
                        // Skip processing but still acknowledge overall
                        continue;
                    }
                    // Proceeding because RequireValidHmac=false
                }

                Log.Info(ctx, $"Valid webhook item. EventCode: {item.EventCode}, Success: {item.Success}, PSP: {item.PspReference}, Original: {item.OriginalReference}, MerchantRef: {item.MerchantReference}. CorrelationId: {correlationId}");

                if (!_enableDbWrites)
                {
                    continue; // log-only mode
                }

                if (string.IsNullOrEmpty(_postgresConnectionString))
                {
                    Log.Warn(ctx, $"DB writes enabled but no ConnectionStrings__Postgres provided. CorrelationId: {correlationId}");
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
                            using var payloadDoc = JsonDocument.Parse(payload);
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
                                payloadDoc.RootElement,
                                default);

                            Log.Info(ctx, $"Transaction {txId} updated to status {status} from webhook");
                        }
                    }
                }
            }

            stopwatch.Stop();
            Log.Info(ctx, $"Webhook processed in {stopwatch.ElapsedMilliseconds}ms. CorrelationId: {correlationId}");

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
            Log.Error(ctx, $"Webhook processing failed after {stopwatch.ElapsedMilliseconds}ms. CorrelationId: {correlationId}. Error: {ex.Message}");
            
            // Always acknowledge to avoid retries; errors are logged for investigation
            return new APIGatewayProxyResponse 
            { 
                StatusCode = 200, 
                Body = "[accepted]",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }
    }

    private async Task<string?> GetTenantHmacKeyAsync(string merchantAccount)
    {
        if (string.IsNullOrWhiteSpace(merchantAccount) || string.IsNullOrWhiteSpace(_postgresConnectionString)) return null;
        if (_hmacCache.TryGetValue(merchantAccount, out var cached))
        {
            Console.WriteLine($"webhook: HMAC cache hit for merchant '{merchantAccount}'");
            return cached;
        }

        var db = new Db(_postgresConnectionString);
        using var c = db.Open();
        var secretName = await c.QuerySingleOrDefaultAsync<string?>(
            "select case when coalesce(nullif(secret_name,''), '') <> '' then secret_name else 'tenant-' || @m || '-config' end from tenants where merchantaccount=@m",
            new { m = merchantAccount });
        Console.WriteLine($"webhook: resolved secretName for merchant '{merchantAccount}' => '{(secretName ?? "<none>")}'");
        if (string.IsNullOrWhiteSpace(secretName)) return null;

        try
        {
            var res = await _sm.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });
            Console.WriteLine($"webhook: retrieved secret '{secretName}' for merchant '{merchantAccount}'");
            using var doc = JsonDocument.Parse(res.SecretString);
            if (doc.RootElement.TryGetProperty("adyenHmacKey", out var hk))
            {
                var key = hk.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    Console.WriteLine($"webhook: adyenHmacKey found for merchant '{merchantAccount}', caching");
                    _hmacCache[merchantAccount] = key!;
                    return key;
                }
                else
                {
                    Console.WriteLine($"webhook: adyenHmacKey property empty for merchant '{merchantAccount}'");
                }
            }
            else
            {
                Console.WriteLine($"webhook: adyenHmacKey not present in secret '{secretName}' for merchant '{merchantAccount}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"webhook: failed to read secret '{secretName}' for merchant '{merchantAccount}': {ex.Message}");
            // fall back decision is made by caller based on _requireValidHmac
        }
        Console.WriteLine($"webhook: returning null HMAC for merchant '{merchantAccount}'");
        return null;
    }

    private static async Task<string> BuildPostgresConnectionStringFromSecretAsync(string secretName)
    {
        var res = await _sm.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });
        using var doc = JsonDocument.Parse(res.SecretString);
        var root = doc.RootElement;
        var host = root.GetProperty("host").GetString();
        var username = root.GetProperty("username").GetString();
        var password = root.GetProperty("password").GetString();
        var port = root.TryGetProperty("port", out var p) ? p.GetInt32() : 5432;
        var dbname = root.TryGetProperty("dbname", out var d) ? d.GetString() : root.TryGetProperty("dbName", out var d2) ? d2.GetString() : null;
        return $"Host={host};Username={username};Password={password};Port={port};Database={dbname}";
    }
}

static class Log
{
    public static void Info(ILambdaContext ctx, string msg) => ctx.Logger.LogLine($"INFO  {msg}");
    public static void Warn(ILambdaContext ctx, string msg) => ctx.Logger.LogLine($"WARN  {msg}");
    public static void Error(ILambdaContext ctx, string msg) => ctx.Logger.LogLine($"ERROR {msg}");
}

