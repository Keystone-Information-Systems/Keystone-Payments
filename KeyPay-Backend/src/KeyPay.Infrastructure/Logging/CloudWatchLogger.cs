using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace KeyPay.Infrastructure.Logging;

public class CloudWatchLogger : ILogger
{
    private readonly string _categoryName;
    private readonly CloudWatchLoggerProvider _provider;

    public CloudWatchLogger(string categoryName, CloudWatchLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Level = logLevel.ToString(),
            Category = _categoryName,
            EventId = eventId.Id,
            Message = message,
            CorrelationId = correlationId,
            Exception = exception?.ToString(),
            Properties = state as IReadOnlyList<KeyValuePair<string, object?>>
        };

        // In Lambda, this will automatically go to CloudWatch
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(logEntry));
    }
}

public class CloudWatchLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new CloudWatchLogger(categoryName, this);
    }

    public void Dispose() { }
}
