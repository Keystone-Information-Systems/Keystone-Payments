using Amazon.Lambda.AspNetCoreServer.Hosting;
using Dapper;
using KeyPay.Infrastructure;
using KeyPay.Api.Middleware;
using KeyPay.Infrastructure.Validation;
using KeyPay.Infrastructure.Resilience;
using KeyPay.Infrastructure.Idempotency;
using KeyPay.Application;
using System.Diagnostics;
using KeyPay.Api;
using KeyPay.Infrastructure.Secrets;
using Amazon.SecretsManager;
using Npgsql;
 
 

var builder = WebApplication.CreateBuilder(args);

// Configure JSON options for case-insensitive deserialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddLambdaLogger();
});

// Database (prefer RDS secret if provided)
string BuildConnString()
{
	// Helper to treat null/empty/whitespace as "not set"
	static string? NonEmpty(params string?[] values)
		=> values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

	// 1) Find the secret name from config/env
	var rdsSecret = NonEmpty(
		builder.Configuration["Rds:SecretName"],
		builder.Configuration["Rds__SecretName"],
		Environment.GetEnvironmentVariable("RDS_SECRET_NAME")
	);

	// 2) Try Secrets Manager first (if configured)
    if (!string.IsNullOrWhiteSpace(rdsSecret))
    {
        try
        {
			var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
			using var sm = new Amazon.SecretsManager.AmazonSecretsManagerClient(
				Amazon.RegionEndpoint.GetBySystemName(region));

			var res = sm.GetSecretValueAsync(
				new Amazon.SecretsManager.Model.GetSecretValueRequest { SecretId = rdsSecret }
			).GetAwaiter().GetResult();

			var payload = res.SecretString ??
					      (res.SecretBinary != null ? System.Text.Encoding.UTF8.GetString(res.SecretBinary.ToArray()) : null);

			if (string.IsNullOrWhiteSpace(payload))
				throw new InvalidOperationException($"Secret {rdsSecret} has no SecretString/SecretBinary.");

			using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;

			string? host    = root.TryGetProperty("host", out var h) ? h.GetString() : null;
			string? user    = root.TryGetProperty("username", out var u) ? u.GetString() : null;
			string? pass    = root.TryGetProperty("password", out var pw) ? pw.GetString() : null;
			int      port   = root.TryGetProperty("port", out var p) ? p.GetInt32() : 5432;
			string?  dbname = root.TryGetProperty("dbname", out var d) ? d.GetString()
			               : root.TryGetProperty("dbName", out var d2) ? d2.GetString()
			               : null;

			if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
				throw new InvalidOperationException($"Secret {rdsSecret} is missing required keys (host/username/password).");

			var csb = new Npgsql.NpgsqlConnectionStringBuilder
			{
				Host = host,
				Port = port,
				Username = user,
				Password = pass,
				Database = dbname // can be null; Npgsql tolerates but better to include
			};

			return csb.ToString();
        }
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[RDS Secret] Failed to load secret '{rdsSecret}': {ex.Message}");
		}
	}

	// 3) Fallback to ConnectionStrings:Postgres (treat empty as missing)
	var cfgCs = NonEmpty(
		builder.Configuration.GetConnectionString("Postgres"),
		builder.Configuration["ConnectionStrings:Postgres"],
		Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
	);
	if (!string.IsNullOrWhiteSpace(cfgCs))
		return cfgCs;

	// 4) Last resort dev default
	return "Host=localhost;Username=postgres;Password=postgres;Database=KeyPay";
}
var connectionString = BuildConnString();
Console.WriteLine($"[DB] Using connection string: {(string.IsNullOrWhiteSpace(connectionString) ? "(empty)" : "(configured)")}");
builder.Services.AddSingleton(new Db(connectionString));

// Idempotency
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();

// HTTP Client
builder.Services.AddHttpClient<IAdyenClient, AdyenClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Adyen:BaseUrl"] ?? "https://checkout-test.adyen.com");
});

// Context and AWS services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
builder.Services.AddSingleton<ISecretsManager, AwsSecretsManager>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",  // Vite default port
                "http://localhost:5174",  // Alternative Vite port
                "http://localhost:4173",   // Vite preview port
                "https://keypay-front-dev.keyinfosys.com"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
builder.Services.AddSingleton<JwtTokenService>();

var app = builder.Build();

// Per-request: Log effective DB target (host/port/db/user/ssl)
app.Use(async (context, next) =>
{
    try
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("DbTarget");
        logger.LogInformation("DB target => Host:{Host} Port:{Port} Db:{Db} User:{User} SSL:{Ssl}",
            csb.Host, csb.Port, csb.Database, csb.Username, csb.SslMode);
    }
    catch { }
    await next();
});

// Run startup diagnostics
KeyPay.Api.StartupDiagnostic.RunDiagnostics(app);
// Ensure DB indexes (best-effort)
try
{
    var dbForIndex = app.Services.GetRequiredService<Db>();
    dbForIndex.EnsureIndexesAsync(default).GetAwaiter().GetResult();
}
catch { }


