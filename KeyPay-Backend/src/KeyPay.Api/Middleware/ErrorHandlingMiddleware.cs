using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Npgsql;
using KeyPay.Infrastructure.Exceptions;

namespace KeyPay.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        context.Items["CorrelationId"] = correlationId;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. CorrelationId: {CorrelationId}. Exception Type: {ExceptionType}. Message: {Message}. StackTrace: {StackTrace}", 
                correlationId, ex.GetType().Name, ex.Message, ex.StackTrace);
            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";
        
        var isDevelopment = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        
        object response;
        
        // Handle PostgreSQL exceptions specifically
        if (exception is NpgsqlException npgsqlEx)
        {
            var (statusCode, userMessage, technicalMessage) = PostgresExceptionHandler.HandlePostgresException(npgsqlEx);
            context.Response.StatusCode = (int)statusCode;
            
            response = new
            {
                error = new
                {
                    message = isDevelopment ? technicalMessage : userMessage,
                    correlationId = correlationId,
                    timestamp = DateTime.UtcNow,
                    type = "PostgresException",
                    sqlState = npgsqlEx.SqlState,
                    isTransient = PostgresExceptionHandler.IsTransientError(npgsqlEx),
                    details = isDevelopment ? new
                    {
                        stackTrace = exception.StackTrace,
                        innerException = exception.InnerException?.Message,
                        source = exception.Source,
                        technicalMessage = technicalMessage
                    } : null
                }
            };
        }
        else
        {
            context.Response.StatusCode = exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                NotImplementedException => (int)HttpStatusCode.NotImplemented,
                _ => (int)HttpStatusCode.InternalServerError
            };
            
            response = new
            {
                error = new
                {
                    message = isDevelopment ? exception.Message : "An error occurred while processing your request",
                    correlationId = correlationId,
                    timestamp = DateTime.UtcNow,
                    type = exception.GetType().Name,
                    details = isDevelopment ? new
                    {
                        stackTrace = exception.StackTrace,
                        innerException = exception.InnerException?.Message,
                        source = exception.Source
                    } : null
                }
            };
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
    }
}
