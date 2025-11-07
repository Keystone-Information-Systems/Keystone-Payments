using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using KeyPay.Domain;
using KeyPay.Infrastructure.Exceptions;

namespace KeyPay.Infrastructure;

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
        string? CardHolderName,
        long? SurchargeAmount,
        string? LegacyPostUrl);

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

    public async Task<(Guid TenantId, string SecretName)?> GetTenantInfoByMerchantAccountAsync(string merchantAccount, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            var row = await c.QuerySingleOrDefaultAsync<(Guid TenantId, string SecretName)?>(
                "select tenantid as TenantId, secret_name as SecretName from tenants where merchantaccount = @merchantAccount",
                new { merchantAccount });
            return row;
        });
    }

    public async Task EnsureAuthCodesTableAsync(CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync(
                """
                create table if not exists auth_codes (
                    code text primary key,
                    tenantid uuid not null,
                    merchantaccount text not null,
                    secret_name text not null,
                    created_at timestamptz not null default now(),
                    expires_at timestamptz not null,
                    used boolean not null default false
                );
                """
            );
        });
    }

    public async Task CreateAuthCodeAsync(string code, Guid tenantId, string merchantAccount, string secretName, DateTimeOffset expiresAt, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync(
                """
                insert into auth_codes(code, tenantid, merchantaccount, secret_name, expires_at, used)
                values (@code, @tenantId, @merchantAccount, @secretName, @expiresAt, false)
                on conflict (code) do update set tenantid=excluded.tenantid, merchantaccount=excluded.merchantaccount, secret_name=excluded.secret_name, expires_at=excluded.expires_at, used=false;
                """,
                new { code, tenantId, merchantAccount, secretName, expiresAt = expiresAt.UtcDateTime });
        });
    }

    public async Task<(Guid TenantId, string MerchantAccount, string SecretName)?> ConsumeAuthCodeAsync(string code, CancellationToken ct)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            IDbTransaction? tx = null;
            try
            {
                if (c is NpgsqlConnection npg)
                {
                    await npg.OpenAsync(ct);
                    tx = await npg.BeginTransactionAsync(ct);
                }
                else
                {
                    c.Open();
                    tx = c.BeginTransaction();
                }

            var row = await c.QuerySingleOrDefaultAsync<(Guid TenantId, string MerchantAccount, string SecretName)?>(
                """
                select tenantid as TenantId, merchantaccount as MerchantAccount, secret_name as SecretName
                from auth_codes
                where code = @code and used = false and expires_at > now()
                for update
                """,
                new { code }, tx);

            if (row is null)
            {
                if (tx is NpgsqlTransaction ntx) await ntx.RollbackAsync(ct); else tx?.Rollback();
                return null;
            }

            await c.ExecuteAsync("update auth_codes set used = true where code = @code", new { code }, tx);

                if (tx is NpgsqlTransaction ntx2) await ntx2.CommitAsync(ct); else tx?.Commit();
            return row;
            }
            catch
            {
                try { if (tx is NpgsqlTransaction ntx3) await ntx3.RollbackAsync(ct); else tx?.Rollback(); } catch { }
                throw;
            }
            finally
            {
                if (tx is System.IAsyncDisposable ad) await ad.DisposeAsync(); else tx?.Dispose();
            }
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



    public async Task EnsureIndexesAsync(CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var c = Open();
            await c.ExecuteAsync(
                """
                create index if not exists idx_transactions_merchantreference on transactions(merchantreference);
                create index if not exists idx_transactions_tenantid on transactions(tenantid);
                create index if not exists idx_ops_txid on operations(transactionid);
                """
            );
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
            long? surchargeAmount = null;
            string? legacyPostUrl = null;
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
                if (root.TryGetProperty("surcharge", out var sc))
                {
                    try
                    {
                        if (sc.ValueKind == JsonValueKind.Object && sc.TryGetProperty("amount", out var amtEl))
                        {
                            surchargeAmount = amtEl.GetInt64();
                        }
                    }
                    catch { }
                }
                if (root.TryGetProperty("legacyPostUrl", out var lpu))
                {
                    try { legacyPostUrl = lpu.GetString(); } catch { }
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
                txRow.CardHolderName,
                surchargeAmount,
                legacyPostUrl);
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