// Simple test middleware
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request received: {context.Request.Method} {context.Request.Path}");
    await next();
});
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Use CORS
app.UseCors("AllowFrontend");

// Tenant hydrate middleware: set AdyenApiKey and MerchantAccount from JWT for protected routes
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.Contains("/webhook", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/paymentmethods", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }
    var auth = context.Request.Headers["Authorization"].ToString();
    if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var token = auth.Substring("Bearer ".Length).Trim();
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var merchant = jwt.Claims.FirstOrDefault(c => c.Type == "merchantAccount")?.Value;
            if (!string.IsNullOrWhiteSpace(merchant))
            {
                var db = context.RequestServices.GetRequiredService<Db>();
                var secrets = context.RequestServices.GetRequiredService<KeyPay.Infrastructure.Secrets.ISecretsManager>();
                var info = await db.GetTenantInfoByMerchantAccountAsync(merchant!, context.RequestAborted);
                if (info is not null)
                {
                    var effectiveSecretName = string.IsNullOrWhiteSpace(info.Value.SecretName) ? $"tenant-{merchant}-config" : info.Value.SecretName;
                    var secretJson = await secrets.GetSecretAsync(effectiveSecretName, context.RequestAborted);
                    using var doc = System.Text.Json.JsonDocument.Parse(secretJson);
                    var root = doc.RootElement;
                    string? adyenApiKey = root.TryGetProperty("adyenAPIKey", out var apiKeyEl) ? apiKeyEl.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(adyenApiKey))
                    {
                        context.Items["AdyenApiKey"] = adyenApiKey!;
                        context.Items["MerchantAccount"] = merchant!;
                    }
                }
            }
        }
        catch { }
    }
    await next();
});

// Middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

// Test endpoint
app.MapGet("/api/", () => "KeyPay API is running!");
app.MapGet("/api/health", () => "OK");

// CORS preflight for REST proxy
app.MapMethods("/{**any}", new[] { "OPTIONS" }, () => Results.Ok());

// Correlation ID middleware
app.Use(async (context, next) =>
{
    var corr = context.Request.Headers["X-Correlation-ID"].ToString();
    if (string.IsNullOrWhiteSpace(corr)) corr = Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = corr;
    context.Response.Headers["X-Correlation-ID"] = corr;
    await next();
});

// Cancel payment endpoint
app.MapPost("/api/payments/{id:guid}/cancel", async (Guid id, Db db, ILogger<Program> logger, HttpContext ctx, CancellationToken ct) =>
{
    var correlationId = Guid.NewGuid().ToString();
    logger.LogInformation("Cancel payment request for transaction {TransactionId}. CorrelationId: {CorrelationId}", id, correlationId);

    try
    {
        using var c = db.Open();

        // Get transaction details including tenantId
        var transaction = await c.QuerySingleOrDefaultAsync<(Guid TransactionId, string Status, Guid TenantId)?>("""
            select transactionId, status::text as status, tenantid
            from transactions 
            where transactionId=@id
            """, new { id });

        if (transaction is null)
        {
            logger.LogWarning("Transaction {TransactionId} not found for cancellation. CorrelationId: {CorrelationId}", id, correlationId);
            return Results.NotFound(new { message = "Transaction not found" });
        }

        // Tenant guard
        var jwtTid = GetTenantIdFromJwt(ctx);
        if (jwtTid is null || jwtTid.Value != transaction.Value.TenantId)
        {
            return Results.StatusCode(403);
        }

        // Check if already cancelled
        if (transaction.Value.Status == "Cancelled")
        {
            logger.LogWarning("Transaction {TransactionId} is already cancelled. CorrelationId: {CorrelationId}", id, correlationId);
            return Results.BadRequest(new { message = "Transaction is already cancelled" });
        }

        // Update transaction status to Cancelled (Adyen handles their own cleanup)

        // Update transaction status to Cancelled
        await c.ExecuteAsync("""
            update transactions 
            set status='Cancelled'::payment_status, updatedAt=now()
            where transactionId=@id
            """, new { id });

        // Create operation record
        var operationId = Guid.NewGuid();
        var tenantId = transaction.Value.TenantId;

        await c.ExecuteAsync("""
            insert into operations(operationId, transactionId, tenantId, pspReference, operationType, status, amountValue, currencyCode, rawPayload)
            values (@operationId, @transactionId, @tenantId, @pspReference, @operationType, @status, @amountValue, @currencyCode, @rawPayload::jsonb)
            """, new
        {
            operationId,
            transactionId = id,
            tenantId,
            pspReference = (string?)null,
            operationType = "CANCEL",
            status = "Success",
            amountValue = (long?)null,
            currencyCode = (string?)null,
            rawPayload = "{}"
        });

        logger.LogInformation("Transaction {TransactionId} cancelled successfully. CorrelationId: {CorrelationId}", id, correlationId);
        return Results.Ok(new { message = "Payment cancelled successfully", transactionId = id });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error cancelling payment {TransactionId}. CorrelationId: {CorrelationId}", id, correlationId);
        return Results.Problem("Error cancelling payment");
    }
});

