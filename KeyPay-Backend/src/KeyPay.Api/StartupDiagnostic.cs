using KeyPay.Infrastructure;
using KeyPay.Application;

namespace KeyPay.Api;

public static class StartupDiagnostic
{
    public static void RunDiagnostics(WebApplication app)
    {
        var logger = app.Logger;
        
        logger.LogInformation("=== STARTUP DIAGNOSTICS ===");
        
        // Test configuration
        try
        {
            var config = app.Configuration;
            logger.LogInformation("Configuration loaded successfully");
            
            var adyenBaseUrl = config["Adyen:BaseUrl"];
            var adyenApiKey = config["Adyen:ApiKey"];
            var connectionString = config.GetConnectionString("Postgres");
            
            logger.LogInformation("Adyen BaseUrl: {BaseUrl}", adyenBaseUrl ?? "NOT_SET");
            logger.LogInformation("Adyen ApiKey: {ApiKey}", string.IsNullOrEmpty(adyenApiKey) ? "NOT_SET" : "SET");
            logger.LogInformation("Connection String: {ConnectionString}", string.IsNullOrEmpty(connectionString) ? "NOT_SET" : "SET");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Configuration test failed");
        }
        
        // Test service resolution
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetService<Db>();
            logger.LogInformation("Db service resolution: {Status}", db != null ? "SUCCESS" : "FAILED");
            
            var adyenClient = scope.ServiceProvider.GetService<IAdyenClient>();
            logger.LogInformation("AdyenClient service resolution: {Status}", adyenClient != null ? "SUCCESS" : "FAILED");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Service resolution test failed");
        }
        
        // Test database connection
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            using var connection = db.Open();
            logger.LogInformation("Database connection: SUCCESS");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database connection test failed");
        }
        
        logger.LogInformation("=== END DIAGNOSTICS ===");
    }
}
