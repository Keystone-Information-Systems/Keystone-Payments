using Amazon.Lambda.AspNetCoreServer.Hosting;
using Dapper;
using ValPay.Infrastructure;
using ValPay.Api.Middleware;
using ValPay.Infrastructure.Validation;
using ValPay.Infrastructure.Resilience;
using ValPay.Infrastructure.Idempotency;
using ValPay.Application;
using System.Diagnostics;
 
 

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

// Database
builder.Services.AddSingleton(new Db(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Username=postgres;Password=postgres;Database=valpay"));

// Idempotency
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();

// HTTP Client
builder.Services.AddHttpClient<IAdyenClient, AdyenClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Adyen:BaseUrl"] ?? "https://checkout-test.adyen.com");
    c.DefaultRequestHeaders.Add("X-API-Key", builder.Configuration["Adyen:ApiKey"] ?? "");
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",  // Vite default port
                "http://localhost:5174",  // Alternative Vite port
                "http://localhost:4173"   // Vite preview port
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// Run startup diagnostics
ValPay.Api.StartupDiagnostic.RunDiagnostics(app);


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

// Middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

// Test endpoint
app.MapGet("/", () => "ValPay API is running!");
app.MapGet("/health", () => "OK");

// Cancel payment endpoint
app.MapPost("/payments/{id:guid}/cancel", async (Guid id, Db db, ILogger<Program> logger, CancellationToken ct) =>
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

// Removed test /webhooks endpoint; real webhooks are handled by ValPay.Webhook Lambda

// Debug endpoints
app.MapGet("/debug/config", (IConfiguration config) => new
{
    AdyenBaseUrl = config["Adyen:BaseUrl"],
    AdyenApiKey = string.IsNullOrEmpty(config["Adyen:ApiKey"]) ? "NOT_SET" : "SET",
    AdyenMerchantAccount = string.IsNullOrEmpty(config["Adyen:MerchantAccount"]) ? "NOT_SET" : "SET",
    ConnectionString = string.IsNullOrEmpty(config.GetConnectionString("Postgres")) ? "NOT_SET" : "SET",
    TenantId = config["Tenant:Id"],
    FrontendBaseUrl = config["Frontend:BaseUrl"]
});

app.MapGet("/debug/db", async (Db db, ILogger<Program> logger) =>
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
app.MapGet("/debug/simple", () => new
{
    Message = "Simple endpoint works",
    Timestamp = DateTime.UtcNow,
    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
});

// New endpoint: Retrieve previously stored payment methods data by orderId
app.MapPost("/getpaymentMethods", async (Db db, HttpContext ctx, CancellationToken ct) =>
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
app.MapPost("/paymentMethods", async (
    PaymentMethodsRequest req,
    IAdyenClient adyen,
    Db db,
    IIdempotencyService idempotency,
    ILogger<Program> logger,
    HttpContext ctx,
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
        // Get tenant ID from tenants table
        var TId = await db.GetTenantIdByMerchantAccountAsync(req.MerchantAccount, ct);
        if (TId == null)
        {
            logger.LogWarning("Merchant account {MerchantAccount} not found. CorrelationId: {CorrelationId}", req.MerchantAccount, correlationId);
            return Results.BadRequest(new { error = "Merchant account not found", field = "merchantAccount" });
        }
        var tenantId = TId.Value;
        if (tenantId == Guid.Empty)
        {
            logger.LogWarning("Merchant account {MerchantAccount} not found. CorrelationId: {CorrelationId}", req.MerchantAccount, correlationId);
            return Results.BadRequest(new { error = "Merchant account not found", field = "merchantAccount" });
        }

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

        logger.LogInformation("Step 4: Generating payment URL. CorrelationId: {CorrelationId}", correlationId);
        // Generate payment URL with populated data
        var frontendBaseUrl = app.Configuration["Frontend:BaseUrl"];
        var paymentUrl = $"{frontendBaseUrl}/payment?orderId={req.OrderId}";

        var response = new
        {
            paymentUrl = paymentUrl,
            paymentMethods = adyenResponse,
            username = req.Username,
            email = req.Email,
            surcharge = new { amount = surchargeAmount, percent = req.SurchargePercent },
            legacyPostUrl = req.LegacyPostUrl
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

app.MapPost("/payments", async (
    CreatePaymentRequest req,
    IAdyenClient adyen,
    Db db,
    IIdempotencyService idempotency,
    ILogger<Program> logger,
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
        else
        {
            var (tranId, tid) = row.Value;
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

        var provisional = res.ResultCode.Equals("Authorised", StringComparison.OrdinalIgnoreCase);
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

app.MapPost("/payments/cost-estimate", async (
    CostEstimateRequest req,
    IAdyenClient adyen,
    Db db,
    ILogger<Program> logger,
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
        var tx = await c.QuerySingleOrDefaultAsync<(Guid TransactionId, long AmountValue, long Surcharge)?> (
            "select transactionId, coalesce(amountValue, 0) as amountValue, coalesce(surcharge, 0) as surcharge from transactions where transactionId=@id",
            new { id = req.TransactionId });
        if (tx is null)
        {
            return Results.BadRequest(new { error = "Transaction not found" });
        }

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

app.MapGet("/transactions/{id:guid}", async (Guid id, Db db, ILogger<Program> logger, CancellationToken ct) =>
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