// Removed test /webhooks endpoint; real webhooks are handled by KeyPay.Webhook Lambda

// Debug endpoints
app.MapGet("/api/debug/config", (IConfiguration config) => new
{
    AdyenBaseUrl = config["Adyen:BaseUrl"],
    AdyenApiKey = string.IsNullOrEmpty(config["Adyen:ApiKey"]) ? "NOT_SET" : "SET",
    AdyenMerchantAccount = string.IsNullOrEmpty(config["Adyen:MerchantAccount"]) ? "NOT_SET" : "SET",
    ConnectionString = string.IsNullOrEmpty(config.GetConnectionString("Postgres")) ? "NOT_SET" : "SET",
    TenantId = config["Tenant:Id"],
    FrontendBaseUrl = config["Frontend:BaseUrl"]
});

app.MapGet("/api/debug/db", async (Db db, ILogger<Program> logger) =>
{
    try
    {
        using var connection = db.Open();
        var result = await connection.QuerySingleAsync<int>("SELECT 1");
        return Results.Ok(new { Status = "Database connection successful", TestQuery = result });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database connection failed");
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
});

// Simple test endpoint without dependencies
app.MapGet("/api/debug/simple", () => new
{
    Message = "Simple endpoint works",
    Timestamp = DateTime.UtcNow,
    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
});

// Helper to get tenantId from JWT (authorizer already validated signature)
static Guid? GetTenantIdFromJwt(HttpContext ctx)
{
    try
    {
        var auth = ctx.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = auth.Substring("Bearer ".Length).Trim();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var t = jwt.Claims.FirstOrDefault(c => c.Type == "tenantId")?.Value;
        return string.IsNullOrWhiteSpace(t) ? (Guid?)null : Guid.Parse(t);
    }
    catch { return null; }
}

// New endpoint: Retrieve previously stored payment methods data by orderId
app.MapPost("/api/getpaymentMethods", async (Db db, HttpContext ctx, CancellationToken ct) =>
{
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            return Results.BadRequest(new { error = "Empty body" });
        }

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("orderId", out var orderIdEl) || orderIdEl.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return Results.BadRequest(new { error = "orderId is required" });
        }
        var orderId = orderIdEl.GetString();

        // Tenant guard: ensure order belongs to JWT tenant
        // var jwtTid = GetTenantIdFromJwt(ctx);
        // if (jwtTid is null) return Results.StatusCode(403);
        // using (var c = db.Open())
        // {
        //     var tid = await c.QuerySingleOrDefaultAsync<Guid?>("select tenantid from transactions where merchantreference=@ref limit 1", new { @ref = orderId });
        //     if (tid is null || tid.Value == Guid.Empty || tid.Value != jwtTid.Value)
        //     {
        //         return Results.StatusCode(403);
        //     }
        // }

        var data = await db.GetPaymentDataByOrderIdAsync(orderId!, ct);
        if (data is null)
        {
            return Results.NotFound(new { message = "No transaction found for the provided orderId" });
        }
        if (data.TransactionId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "TransactionId is required for cancellation" });
        }

        if (data.AmountValue is null)
        {
            return Results.BadRequest(new { message = "Could not find amount value for the provided orderId" });
        }

        if (string.IsNullOrEmpty(data.Currency))
        {
            return Results.BadRequest(new { message = "Could not find currency for the provided orderId" });
        }

        if (string.IsNullOrEmpty(data.CountryCode))
        {
            return Results.BadRequest(new { message = "Could not find countryCode for the provided orderId" });
        }

        if (data.PaymentMethods is null)
        {
            // If not in cache and not stored, return empty array to avoid blocking UX
            var emptyLineItems = (data.LineItems != null)
                ? data.LineItems.Select(li => (object)new { accountNumber = li.AccountNumber, billNumber = li.BillNumber, description = li.Description, amountValue = li.AmountValue }).ToArray()
                : Array.Empty<object>();
            var emptyResponse = new
            {
                transactionId = data.TransactionId,
                paymentMethods = new object[] { },
                sessionId = Guid.NewGuid().ToString(),
                reference = data.Reference,
                amount = new { value = data.AmountValue!.Value, currency = data.Currency! },
                countryCode = data.CountryCode!,
                lineItems = emptyLineItems,
                username = data.Username,
                email = data.Email,
                cardHolderName = data.CardHolderName,
                surcharge = new { amount = data.SurchargeAmount ?? 0, percent = (int?)null },
                legacyPostUrl = data.LegacyPostUrl ?? string.Empty
            };
            return Results.Ok(emptyResponse);
        }



        var responseLineItems2 = (data.LineItems != null)
            ? data.LineItems.Select(li => (object)new { accountNumber = li.AccountNumber, billNumber = li.BillNumber, description = li.Description, amountValue = li.AmountValue }).ToArray()
            : Array.Empty<object>();
        var response = new
        {
            transactionId = data.TransactionId,
            paymentMethods = data.PaymentMethods,
            sessionId = Guid.NewGuid().ToString(),
            reference = data.Reference,
            amount = new { value = data.AmountValue!.Value, currency = data.Currency! },
            countryCode = data.CountryCode!,
            lineItems = responseLineItems2,
            username = data.Username,
            email = data.Email,
            cardHolderName = data.CardHolderName,
            surcharge = new { amount = data.SurchargeAmount ?? 0, percent = (int?)null },
            legacyPostUrl = data.LegacyPostUrl ?? string.Empty
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// API Endpoints
app.MapPost("/api/paymentMethods", async (
    PaymentMethodsRequest req,
    IAdyenClient adyen,
    Db db,
    IIdempotencyService idempotency,
    ILogger<Program> logger,
    HttpContext ctx,
    ISecretsManager secrets,
    CancellationToken ct) =>
{
    var stopwatch = Stopwatch.StartNew();
    var correlationId = Guid.NewGuid().ToString();

    logger.LogInformation("Payment methods request started. CorrelationId: {CorrelationId}, Request: {@Request}", correlationId, req);

    try
    {
        // Validate request (amount validation moved after computing from lineItems)

        // Apply defaults if missing
        var currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency;
        if (currency!.Length != 3)
        {
            return Results.BadRequest(new { error = "Currency must be a valid 3-letter ISO code", field = "currency" });
        }

        var country = string.IsNullOrWhiteSpace(req.Country) ? "US" : req.Country;
        if (country!.Length != 2)
        {
            return Results.BadRequest(new { error = "Country must be a valid 2-letter ISO code", field = "country" });
        }

        if (string.IsNullOrWhiteSpace(req.OrderId) || req.OrderId.Length > 200)
        {
            return Results.BadRequest(new { error = "OrderId is required and must be less than 200 characters", field = "orderId" });
        }

        if (string.IsNullOrWhiteSpace(req.MerchantAccount))
        {
            return Results.BadRequest(new { error = "MerchantAccount is required", field = "merchantAccount" });
        }

        // Require legacyPostUrl for final handoff and surchargePercent to compute surcharge up front
        if (string.IsNullOrWhiteSpace(req.LegacyPostUrl))
        {
            return Results.BadRequest(new { error = "legacyPostUrl is required", field = "legacyPostUrl", code = "MissingRedirectUrl" });
        }
        if (!Uri.TryCreate(req.LegacyPostUrl, UriKind.Absolute, out var legacyUri) ||
            !(legacyUri.Scheme == Uri.UriSchemeHttps || legacyUri.Scheme == Uri.UriSchemeHttp))
        {
            return Results.BadRequest(new { error = "legacyPostUrl must be a valid absolute URL", field = "legacyPostUrl" });
        }
        if (req.SurchargePercent is null)
        {
            return Results.BadRequest(new { error = "surchargePercent is required", field = "surchargePercent" });
        }
        if (req.SurchargePercent < 0 || req.SurchargePercent > 100)
        {
            return Results.BadRequest(new { error = "surchargePercent must be between 0 and 100", field = "surchargePercent" });
        }

        logger.LogInformation("Step 1: Resolving tenant. CorrelationId: {CorrelationId}", correlationId);
        var tenantInfo = await db.GetTenantInfoByMerchantAccountAsync(req.MerchantAccount, ct);
        if (tenantInfo == null)
        {
            logger.LogWarning("Merchant account {MerchantAccount} not found. CorrelationId: {CorrelationId}", req.MerchantAccount, correlationId);
            return Results.BadRequest(new { error = "Merchant account not found", field = "merchantAccount" });
        }
        var tenantId = tenantInfo.Value.TenantId;
        if (tenantId == Guid.Empty)
        {
            logger.LogWarning("Merchant account {MerchantAccount} not found. CorrelationId: {CorrelationId}", req.MerchantAccount, correlationId);
            return Results.BadRequest(new { error = "Merchant account not found", field = "merchantAccount" });
        }

        // Load tenant secret for Adyen keys
        logger.LogInformation("Loading tenant secret for merchant {MerchantAccount}", req.MerchantAccount);
        var effectiveSecretName = string.IsNullOrWhiteSpace(tenantInfo.Value.SecretName)
            ? $"tenant-{req.MerchantAccount}-config"
            : tenantInfo.Value.SecretName;
        var secretJson = await secrets.GetSecretAsync(effectiveSecretName, ct);
        using var secretDoc = System.Text.Json.JsonDocument.Parse(secretJson);
        var secretRoot = secretDoc.RootElement;
        string? adyenApiKey = secretRoot.TryGetProperty("adyenAPIKey", out var apiKeyEl) ? apiKeyEl.GetString() : null;
        string? adyenClientKey = secretRoot.TryGetProperty("adyenClientKey", out var clientKeyEl) ? clientKeyEl.GetString() : null;
        logger.LogInformation("Tenant secret loaded: {SecretName}. AdyenApiKey set: {HasApiKey}, AdyenClientKey set: {HasClientKey}",
            effectiveSecretName,
            !string.IsNullOrWhiteSpace(adyenApiKey),
            !string.IsNullOrWhiteSpace(adyenClientKey));
        ctx.Items["AdyenApiKey"] = adyenApiKey ?? string.Empty;
        ctx.Items["MerchantAccount"] = req.MerchantAccount;

        logger.LogInformation("Step 2: Generating idempotency key. CorrelationId: {CorrelationId}", correlationId);
        // Check idempotency - use OrderId for unique key
        var idempotencyKey = await idempotency.GenerateKeyAsync(req.OrderId, ct);
        var cachedResponse = await idempotency.GetResponseAsync(idempotencyKey, ct);
        if (cachedResponse != null)
        {
            logger.LogInformation("Returning cached response for idempotency key {Key}", idempotencyKey);
            return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<object>(cachedResponse));
        }
        Guid TxId = Guid.NewGuid();
        logger.LogInformation("Step 3: Creating database record. CorrelationId: {CorrelationId}", correlationId);

        // Always compute total amount from lineItems
        if (req.LineItems is null || req.LineItems.Count == 0)
        {
            return Results.BadRequest(new { error = "lineItems are required to compute amount", field = "lineItems" });
        }
        var amountMinor = req.LineItems.Sum(li => li.AmountValue);
        if (amountMinor <= 0)
        {
            return Results.BadRequest(new { error = "Sum of lineItems must be greater than 0", field = "lineItems.amountValue" });
        }
        if (amountMinor > 999999999) // 9.99M in minor units
        {
            return Results.BadRequest(new { error = "Amount exceeds maximum allowed value", field = "amountMinor" });
        }

        await db.CreatePendingAsync(TxId, tenantId, req.OrderId, amountMinor, currency, idempotencyKey, req.Username, req.Email, ct);

        // Compute surcharge amount using provided percent and persist amount only
        var surchargeAmount = (long)Math.Round(amountMinor * (req.SurchargePercent!.Value / 100.0));
        if (surchargeAmount < 0) surchargeAmount = 0;
        await db.UpdateSurchargeAsync(TxId, surchargeAmount, ct);

        // Log transaction creation operation
        await db.CreateOperationAsync(
            Guid.NewGuid(),
            TxId,
            tenantId,
            null,
            "PAYMENT_METHODS_REQUESTED",
            "Success",
            amountMinor,
            currency,
            new { OrderId = req.OrderId, Amount = amountMinor, Currency = currency, Country = country, Username = req.Username, Email = req.Email },
            ct);

        // Username and email are stored at creation. CardholderName will be updated later during payment creation.

        // Store line items if provided
        if (req.LineItems is not null && req.LineItems.Count > 0)
        {
            var mapped = req.LineItems.Select(li => new Db.LineItemDto(
                Guid.Empty,
                li.AccountNumber,
                li.BillNumber,
                li.Description,
                li.AmountValue)).ToList();
            await db.ReplaceLineItemsAsync(TxId, mapped, ct);
        }

        logger.LogInformation("Step 3: Calling Adyen API. CorrelationId: {CorrelationId}", correlationId);
        // Build a normalized request for Adyen (with defaults applied)
        var normalizedReq = req with { AmountMinor = amountMinor, Currency = currency, Country = country };
        var adyenResponse = await adyen.GetPaymentMethodsAsync(normalizedReq, ct);

        // Persist returned payment methods JSON on the transaction for later retrieval
        await db.UpdatePaymentMethodsAsync(TxId, adyenResponse, ct);

        logger.LogInformation("Step 4: Generating auth code and payment URL. CorrelationId: {CorrelationId}", correlationId);
        // Create short-lived auth code (2 minutes) for frontend exchange
        await db.EnsureAuthCodesTableAsync(ct);
        var authCode = Guid.NewGuid().ToString("N");
        await db.CreateAuthCodeAsync(authCode, tenantId, req.MerchantAccount, effectiveSecretName, DateTimeOffset.UtcNow.AddMinutes(2), ct);

        // Generate payment URL with populated data and auth code
        var frontendBaseUrl = app.Configuration["Frontend:BaseUrl"];
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            return Results.BadRequest(new { error = "Frontend:BaseUrl not configured" });
        }
        var paymentUrl = $"{frontendBaseUrl}/payment?orderId={req.OrderId}&code={authCode}";

        var response = new
        {
            paymentUrl = paymentUrl,
            paymentMethods = adyenResponse,
            username = req.Username,
            email = req.Email,
            surcharge = new { amount = surchargeAmount, percent = req.SurchargePercent },
            legacyPostUrl = req.LegacyPostUrl,
            authCode,
            adyenClientKey = adyenClientKey ?? string.Empty
        };

        logger.LogInformation("Step 5: Caching response. CorrelationId: {CorrelationId}", correlationId);
        // Cache response
        await idempotency.StoreKeyAsync(idempotencyKey, System.Text.Json.JsonSerializer.Serialize(response), ct);

        stopwatch.Stop();
        logger.LogInformation("Payment methods request completed in {Duration}ms. Generated URL: {PaymentUrl}. CorrelationId: {CorrelationId}",
            stopwatch.ElapsedMilliseconds, paymentUrl, correlationId);

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        logger.LogError(ex, "Payment methods request failed after {Duration}ms. Exception Type: {ExceptionType}. Message: {Message}. CorrelationId: {CorrelationId}",
            stopwatch.ElapsedMilliseconds, ex.GetType().Name, ex.Message, correlationId);
        throw;
    }
})
.WithName("getpaymentMethods");

