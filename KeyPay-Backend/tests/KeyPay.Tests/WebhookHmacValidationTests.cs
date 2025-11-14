using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Adyen.Util;
using FluentAssertions;
using Xunit;

namespace KeyPay.Tests;

public class WebhookHmacValidationTests
{
    private static string Escape(string? v)
    {
        if (string.IsNullOrEmpty(v)) return string.Empty;
        return v.Replace(@"\", @"\\").Replace(":", @"\:");
    }

    private static string BuildMacString(
        string? pspReference,
        string? originalReference,
        string? merchantAccount,
        string? merchantReference,
        string? amountValue,
        string? amountCurrency,
        string? eventCode,
        string? success)
    {
        var successStr = string.Equals(success, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
        var parts = new[]
        {
            Escape(pspReference),
            Escape(originalReference),
            Escape(merchantAccount),
            Escape(merchantReference),
            Escape(amountValue),
            Escape(amountCurrency),
            Escape(eventCode),
            Escape(successStr)
        };
        return string.Join(":", parts);
    }

    private static string ManualHmacBase64FromHexKey(string hexKey, string data)
    {
        byte[] keyBytes = Enumerable.Range(0, hexKey.Length / 2)
            .Select(i => Convert.ToByte(hexKey.Substring(i * 2, 2), 16))
            .ToArray();

        using var hmac = new HMACSHA256(keyBytes);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public void ValidHmac_Computation_MatchesAdyenValidator()
    {
        const string hexKey = "00112233445566778899AABBCCDDEEFF";
        var macString = BuildMacString(
            pspReference: @"psp:ref\1",
            originalReference: @"orig:ref\2",
            merchantAccount: "TestMerchant",
            merchantReference: "Order-123",
            amountValue: "1000",
            amountCurrency: "USD",
            eventCode: "AUTHORISATION",
            success: "true");

        var expected = new HmacValidator().CalculateHmac(macString, hexKey);
        var manual = ManualHmacBase64FromHexKey(hexKey, macString);

        manual.Should().Be(expected);
    }

    [Fact]
    public void InvalidHmac_DoesNotMatch()
    {
        const string hexKey = "00112233445566778899AABBCCDDEEFF";
        var macString = BuildMacString("psp", null, "M", "Ref", "1", "USD", "REFUND", "false");

        var expected = new HmacValidator().CalculateHmac(macString, hexKey);
        var wrong = new HmacValidator().CalculateHmac(macString + ":tampered", hexKey);

        expected.Should().NotBe(wrong);
    }
}


