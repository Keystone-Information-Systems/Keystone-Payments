using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KeyPay.Infrastructure.Secrets;

public interface ISecretsManager
{
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
    Task<T> GetSecretAsync<T>(string secretName, CancellationToken cancellationToken = default);
}

public class AwsSecretsManager : ISecretsManager
{
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<AwsSecretsManager> _logger;

    public AwsSecretsManager(IAmazonSecretsManager secretsManager, ILogger<AwsSecretsManager> logger)
    {
        _secretsManager = secretsManager;
        _logger = logger;
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetSecretValueRequest
            {
                SecretId = secretName
            };

            var response = await _secretsManager.GetSecretValueAsync(request, cancellationToken);
            return response.SecretString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretName}", secretName);
            throw;
        }
    }

    public async Task<T> GetSecretAsync<T>(string secretName, CancellationToken cancellationToken = default)
    {
        var secretString = await GetSecretAsync(secretName, cancellationToken);
        return JsonSerializer.Deserialize<T>(secretString) ?? throw new InvalidOperationException($"Failed to deserialize secret {secretName}");
    }
}
