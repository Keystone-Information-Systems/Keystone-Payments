using System.Security.Cryptography;
using System.Text;

namespace ValPay.Webhook;

public static class AdyenHmacValidator
{
    public static bool Verify(NotificationRequestItem item, string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key) || item == null)
        {
            return false;
        }

        try
        {
            var key = Convert.FromBase64String(base64Key);
            var data = string.Join(":", new[]
            {
                item.PspReference ?? string.Empty,
                item.OriginalReference ?? string.Empty,
                item.MerchantAccountCode ?? string.Empty,
                item.MerchantReference ?? string.Empty,
                (item.Amount?.Value?.ToString() ?? string.Empty),
                item.Amount?.Currency ?? string.Empty,
                item.EventCode ?? string.Empty,
                item.Success ?? string.Empty
            });

            using var hmac = new HMACSHA256(key);
            var computedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
            var providedSignature = item.AdditionalData?.HmacSignature ?? string.Empty;

            // Fixed time comparison
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(providedSignature));
        }
        catch
        {
            return false;
        }
    }
}


