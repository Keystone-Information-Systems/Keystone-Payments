using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Dapper;

namespace ValPay.Infrastructure.Idempotency;

public interface IIdempotencyService
{
    Task<string> GenerateKeyAsync(string requestData, CancellationToken cancellationToken = default);
    Task<bool> IsDuplicateAsync(string key, CancellationToken cancellationToken = default);
    Task StoreKeyAsync(string key, string response, CancellationToken cancellationToken = default);
    Task<string?> GetResponseAsync(string key, CancellationToken cancellationToken = default);
}

public class IdempotencyService : IIdempotencyService
{
    private readonly Db _db;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(Db db, ILogger<IdempotencyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<string> GenerateKeyAsync(string requestData, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(requestData));
        return Task.FromResult(Convert.ToBase64String(hash));
    }

    public async Task<bool> IsDuplicateAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = _db.Open();
        var count = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM idempotency_keys WHERE key = @key AND expires_at > NOW()",
            new { key });
        return count > 0;
    }

    public async Task StoreKeyAsync(string key, string response, CancellationToken cancellationToken = default)
    {
        using var connection = _db.Open();
        await connection.ExecuteAsync(
            "INSERT INTO idempotency_keys (key, response, expires_at) VALUES (@key, @response, NOW() + INTERVAL '1 hour') ON CONFLICT (key) DO UPDATE SET response = EXCLUDED.response, expires_at = EXCLUDED.expires_at",
            new { key, response });
    }

    public async Task<string?> GetResponseAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = _db.Open();
        return await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT response FROM idempotency_keys WHERE key = @key AND expires_at > NOW()",
            new { key });
    }
}
