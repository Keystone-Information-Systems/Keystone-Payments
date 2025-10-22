# PostgreSQL Exception Handling Guide

This document explains how PostgreSQL exceptions are handled in the ValPay backend application.

## Overview

The application now includes comprehensive PostgreSQL exception handling with:
- Specific error code mapping to user-friendly messages
- Automatic retry logic for transient errors
- Circuit breaker patterns for resilience
- Detailed logging and monitoring

## Components

### 1. PostgresExceptionHandler

Located in `src/KeyPay.Infrastructure/Exceptions/PostgresExceptionHandler.cs`

This class provides:
- **Error Code Mapping**: Maps PostgreSQL SQL state codes to appropriate HTTP status codes and user messages
- **Transient Error Detection**: Identifies which errors are transient and can be retried
- **Retry Logic**: Determines which errors should trigger automatic retries

#### Common PostgreSQL Error Codes Handled:

| SQL State | Description | HTTP Status | User Message |
|-----------|-------------|-------------|--------------|
| `08000`, `08003`, `08006` | Connection issues | 503 Service Unavailable | Database connection is temporarily unavailable |
| `28P01` | Authentication failure | 500 Internal Server Error | Database authentication failed |
| `42501` | Authorization failure | 403 Forbidden | Insufficient database permissions |
| `23505` | Unique constraint violation | 409 Conflict | A record with this information already exists |
| `23503` | Foreign key violation | 400 Bad Request | Referenced record does not exist |
| `23502` | Not null violation | 400 Bad Request | Required field is missing |
| `23514` | Check constraint violation | 400 Bad Request | Invalid data provided |
| `22P02` | Invalid text representation | 400 Bad Request | Invalid data format provided |
| `40001` | Serialization failure | 409 Conflict | Transaction conflict. Please try again |
| `40P01` | Deadlock detected | 409 Conflict | Database deadlock detected. Please try again |

### 2. Enhanced Error Handling Middleware

The `ErrorHandlingMiddleware` now specifically handles `NpgsqlException` instances and provides:
- PostgreSQL-specific error responses
- SQL state information in development mode
- Transient error indicators
- Detailed technical information for debugging

### 3. Database Layer Resilience

The `Db` class now includes:
- **Automatic Retry Logic**: Retries transient errors with exponential backoff
- **Connection Management**: Proper connection lifecycle management
- **Error Propagation**: Non-transient errors are properly propagated

### 4. Resilience Policies

Located in `src/KeyPay.Infrastructure/Resilience/PostgresResiliencePolicies.cs`

Provides Polly-based resilience patterns:
- **Retry Policy**: Automatic retries for transient errors with exponential backoff
- **Timeout Policy**: Prevents hanging operations
- **Combined Policies**: Wrapped policies for comprehensive error handling

## Usage Examples

### Basic Database Operation with Retry

```csharp
public async Task CreateTransactionAsync(TransactionData data)
{
    await ExecuteWithRetryAsync(async () =>
    {
        using var connection = db.Open();
        await connection.ExecuteAsync(sql, data);
    });
}
```

### Using Resilience Policies

```csharp
var policy = PostgresResiliencePolicies.GetCombinedDatabasePolicy();
await policy.ExecuteAsync(async () =>
{
    using var connection = db.Open();
    return await connection.QueryAsync<Transaction>(sql, parameters);
});
```

### Error Response Example

When a PostgreSQL error occurs, the API returns:

```json
{
  "error": {
    "message": "A record with this information already exists.",
    "correlationId": "12345678-1234-1234-1234-123456789012",
    "timestamp": "2024-01-15T10:30:00Z",
    "type": "PostgresException",
    "sqlState": "23505",
    "isTransient": false,
    "details": {
      "stackTrace": "...",
      "technicalMessage": "Unique constraint violation: duplicate key value violates unique constraint..."
    }
  }
}
```

## Configuration

### Connection String

Ensure your PostgreSQL connection string includes appropriate timeout settings:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=KeyPay;Username=postgres;Password=password;Timeout=30;CommandTimeout=30;"
  }
}
```

### Retry Configuration

You can customize retry behavior by modifying the retry parameters in the `Db` class:

```csharp
private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    // Customize maxRetries as needed
}
```

## Monitoring and Logging

### Log Levels

- **Information**: Successful operations and retry attempts
- **Warning**: Non-critical errors that are handled gracefully
- **Error**: Critical errors that require attention

### Key Metrics to Monitor

1. **Retry Rates**: High retry rates may indicate connection issues
2. **Circuit Breaker Activations**: May indicate database performance problems
3. **Error Code Distribution**: Helps identify common failure patterns
4. **Response Times**: Database operation latency

### Example Log Entries

```
[INFO] Database retry 1 after 200ms. SQL State: 40001, Message: serialization_failure
[WARN] Database circuit breaker opened for 30 seconds. SQL State: 08006, Message: connection_failure
[ERROR] PostgreSQL error [23505]: duplicate key value violates unique constraint
```

## Best Practices

1. **Always Use Retry Logic**: For operations that can be safely retried
2. **Monitor Error Patterns**: Track which error codes occur most frequently
3. **Set Appropriate Timeouts**: Prevent hanging operations
4. **Use Circuit Breakers**: Protect against cascading failures
5. **Log Sufficient Context**: Include correlation IDs and SQL states
6. **Handle Idempotency**: Ensure retried operations are safe to repeat

## Troubleshooting

### Common Issues

1. **High Retry Rates**
   - Check database connection pool settings
   - Verify network connectivity
   - Monitor database performance

2. **Circuit Breaker Activations**
   - Check database resource usage
   - Review query performance
   - Consider scaling database resources

3. **Authentication Errors**
   - Verify connection string credentials
   - Check database user permissions
   - Ensure database is accessible

### Debug Mode

In development mode, the API returns detailed error information including:
- Full stack traces
- SQL state codes
- Technical error messages
- Inner exception details

This information is hidden in production for security reasons.
