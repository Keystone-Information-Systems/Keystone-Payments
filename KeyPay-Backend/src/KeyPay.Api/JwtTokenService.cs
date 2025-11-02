using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KeyPay.Api;

public sealed class JwtTokenService
{
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly string _secretName;

    public JwtTokenService(IAmazonSecretsManager secretsManager, IConfiguration config)
    {
        _secretsManager = secretsManager;
        _secretName = config["Jwt:SecretName"] ?? "keypay/jwt";
    }

    public async Task<string> IssueAsync(Guid tenantId, string merchantAccount, TimeSpan lifetime, CancellationToken ct)
    {
        var secret = await GetSecretAsync(ct);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            claims: new List<Claim>
            {
                new("sub", tenantId.ToString()),
                new("tenantId", tenantId.ToString()),
                new("merchantAccount", merchantAccount),
                new("jti", Guid.NewGuid().ToString())
            },
            notBefore: now.AddSeconds(-30),
            expires: now.Add(lifetime),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GetSecretAsync(CancellationToken ct)
    {
        var res = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _secretName }, ct);
        var val = res.SecretString;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(val);
            if (doc.RootElement.TryGetProperty("secret", out var s)) return s.GetString() ?? string.Empty;
        }
        catch { }
        return val;
    }
}


