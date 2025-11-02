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
        var rdsSecretName = Environment.GetEnvironmentVariable("Rds__SecretName");
        if (!string.IsNullOrWhiteSpace(rdsSecretName))
        {
            try
            {
                var cs = BuildPostgresConnectionStringFromSecretAsync(rdsSecretName).GetAwaiter().GetResult();
                _postgresConnectionString = cs;
            }
            catch
            {
                _postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
            }
        }
        else
        {
            _postgresConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        }
        _jwtSecretName = Environment.GetEnvironmentVariable("Jwt__SecretName") ?? "keypay/jwt";
        _secretsManager = new AmazonSecretsManagerClient();
    }

    public async Task<APIGatewayCustomAuthorizerResponse> FunctionHandler(APIGatewayCustomAuthorizerRequest request, ILambdaContext context)
    {
        var principalId = "anonymous";
        var effect = "Deny";
        var resource = request.MethodArn ?? "*";

        try
        {
            var headers = request.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var path = GetPathFromMethodArn(request.MethodArn) ?? "/";
            var method = GetMethodFromMethodArn(request.MethodArn) ?? "";

            // Bypass webhook
            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/webhook", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/token/exchange", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                effect = "Allow";
                return Policy(principalId, effect, resource);
            }

            var sourceIp = GetSourceIp(request) ?? string.Empty;

            // /paymentmethods: require x-api-key
            if (path.EndsWith("/paymentmethods", StringComparison.OrdinalIgnoreCase))
            {
                if (!headers.TryGetValue("x-api-key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
                {
                    return Policy(principalId, "Deny", resource);
                }

                var tenant = await GetTenantByApiKeyAsync(apiKey);
                if (tenant is null)
                {
                    return Policy(principalId, "Deny", resource);
                }

                var secretName = string.IsNullOrWhiteSpace(tenant.Value.SecretName)
                    ? $"tenant-{tenant.Value.MerchantAccount}-config"
                    : tenant.Value.SecretName;
                var ipAllowlist = await GetTenantIpAllowlistAsync(secretName);
                if (!IsIpAllowed(sourceIp, ipAllowlist))
                {
                    return Policy(principalId, "Deny", resource);
                }

                effect = "Allow";
                return Policy(principalId, effect, resource);
            }

            // All other routes: require JWT
            if (!headers.TryGetValue("Authorization", out var auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
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
                return Policy(principalId, "Deny", resource);
            }

            var tenantGuid = Guid.Parse(tenantIdStr);
            var tenant2 = await GetTenantByIdAsync(tenantGuid);
            if (tenant2 is null)
            {
                return Policy(principalId, "Deny", resource);
            }

            var secretName2 = string.IsNullOrWhiteSpace(tenant2.Value.SecretName)
                ? $"tenant-{tenant2.Value.MerchantAccount}-config"
                : tenant2.Value.SecretName;
            var ipAllowlist2 = await GetTenantIpAllowlistAsync(secretName2);
            if (!IsIpAllowed(sourceIp, ipAllowlist2))
            {
                return Policy(principalId, "Deny", resource);
            }

            // Defense-in-depth: ensure merchantAccount claim matches DB
            if (!string.Equals(merchant, tenant2.Value.MerchantAccount, StringComparison.OrdinalIgnoreCase))
            {
                return Policy(principalId, "Deny", resource);
            }

            effect = "Allow";
            return Policy(principalId, effect, resource);
        }
        catch
        {
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

    private static string? GetSourceIp(APIGatewayCustomAuthorizerRequest request)
    {
        var headers = request.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers.TryGetValue("X-Forwarded-For", out var xff))
        {
            var first = xff.Split(',').FirstOrDefault()?.Trim();
            return first;
        }
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
        var host = root.GetProperty("host").GetString();
        var username = root.GetProperty("username").GetString();
        var password = root.GetProperty("password").GetString();
        var port = root.TryGetProperty("port", out var p) ? p.GetInt32() : 5432;
        var dbname = root.TryGetProperty("dbname", out var d) ? d.GetString() : root.TryGetProperty("dbName", out var d2) ? d2.GetString() : null;
        return $"Host={host};Username={username};Password={password};Port={port};Database={dbname}";
    }
}