// Exchange auth code for JWT and client key
app.MapPost("/api/token/exchange", async (Db db, JwtTokenService jwt, ISecretsManager secrets, HttpContext ctx, CancellationToken ct) =>
{
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();
        var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest(new { error = "code is required" });
        }
        var consumed = await db.ConsumeAuthCodeAsync(code!, ct);
        if (consumed is null)
        {
            return Results.BadRequest(new { error = "invalid_or_expired_code" });
        }
        var (tenantId, merchantAccount, secretName) = consumed.Value;
        var effectiveSecretName = string.IsNullOrWhiteSpace(secretName)
            ? $"tenant-{merchantAccount}-config"
            : secretName;
        var token = await jwt.IssueAsync(tenantId, merchantAccount, TimeSpan.FromMinutes(10), ct);

        var secretJson = await secrets.GetSecretAsync(effectiveSecretName, ct);
        using var secretDoc = System.Text.Json.JsonDocument.Parse(secretJson);
        var secretRoot = secretDoc.RootElement;
        string? adyenClientKey = secretRoot.TryGetProperty("adyenClientKey", out var clientKeyEl) ? clientKeyEl.GetString() : null;

        return Results.Ok(new { token, adyenClientKey, expiresIn = 600 });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/payments", async (
    CreatePaymentRequest req,
    IAdyenClient adyen,
    Db db,
    IIdempotencyService idempotency,
    ILogger<Program> logger,
    HttpContext ctx,
    CancellationToken ct) =>
{
    var stopwatch = Stopwatch.StartNew();
    var correlationId = Guid.NewGuid().ToString();

    logger.LogInformation("Create payment request started for reference {Reference}, Country: {Country}. CorrelationId: {CorrelationId}",
        (string)req.Reference, req.Country ?? "NULL", correlationId);

    try
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(req.Reference) || req.Reference.Length > 200)
        {
            logger.LogWarning("Invalid reference: {Reference}. CorrelationId: {CorrelationId}", req.Reference ?? "NULL", correlationId);
            return Results.BadRequest(new { error = "Reference is required and must be less than 200 characters", field = "reference" });
        }

        if (req.AmountMinor <= 0 || req.AmountMinor > 999999999)
        {
            logger.LogWarning("Invalid amount: {Amount}. CorrelationId: {CorrelationId}", (long)req.AmountMinor, correlationId);
            return Results.BadRequest(new { error = "Amount must be between 1 and 999999999", field = "amountMinor" });
        }

        if (string.IsNullOrWhiteSpace(req.Currency) || req.Currency.Length != 3)
        {
            logger.LogWarning("Invalid currency: {Currency}. CorrelationId: {CorrelationId}", req.Currency ?? "NULL", correlationId);
            return Results.BadRequest(new { error = "Currency must be a valid 3-letter ISO code", field = "currency" });
        }

        if (string.IsNullOrWhiteSpace(req.ReturnUrl) || !Uri.TryCreate(req.ReturnUrl, UriKind.Absolute, out _))
        {
            logger.LogWarning("Invalid returnUrl: {ReturnUrl}. CorrelationId: {CorrelationId}", req.ReturnUrl ?? "NULL", correlationId);
            return Results.BadRequest(new { error = "ReturnUrl must be a valid absolute URL", field = "returnUrl" });
        }

        if (req.PaymentMethod == null)
        {
            logger.LogWarning("PaymentMethod is required. CorrelationId: {CorrelationId}", correlationId);
            return Results.BadRequest(new { error = "PaymentMethod is required", field = "paymentMethod" });
        }

        // Check idempotency
        var idempotencyKey = await idempotency.GenerateKeyAsync(System.Text.Json.JsonSerializer.Serialize(req), ct);
        var cachedResponse = await idempotency.GetResponseAsync(idempotencyKey, ct);
        if (cachedResponse != null)
        {
            logger.LogInformation("Returning cached response for idempotency key {Key}", idempotencyKey);
            return Results.Ok(System.Text.Json.JsonSerializer.Deserialize<object>(cachedResponse));
        }

        //var tenantId = Guid.Parse(app.Configuration["Tenant:Id"] ?? "00000000-0000-0000-0000-000000000001");
 
        // Find existing transaction by merchantReference (OrderId)
        using var connection = db.Open();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid TransactionId, Guid TenantId)?>(@"
    SELECT transactionid, tenantid
    FROM transactions
    WHERE merchantreference = @reference",
        new { reference = req.Reference });

        if (row is null)
        {
            logger.LogWarning("Transaction not found for reference {Reference}. CorrelationId: {CorrelationId}", (string)req.Reference, correlationId);
            return Results.BadRequest(new { error = "Transaction not found. Please call /paymentMethods first.", field = "reference" });
        }
        // Tenant guard
        var jwtTenantId = GetTenantIdFromJwt(ctx);
        if (jwtTenantId is null || jwtTenantId.Value != row.Value.TenantId)
        {
            return Results.StatusCode(403);
        }


        var txId = row.Value.TransactionId;
        var tenantId = row.Value.TenantId;
        logger.LogInformation($"Found existing transaction {txId} for reference {req.Reference}");

        // If a surcharge exists, ensure total sent to Adyen is base + surcharge
        long baseAmount;
        long surcharge;
        using (var c2 = db.Open())
        {
            var amtRow = await c2.QuerySingleOrDefaultAsync<(long AmountValue, long Surcharge)?>(
                "select coalesce(amountValue,0) as amountValue, coalesce(surcharge,0) as surcharge from transactions where transactionId=@id",
                new { id = txId });
            baseAmount = amtRow?.AmountValue ?? req.AmountMinor;
            surcharge = amtRow?.Surcharge ?? 0;
        }

        var adjustedReq = req with { AmountMinor = checked(baseAmount + surcharge) };

        var res = await adyen.CreatePaymentAsync(adjustedReq, idempotencyKey, ct);

        // Update cardholder name (if provided) at payment creation stage only
        if (!string.IsNullOrWhiteSpace(req.CardHolderName))
        {
            await db.UpdateCardHolderNameAsync(txId, req.CardHolderName, ct);
        }
        var finalStatus = res.ResultCode.Equals("Authorised", StringComparison.OrdinalIgnoreCase) ? "Authorised" : "Refused";

        // Update status and automatically log operation
        await db.UpdateStatusWithOperationAsync(
            txId,
            finalStatus,
            res.PspReference,
            res.ResultCode,
            null,
            tenantId,
            "PAYMENT_PROCESSED",
            adjustedReq.AmountMinor,
            req.Currency,
            res.Raw,
            ct);

        var rcLower = res.ResultCode?.ToLowerInvariant();
        var isFinal = res.Action is null && rcLower is not null && (rcLower == "authorised" || rcLower == "authorized" || rcLower == "refused" || rcLower == "cancelled" || rcLower == "canceled" || rcLower == "error");
        var provisional = !isFinal;
        var statusCheckUrl = $"/transactions/{txId}";

        object response = res.Action is not null
            ? new { txId, action = res.Action, statusCheckUrl }
            : new { txId, resultCode = res.ResultCode, pspReference = res.PspReference, provisional, statusCheckUrl };

        // Cache response
        await idempotency.StoreKeyAsync(idempotencyKey, System.Text.Json.JsonSerializer.Serialize(response), ct);

        stopwatch.Stop();
        logger.LogInformation("Create payment request completed in {Duration}ms. Result: {ResultCode}. CorrelationId: {CorrelationId}",
            stopwatch.ElapsedMilliseconds, res.ResultCode, correlationId);

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        logger.LogError(ex, "Create payment request failed after {Duration}ms for reference {Reference}. CorrelationId: {CorrelationId}",
            stopwatch.ElapsedMilliseconds, (string)req.Reference, correlationId);
        throw;
    }
})
.WithName("CreatePayment");

