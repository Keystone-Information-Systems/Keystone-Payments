using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using ValPay.Domain;
using ValPay.Infrastructure.Exceptions;

namespace ValPay.Infrastructure;

public sealed class Db(string connStr)
{
    public IDbConnection Open() => new NpgsqlConnection(connStr);
    
    public sealed record PaymentData(
        Guid TransactionId,
        object? PaymentMethods,
        string? SessionId,
        string Reference,
        long? AmountValue,
        string? Currency,
        string? CountryCode,
        IReadOnlyList<LineItemDto>? LineItems,
        string? Username,
        string? Email,
        string? CardHolderName);

    public sealed record LineItemDto(
        Guid LineItemId,
        string AccountNumber,
        string? BillNumber,
        string? Description,
        long AmountValue);
    
    private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (NpgsqlException ex) when (PostgresExceptionHandler.IsRetryableError(ex) && attempt < maxRetries)
            {
                attempt++;
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100); // Exponential backoff
                await Task.Delay(delay);
                continue;
            }
        }
    }
    
    private static async Task ExecuteWithRetryAsync(Func<Task> operation, int maxRetries = 3)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                await operation();
                return;
            }
            catch (NpgsqlException ex) when (PostgresExceptionHandler.IsRetryableError(ex) && attempt < maxRetries)
            {
                attempt++;
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100); // Exponential backoff
                await Task.Delay(delay);
                continue;
            }
        }
    }


    
    public async Task<Guid?> GetTenantIdByMerchantAccountAsync(string merchantAccount, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            return await c.QuerySingleOrDefaultAsync<Guid?>(
                "SELECT COALESCE(tenants.tenantid, '00000000-0000-0000-0000-000000000000'::uuid) FROM tenants WHERE merchantAccount = @merchantAccount", 
                new { merchantAccount });
        });
    }

    public async Task CreatePendingAsync(Guid id, Guid tenantId, string reference, long amountMinor, string currency, string? idempotencyKey, string? username, string? email, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync("""
            insert into transactions(transactionId, tenantId, merchantReference, amountValue, currencyCode, status, idempotencyKey, username, email)
            values (@id, @tenantId, @ref, @amt, @cur, 'Pending'::payment_status, @idempotencyKey, @username, @email)
            on conflict (transactionId) do nothing;
            """, new { id, tenantId, @ref = reference, amt = amountMinor, cur = currency, idempotencyKey, username, email });
        });
    }

    public async Task UpdateCardHolderNameAsync(Guid id, string? cardHolderName, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync(
                """
                update transactions
                set cardholdername = coalesce(@cardHolderName, cardholdername),
                    updatedAt = now()
                where transactionId = @id;
                """,
                new { id, cardHolderName });
        });
    }

    public async Task ReplaceLineItemsAsync(Guid transactionId, IEnumerable<LineItemDto> items, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            if (c is NpgsqlConnection npg)
            {
                await npg.OpenAsync(ct);
            }
            else
            {
                c.Open();
            }
            using var tx = c.BeginTransaction();

            await c.ExecuteAsync("delete from transaction_lineitems where transactionid = @transactionId", new { transactionId }, tx);

            const string insertSql = """
            insert into transaction_lineitems(lineitemid, transactionid, accountnumber, billnumber, description, amountvalue)
            values (@LineItemId, @TransactionId, @AccountNumber, @BillNumber, @Description, @AmountValue)
            """;

            foreach (var item in items)
            {
                await c.ExecuteAsync(insertSql, new
                {
                    LineItemId = item.LineItemId == Guid.Empty ? Guid.NewGuid() : item.LineItemId,
                    TransactionId = transactionId,
                    item.AccountNumber,
                    item.BillNumber,
                    item.Description,
                    item.AmountValue
                }, tx);
            }

            tx.Commit();
        });
    }

    public async Task<IReadOnlyList<LineItemDto>> GetLineItemsAsync(Guid transactionId, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            var rows = await c.QueryAsync<LineItemDto>(
                """
                select lineitemid as LineItemId,
                       accountnumber as AccountNumber,
                       billnumber as BillNumber,
                       description as Description,
                       amountvalue as AmountValue
                from transaction_lineitems
                where transactionid = @transactionId
                order by lineitemid
                """,
                new { transactionId });
            return rows.AsList();
        });
    }

    public async Task CreateOperationAsync(Guid operationId, Guid transactionId, Guid tenantId, string? pspReference, string operationType, string status, long? amountValue, string? currencyCode, object? rawPayload, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync("""
            insert into operations(operationId, transactionId, tenantId, pspReference, operationType, status, amountValue, currencyCode, rawPayload)
            values (@operationId, @transactionId, @tenantId, @pspReference, @operationType, @status, @amountValue, @currencyCode, @rawPayload::jsonb)
            on conflict (pspReference) do nothing;
            """, new { operationId, transactionId, tenantId, pspReference, operationType, status, amountValue, currencyCode, rawPayload = rawPayload != null ? System.Text.Json.JsonSerializer.Serialize(rawPayload) : null });
        });
    }

    public async Task UpdateStatusAsync(Guid id, string status, string? pspReference, string? resultCode, string? refusalReason, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync("""
            update transactions
            set status=@status::payment_status, pspReference=coalesce(@pspReference, pspReference), resultCode=@resultCode, refusalReason=@refusalReason, updatedAt=now()
            where transactionId=@id;
            """, new { id, status, pspReference, resultCode, refusalReason });
        });
    }

    public async Task UpdateStatusWithOperationAsync(Guid id, string status, string? pspReference, string? resultCode, string? refusalReason, Guid tenantId, string operationType, long? amountValue, string? currencyCode, object? rawPayload, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync("""
            update transactions
            set status=@status::payment_status, pspReference=coalesce(@pspReference, pspReference), resultCode=@resultCode, refusalReason=@refusalReason, updatedAt=now()
            where transactionId=@id;
            """, new { id, status, pspReference, resultCode, refusalReason });
        });
        
        // Log the status update operation
        await CreateOperationAsync(
            Guid.NewGuid(), 
            id, 
            tenantId, 
            pspReference ?? null,
            operationType, 
            status, 
            amountValue, 
            currencyCode, 
            rawPayload, 
            ct);
    }

    public async Task UpdateSurchargeAsync(Guid transactionId, long surchargeAmount, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync(
                """
                update transactions
                set surcharge = @surchargeAmount,
                    updatedAt = now()
                where transactionId = @transactionId;
                """,
                new { transactionId, surchargeAmount });
        });
    }



    public async Task<PaymentData?> GetPaymentDataByOrderIdAsync(string orderId, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();

            // Find transaction by orderId (merchantReference)
            var txRow = await c.QuerySingleOrDefaultAsync<(Guid TransactionId, long? AmountValue, string? CurrencyCode, string? PaymentMethodsJson, string? Username, string? Email, string? CardHolderName)>(
                """
                select transactionId, amountValue, currencyCode, paymentmethods::text as paymentmethods, username, email, cardholdername
                from transactions
                where merchantReference = @orderId
                """,
                new { orderId });

            if (txRow.TransactionId == Guid.Empty)
            {
                return null;
            }

            // Compute idempotency key used when /paymentMethods was called (hash of orderId)
            using var sha256 = SHA256.Create();
            var key = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(orderId)));

            // Try to get cached response that contains paymentMethods and sessionId
            string? cachedResponse = await c.QuerySingleOrDefaultAsync<string>(
                """
                select response
                from idempotency_keys
                where key = @key and expires_at > now()
                """,
                new { key });

            object? paymentMethods = null;
            string? sessionId = null;
            if (!string.IsNullOrEmpty(cachedResponse))
            {
                using var doc = JsonDocument.Parse(cachedResponse);
                var root = doc.RootElement;
                if (root.TryGetProperty("paymentMethods", out var pm))
                {
                    paymentMethods = JsonSerializer.Deserialize<object>(pm.GetRawText());
                }
                if (root.TryGetProperty("sessionId", out var sid))
                {
                    sessionId = sid.GetString();
                }
            }

            // Fallback to the stored payment methods in the transaction if cache is missing
            if (paymentMethods is null && !string.IsNullOrWhiteSpace(txRow.PaymentMethodsJson))
            {
                paymentMethods = JsonSerializer.Deserialize<object>(txRow.PaymentMethodsJson);
            }

            // Try to infer countryCode from the logged operation rawPayload
            var countryCode = await c.QuerySingleOrDefaultAsync<string?>(
                """
                select coalesce(rawPayload->>'Country', rawPayload->>'country') as country
                from operations
                where transactionId = @txId and operationType = 'PAYMENT_METHODS_REQUESTED'
                limit 1
                """,
                new { txId = txRow.TransactionId });

            // Load line items
            var lineItems = await GetLineItemsAsync(txRow.TransactionId, ct);

            return new PaymentData(
                txRow.TransactionId,
                paymentMethods,
                sessionId,
                orderId,
                txRow.AmountValue,
                txRow.CurrencyCode,
                countryCode,
                lineItems,
                txRow.Username,
                txRow.Email,
                txRow.CardHolderName);
        });
    }

    public async Task UpdatePaymentMethodsAsync(Guid id, object paymentMethods, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync(
                """
                update transactions
                set paymentmethods = @paymentMethods::jsonb, updatedAt = now()
                where transactionId = @id;
                """,
                new { id, paymentMethods = System.Text.Json.JsonSerializer.Serialize(paymentMethods) });
        });
    }
}

