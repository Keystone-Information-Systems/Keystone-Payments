using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using KeyPay.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Xunit;
using Moq;

namespace KeyPay.Tests;

public class SecretsParsingTests
{
    private static JwtTokenService CreateServiceReturning(string secretStringPayload)
    {
        var sm = new Mock<IAmazonSecretsManager>(MockBehavior.Strict);
        sm.Setup(x => x.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new GetSecretValueResponse { SecretString = secretStringPayload });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:SecretName", "ignored-for-tests" }
            })
            .Build();

        return new JwtTokenService(sm.Object, config);
    }

    private static void ValidateWithSecret(string jwt, string secret)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(60)
        };
        handler.ValidateToken(jwt, parameters, out _);
    }

    [Fact]
    public async Task JwtSecret_JsonWithSecretProperty_IsUsedForSigning()
    {
        var svc = CreateServiceReturning("{\"secret\":\"SuperLong_Test_Secret_Value_With_AtLeast_32_Bytes__001\"}");
        var jwt = await svc.IssueAsync(Guid.NewGuid(), "MerchantA", TimeSpan.FromMinutes(5), CancellationToken.None);
        ValidateWithSecret(jwt, "SuperLong_Test_Secret_Value_With_AtLeast_32_Bytes__001");
    }

    [Fact]
    public async Task JwtSecret_RawString_IsUsedForSigning()
    {
        var svc = CreateServiceReturning("Raw_Secret_Test_Value_With_At_Least_32_Bytes_Length__XYZ");
        var jwt = await svc.IssueAsync(Guid.NewGuid(), "MerchantB", TimeSpan.FromMinutes(5), CancellationToken.None);
        ValidateWithSecret(jwt, "Raw_Secret_Test_Value_With_At_Least_32_Bytes_Length__XYZ");
    }

    [Fact]
    public async Task JwtSecret_JsonWithoutSecretProperty_FallsBackToRawJsonString()
    {
        const string rawJson = "{\"foo\":\"bar\",\"pad\":\"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX\"}";
        var svc = CreateServiceReturning(rawJson);
        var jwt = await svc.IssueAsync(Guid.NewGuid(), "MerchantC", TimeSpan.FromMinutes(5), CancellationToken.None);
        ValidateWithSecret(jwt, rawJson);
    }

    // No fake implementation needed; Moq is used.
}