// Additional details (3DS) endpoint
app.MapPost("/api/payments/details", async (
    HttpContext ctx,
    IAdyenClient adyen,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        using var sr = new StreamReader(ctx.Request.Body);
        var raw = await sr.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Results.BadRequest(new { error = "Empty body" });
        }
        using var doc = System.Text.Json.JsonDocument.Parse(raw);
        var payload = doc.RootElement.Clone();
        var res = await adyen.SubmitPaymentDetailsAsync(payload, ct);
        return Results.Ok(res);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "payments/details failed");
        throw;
    }
});

app.MapPost("/api/payments/cost-estimate", async (
    CostEstimateRequest req,
    IAdyenClient adyen,
    Db db,
    ILogger<Program> logger,
    HttpContext ctx,
    CancellationToken ct) =>
{
    var correlationId = Guid.NewGuid().ToString();
    logger.LogInformation("Cost estimate request for tx {TxId}, ref {Ref}. CorrelationId: {CorrelationId}", req.TransactionId, req.Reference, correlationId);

    try
    {
        if (req.TransactionId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "transactionId is required" });
        }
        if (req.Amount is null || req.Amount.Value <= 0)
        {
            return Results.BadRequest(new { error = "amount.value must be > 0" });
        }
        var currency = string.IsNullOrWhiteSpace(req.Amount.Currency) ? "USD" : req.Amount.Currency;
        var country = string.IsNullOrWhiteSpace(req.Country) ? "US" : req.Country;

        // Verify transaction exists and load base amount and current surcharge
        using var c = db.Open();
        var tx = await c.QuerySingleOrDefaultAsync<(Guid TransactionId, long AmountValue, long Surcharge)?>(
            "select transactionId, coalesce(amountValue, 0) as amountValue, coalesce(surcharge, 0) as surcharge from transactions where transactionId=@id",
            new { id = req.TransactionId });
        if (tx is null)
        {
            return Results.BadRequest(new { error = "Transaction not found" });
        }
        // Tenant guard
        var txTenant = await c.QuerySingleOrDefaultAsync<Guid?>("select tenantid from transactions where transactionid=@id", new { id = req.TransactionId });
        var jwtTid = GetTenantIdFromJwt(ctx);
        if (jwtTid is null || txTenant is null || jwtTid.Value != txTenant.Value) { return Results.StatusCode(403); }

        // Call Adyen BinLookup getCostEstimate using encryptedCardNumber
        var adyenResp = await adyen.GetCostEstimateAsync(
            req.Reference,
            req.EncryptedCardNumber,
            tx.Value.AmountValue, // always estimate from base amount
            currency,
            country!,
            req.ShopperInteraction,
            req.Assumptions,
            ct);

        // Extract surcharge from Adyen response; fall back to 0
        long surcharge = 0;
        try
        {
            if (adyenResp is System.Text.Json.JsonElement el && el.TryGetProperty("costEstimateAmount", out var cea))
            {
                var val = cea.TryGetProperty("value", out var v) ? v.GetInt64() : 0;
                surcharge = val;
            }
        }
        catch { surcharge = 0; }

        // Update DB: only set surcharge; base amount remains immutable
        await db.UpdateSurchargeAsync(req.TransactionId, surcharge, ct);

        var totalWithSurcharge = checked(tx.Value.AmountValue + surcharge);

        var response = new CostEstimateResponse(
            SurchargeAmount: surcharge,
            TotalWithSurcharge: totalWithSurcharge,
            Raw: adyenResp);

        logger.LogInformation("Cost estimate computed. Surcharge {Surcharge}, Total {Total}. CorrelationId: {CorrelationId}", surcharge, totalWithSurcharge, correlationId);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Cost estimate failed. CorrelationId: {CorrelationId}", correlationId);
        throw;
    }
})
.WithName("GetCostEstimate");

