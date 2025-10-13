using Npgsql;
using System.Net;

namespace ValPay.Infrastructure.Exceptions;

public static class PostgresExceptionHandler
{
    public static (HttpStatusCode StatusCode, string UserMessage, string TechnicalMessage) HandlePostgresException(NpgsqlException ex)
    {
        return ex.SqlState switch
        {
            // Connection issues
            "08000" or "08003" or "08006" => (
                HttpStatusCode.ServiceUnavailable,
                "Database connection is temporarily unavailable. Please try again later.",
                $"Connection error: {ex.Message}"
            ),
            
            // Authentication failures
            "28P01" => (
                HttpStatusCode.InternalServerError,
                "Database authentication failed. Please contact support.",
                $"Authentication error: {ex.Message}"
            ),
            
            // Authorization failures
            "42501" => (
                HttpStatusCode.Forbidden,
                "Insufficient database permissions.",
                $"Authorization error: {ex.Message}"
            ),
            
            // Data integrity violations
            "23505" => ( // Unique violation
                HttpStatusCode.Conflict,
                "A record with this information already exists.",
                $"Unique constraint violation: {ex.Message}"
            ),
            "23503" => ( // Foreign key violation
                HttpStatusCode.BadRequest,
                "Referenced record does not exist.",
                $"Foreign key constraint violation: {ex.Message}"
            ),
            "23502" => ( // Not null violation
                HttpStatusCode.BadRequest,
                "Required field is missing.",
                $"Not null constraint violation: {ex.Message}"
            ),
            "23514" => ( // Check constraint violation
                HttpStatusCode.BadRequest,
                "Invalid data provided.",
                $"Check constraint violation: {ex.Message}"
            ),
            
            // Data type errors
            "22P02" => ( // Invalid text representation
                HttpStatusCode.BadRequest,
                "Invalid data format provided.",
                $"Invalid data type: {ex.Message}"
            ),
            "42804" => ( // Datatype mismatch
                HttpStatusCode.BadRequest,
                "Data type mismatch.",
                $"Datatype mismatch: {ex.Message}"
            ),
            
            // Syntax errors (shouldn't happen in production)
            "42601" => ( // Syntax error
                HttpStatusCode.InternalServerError,
                "Database query syntax error. Please contact support.",
                $"SQL syntax error: {ex.Message}"
            ),
            
            // Resource issues
            "53100" => ( // Disk full
                HttpStatusCode.ServiceUnavailable,
                "Database storage is full. Please contact support.",
                $"Disk full: {ex.Message}"
            ),
            "53200" => ( // Out of memory
                HttpStatusCode.ServiceUnavailable,
                "Database is out of memory. Please try again later.",
                $"Out of memory: {ex.Message}"
            ),
            
            // Lock issues
            "40001" => ( // Serialization failure
                HttpStatusCode.Conflict,
                "Transaction conflict. Please try again.",
                $"Serialization failure: {ex.Message}"
            ),
            "40P01" => ( // Deadlock detected
                HttpStatusCode.Conflict,
                "Database deadlock detected. Please try again.",
                $"Deadlock: {ex.Message}"
            ),
            
            // Default case
            _ => (
                HttpStatusCode.InternalServerError,
                "A database error occurred. Please try again later.",
                $"PostgreSQL error [{ex.SqlState}]: {ex.Message}"
            )
        };
    }
    
    public static bool IsTransientError(NpgsqlException ex)
    {
        return ex.SqlState switch
        {
            // Connection issues - transient
            "08000" or "08003" or "08006" => true,
            
            // Resource issues - transient
            "53100" or "53200" => true,
            
            // Lock issues - transient
            "40001" or "40P01" => true,
            
            // Default - not transient
            _ => false
        };
    }
    
    public static bool IsRetryableError(NpgsqlException ex)
    {
        return IsTransientError(ex);
    }
}
