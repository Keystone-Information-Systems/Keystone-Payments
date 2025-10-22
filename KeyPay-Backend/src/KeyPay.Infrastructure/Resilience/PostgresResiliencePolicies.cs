using Npgsql;
using Polly;
using KeyPay.Infrastructure.Exceptions;

namespace KeyPay.Infrastructure.Resilience;

public static class PostgresResiliencePolicies
{
    public static IAsyncPolicy GetDatabaseRetryPolicy(int maxRetries = 3)
    {
        return Policy
            .Handle<NpgsqlException>(ex => PostgresExceptionHandler.IsRetryableError(ex))
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100));
    }

    public static IAsyncPolicy GetDatabaseTimeoutPolicy(int timeoutInSeconds = 30)
    {
        return Policy.TimeoutAsync(timeoutInSeconds);
    }

    public static IAsyncPolicy GetCombinedDatabasePolicy()
    {
        return Policy.WrapAsync(
            GetDatabaseRetryPolicy(),
            GetDatabaseTimeoutPolicy()
        );
    }

    public static IAsyncPolicy<T> GetDatabaseRetryPolicy<T>(int maxRetries = 3)
    {
        return Policy<T>
            .Handle<NpgsqlException>(ex => PostgresExceptionHandler.IsRetryableError(ex))
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100));
    }

    public static IAsyncPolicy<T> GetCombinedDatabasePolicy<T>()
    {
        return Policy.WrapAsync(
            GetDatabaseRetryPolicy<T>(),
            Policy.TimeoutAsync<T>(30)
        );
    }
}