app.MapGet("/api/transactions/{id:guid}", async (Guid id, Db db, ILogger<Program> logger, HttpContext ctx, CancellationToken ct) =>
{
    var correlationId = Guid.NewGuid().ToString();
    logger.LogInformation("Get transaction request for ID {TransactionId}. CorrelationId: {CorrelationId}", id, correlationId);

    try
    {
        using var c = db.Open();
        var row = await c.QuerySingleOrDefaultAsync<dynamic>("select transactionId, tenantId, merchantReference, amountValue, currencyCode, status::text as status, pspReference, resultCode, refusalReason, idempotencyKey, createdAt, updatedAt, username, email, cardholdername from transactions where transactionId=@id", new { id });

        if (row is null)
        {
            logger.LogWarning("Transaction {TransactionId} not found. CorrelationId: {CorrelationId}", id, correlationId);
            return Results.NotFound();
        }
        // Tenant guard (null-safe)
        // var jwtTid = GetTenantIdFromJwt(ctx);
        // Guid? rowTenantId = row.tenantId is Guid g
        //     ? g
        //     : (row.tenantId is string s && Guid.TryParse(s, out var g2) ? g2 : null);

        // if (rowTenantId is null)
        // {
        //     logger.LogWarning("Transaction {TransactionId} has null tenantId. CorrelationId: {CorrelationId}", id, correlationId);
        //     return Results.Problem("Transaction has no tenant assigned.", statusCode: 409);
        // }

        // if (jwtTid is null || jwtTid.Value != rowTenantId.Value) { return Results.StatusCode(403); }

        // Include line items in the response
        var items = await db.GetLineItemsAsync(id, ct);
        logger.LogInformation("Transaction {TransactionId} retrieved successfully. CorrelationId: {CorrelationId}", id, correlationId);
        return Results.Ok(new
        {
            row.transactionId,
            row.tenantId,
            row.merchantReference,
            row.amountValue,
            row.currencyCode,
            row.status,
            row.pspReference,
            row.resultCode,
            row.refusalReason,
            row.idempotencyKey,
            row.createdAt,
            row.updatedAt,
            row.username,
            row.email,
            row.cardholdername,
            lineItems = items.Select(li => new { accountNumber = li.AccountNumber, billNumber = li.BillNumber, description = li.Description, amountValue = li.AmountValue })
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve transaction {TransactionId}. CorrelationId: {CorrelationId}", id, correlationId);
        throw;
    }
})
.WithName("GetTransaction");

app.Run();

