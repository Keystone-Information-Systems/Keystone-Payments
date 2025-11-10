using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace KeyPay.Authorizer;

public class Function
{
    private readonly string? _postgresConnectionString;
    private readonly string _jwtSecretName;
    private readonly IAmazonSecretsManager _secretsManager;

    public Function()
    {
        _secretsManager = new AmazonSecretsManagerClient(); // initialize first

        var rdsSecretName = Environment.GetEnvironmentVariable("Rds__SecretName");
        if (!string.IsNullOrWhiteSpace(rdsSecretName))
        {
            try
            {
                _postgresConnectionString =
                    BuildPostgresConnectionStringFromSecretAsync(rdsSecretName).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"authorizer: RDS secret fallback: {ex.Message}");
                _postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
            }
        }
        else
        {
            _postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        }

        _jwtSecretName = Environment.GetEnvironmentVariable("Jwt__SecretName") ?? "keypay/jwt";
    }

    public async Task<APIGatewayCustomAuthorizerResponse> FunctionHandler(APIGatewayCustomAuthorizerRequest request, ILambdaContext context)
    {
        var principalId = "anonymous";
        var effect = "Deny";
        var resource = request.MethodArn ?? "*";

        try
        {
            var headers = new Dictionary<string, string>(request.Headers ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            var path = GetPathFromMethodArn(request.MethodArn) ?? "/";
            var method = GetMethodFromMethodArn(request.MethodArn) ?? "";
            var sourceIp = GetSourceIp(request) ?? string.Empty;
            Console.WriteLine($"authorizer: {method} {path} ip={sourceIp}");

            // Bypass webhook and preflights/token
            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/webhook", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/token/exchange", StringComparison.OrdinalIgnoreCase)) ||
                path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                effect = "Allow";
                return Policy(principalId, effect, resource);
            }

            // /paymentmethods: require x-api-key and merchantAccount (from header or query); verify key in tenant secret; enforce IP allowlist
            if (path.EndsWith("/paymentmethods", StringComparison.OrdinalIgnoreCase))
            {
                if (!headers.TryGetValue("x-api-key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
                {
                    Console.WriteLine("authorizer: deny: missing x-api-key");
                    return Policy(principalId, "Deny", resource);
                }

                var merchantAccount = GetMerchantAccountFromRequest(request);
                if (string.IsNullOrWhiteSpace(merchantAccount))
                {
                    Console.WriteLine("authorizer: deny: missing merchantAccount (x-merchant-account header or merchantAccount query)");
                    return Policy(principalId, "Deny", resource);
                }

                var tenant = await GetTenantByMerchantAsync(merchantAccount!);
                if (tenant is null)
                {
                    Console.WriteLine($"authorizer: deny: tenant not found by merchantAccount '{merchantAccount}'");
                    return Policy(principalId, "Deny", resource);
                }

                var secretName = string.IsNullOrWhiteSpace(tenant.Value.SecretName)
                    ? $"tenant-{tenant.Value.MerchantAccount}-config"
                    : tenant.Value.SecretName;

                try
                {
                    var sec = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });
                    using var doc = JsonDocument.Parse(sec.SecretString);
                    if (!ApiKeyExists(doc.RootElement, apiKey))
                    {
                        Console.WriteLine("authorizer: deny: x-api-key not in tenant secret");
                        return Policy(principalId, "Deny", resource);
                    }

                    var ipAllowlist = await GetTenantIpAllowlistAsync(secretName);
                    if (!IsIpAllowed(sourceIp, ipAllowlist))
                    {
                        Console.WriteLine($"authorizer: deny: ip not allowed ({sourceIp})");
                        return Policy(principalId, "Deny", resource);
                    }

                    Console.WriteLine($"authorizer: allow: tenant={tenant.Value.TenantId} merchant={tenant.Value.MerchantAccount}");
                    principalId = tenant.Value.MerchantAccount;
                    return Policy(principalId, "Allow", resource);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"authorizer: deny: failed to read tenant secret: {ex.Message}");
                    return Policy(principalId, "Deny", resource);
                }
            }

            // All other routes: require JWT
            if (!headers.TryGetValue("Authorization", out var auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("authorizer: deny: missing/invalid JWT header");
                return Policy(principalId, "Deny", resource);
            }

            var token = auth.Substring("Bearer ".Length).Trim();
            var jwtSecret = await GetJwtSettingsAsync();
            var principal = ValidateJwt(token, jwtSecret);
            principalId = principal.Identity?.Name ?? "tenant";

            var tenantIdStr = principal.FindFirst("tenantId")?.Value;
            var merchant = principal.FindFirst("merchantAccount")?.Value;
            if (string.IsNullOrWhiteSpace(tenantIdStr) || string.IsNullOrWhiteSpace(merchant))
            {
                Console.WriteLine("authorizer: deny: missing claims tenantId/merchantAccount");
                return Policy(principalId, "Deny", resource);
            }

            var tenantGuid = Guid.Parse(tenantIdStr);
            var tenant2 = await GetTenantByIdAsync(tenantGuid);
            if (tenant2 is null)
            {
                Console.WriteLine("authorizer: deny: tenantId not found in DB");
                return Policy(principalId, "Deny", resource);
            }

            var secretName2 = string.IsNullOrWhiteSpace(tenant2.Value.SecretName)
                ? $"tenant-{tenant2.Value.MerchantAccount}-config"
                : tenant2.Value.SecretName;
            var ipAllowlist2 = await GetTenantIpAllowlistAsync(secretName2);
            if (!IsIpAllowed(sourceIp, ipAllowlist2))
            {
                Console.WriteLine($"authorizer: deny: ip not allowed ({sourceIp})");
                return Policy(principalId, "Deny", resource);
            }

            // Defense-in-depth: ensure merchantAccount claim matches DB
            if (!string.Equals(merchant, tenant2.Value.MerchantAccount, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("authorizer: deny: merchant claim does not match DB");
                return Policy(principalId, "Deny", resource);
            }

            Console.WriteLine($"authorizer: allow: tenant={tenant2.Value.TenantId} merchant={tenant2.Value.MerchantAccount}");
            effect = "Allow";
            return Policy(principalId, effect, resource);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"authorizer: exception: {ex.Message}");
            return Policy(principalId, "Deny", resource);
        }
    }

    private static APIGatewayCustomAuthorizerResponse Policy(string principalId, string effect, string resource)
    {
        return new APIGatewayCustomAuthorizerResponse
        {
            PrincipalID = principalId,
            PolicyDocument = new APIGatewayCustomAuthorizerPolicy
            {
                Version = "2012-10-17",
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                {
                    new()
                    {
                        Action = new HashSet<string> { "execute-api:Invoke" },
                        Effect = effect,
                        Resource = new HashSet<string> { resource }
                    }
                }
            },
            Context = null
        };
    }

    private static string? GetPathFromMethodArn(string? methodArn)
    {
        // arn:aws:execute-api:{region}:{account}:{apiId}/{stage}/{method}/{path}
        if (string.IsNullOrEmpty(methodArn)) return null;
        var parts = methodArn.Split(':');
        if (parts.Length < 6) return null;
        var resource = parts[5];
        var resParts = resource.Split('/');
        if (resParts.Length < 4) return "/";
        var pathParts = resParts.Skip(3);
        return "/" + string.Join('/', pathParts);
    }

    private static string? GetMethodFromMethodArn(string? methodArn)
    {
        if (string.IsNullOrEmpty(methodArn)) return null;
        var parts = methodArn.Split(':');
        if (parts.Length < 6) return null;
        var resource = parts[5];
        var resParts = resource.Split('/');
        if (resParts.Length < 3) return null;
        return resParts[2];
    }

    private static string? GetSourceIp(APIGatewayCustomAuthorizerRequest? request)
    {
        var ip = request?.RequestContext?.Identity?.SourceIp;
        if (!string.IsNullOrWhiteSpace(ip)) return ip;

        var headers = request?.Headers != null
            ? new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers.TryGetValue("X-Forwarded-For", out var xff))
            return xff.Split(',').FirstOrDefault()?.Trim();

        return null;
    }

    // NEW: merchantAccount from header or query
    private static string? GetMerchantAccountFromRequest(APIGatewayCustomAuthorizerRequest request)
    {
        var headers = request.Headers != null
            ? new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers.TryGetValue("x-merchant-account", out var fromHeader) && !string.IsNullOrWhiteSpace(fromHeader))
            return fromHeader.Trim();
        var qs = request.QueryStringParameters != null
            ? new Dictionary<string, string>(request.QueryStringParameters, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (qs.TryGetValue("merchantAccount", out var fromQs) && !string.IsNullOrWhiteSpace(fromQs))
            return fromQs.Trim();
        return null;
    }

    private async Task<(Guid TenantId, string SecretName, string MerchantAccount)?> GetTenantByApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(_postgresConnectionString)) return null;
        await using var conn = new NpgsqlConnection(_postgresConnectionString);
        await conn.OpenAsync();
        const string sql = "select tenantid, secret_name, merchantaccount from tenants where apikey = @apiKey";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("apiKey", apiKey);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
        }
        return null;
    }

    private async Task<(Guid TenantId, string SecretName, string MerchantAccount)?> GetTenantByIdAsync(Guid tenantId)
    {
        if (string.IsNullOrEmpty(_postgresConnectionString)) return null;
        await using var conn = new NpgsqlConnection(_postgresConnectionString);
        await conn.OpenAsync();
        const string sql = "select tenantid, secret_name, merchantaccount from tenants where tenantid = @tenantId";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
        }
        return null;
    }

    // NEW: tenant by merchantAccount
    private async Task<(Guid TenantId, string SecretName, string MerchantAccount)?> GetTenantByMerchantAsync(string merchantAccount)
    {
        if (string.IsNullOrEmpty(_postgresConnectionString)) return null;
        await using var conn = new NpgsqlConnection(_postgresConnectionString);
        await conn.OpenAsync();
        const string sql = "select tenantid, secret_name, merchantaccount from tenants where merchantaccount = @m limit 1";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("m", merchantAccount);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
        }
        return null;
    }

    private async Task<string[]> GetTenantIpAllowlistAsync(string secretName)
    {
        var req = new GetSecretValueRequest { SecretId = secretName };
        var res = await _secretsManager.GetSecretValueAsync(req);
        var json = res.SecretString;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var ips = new List<string>();
        if (root.TryGetProperty("ipAllowlist", out var ipArr) && ipArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ipArr.EnumerateArray())
            {
                var v = el.GetString();
                if (!string.IsNullOrWhiteSpace(v)) ips.Add(v);
            }
        }
        return ips.ToArray();
    }

    private async Task<string> GetJwtSettingsAsync()
    {
        var req = new GetSecretValueRequest { SecretId = _jwtSecretName };
        var res = await _secretsManager.GetSecretValueAsync(req);
        // Secret may be a plain string or JSON {"secret":"..."}
        var val = res.SecretString;
        try
        {
            using var doc = JsonDocument.Parse(val);
            if (doc.RootElement.TryGetProperty("secret", out var s)) return s.GetString() ?? "";
        }
        catch { }
        return val;
    }

    private static ClaimsPrincipal ValidateJwt(string token, string secret)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(60)
        };
        return tokenHandler.ValidateToken(token, parameters, out _);
    }

    private static bool IsIpAllowed(string? sourceIp, IEnumerable<string> allowlist)
    {
        if (string.IsNullOrWhiteSpace(sourceIp)) return false;
        foreach (var entry in allowlist)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            if (entry.Contains('/'))
            {
                if (IsInCidr(sourceIp, entry)) return true;
            }
            else if (string.Equals(entry.Trim(), sourceIp.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    // NEW: check if apiKey exists in secret JSON
    private static bool ApiKeyExists(JsonElement root, string apiKey)
    {
        if (root.TryGetProperty("apiKey", out var single) && string.Equals(single.GetString(), apiKey, StringComparison.Ordinal))
            return true;
        if (root.TryGetProperty("apiKeys", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                if (string.Equals(el.GetString(), apiKey, StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }

    private static bool IsInCidr(string ip, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            var baseIp = IPAddress.Parse(parts[0]);
            var prefix = int.Parse(parts[1]);
            var ipBytes = IPAddress.Parse(ip).GetAddressBytes();
            var baseBytes = baseIp.GetAddressBytes();
            if (ipBytes.Length != baseBytes.Length) return false;
            int fullBytes = prefix / 8;
            int remainingBits = prefix % 8;
            for (int i = 0; i < fullBytes; i++)
            {
                if (ipBytes[i] != baseBytes[i]) return false;
            }
            if (remainingBits > 0)
            {
                int mask = (byte)~(255 >> remainingBits);
                if ((ipBytes[fullBytes] & mask) != (baseBytes[fullBytes] & mask)) return false;
            }
            return true;
        }
        catch { return false; }
    }

    private async Task<string> BuildPostgresConnectionStringFromSecretAsync(string secretName)
    {
        var res = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });
        using var doc = JsonDocument.Parse(res.SecretString);
        var root = doc.RootElement;

        string? GetStr(string name) => root.TryGetProperty(name, out var v) ? v.GetString() : null;

        var host = GetStr("host");
        var username = GetStr("username");
        var password = GetStr("password");
        var port = root.TryGetProperty("port", out var p) ? p.GetInt32() : 5432;
        var dbname = GetStr("dbname") ?? GetStr("dbName") ?? "postgres";

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new Exception("RDS secret missing required fields");

        return $"Host={host};Username={username};Password={password};Port={port};Database={dbname}";
    }
}


