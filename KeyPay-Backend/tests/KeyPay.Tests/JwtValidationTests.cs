using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KeyPay.Tests;

public class JwtValidationTests
{
    private static string CreateJwt(string secret, TimeSpan lifetime, params Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            claims: claims.ToList(),
            notBefore: now.AddSeconds(-30),
            expires: now.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static ClaimsPrincipal InvokeAuthorizerValidate(string jwt, string secret)
    {
        var authorizerType = Type.GetType("KeyPay.Authorizer.Function, KeyPay.Authorizer", throwOnError: true)!;
        var method = authorizerType.GetMethod("ValidateJwt", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("ValidateJwt must exist and be private static in KeyPay.Authorizer.Function");

        var result = method!.Invoke(null, new object[] { jwt, secret });
        result.Should().NotBeNull();
        return (ClaimsPrincipal)result!;
    }

    [Fact]
    public void ValidateJwt_WithValidToken_ReturnsPrincipal()
    {
        const string secret = "UnitTestSecret_This_Is_A_32+_Byte_Key_For_HS256!!!";
        var jwt = CreateJwt(secret, TimeSpan.FromMinutes(5),
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenantId", Guid.NewGuid().ToString()),
            new Claim("merchantAccount", "TestMerchant"));

        var principal = InvokeAuthorizerValidate(jwt, secret);
        principal.Identity?.IsAuthenticated.Should().BeTrue();
        principal.Claims.FirstOrDefault(c => c.Type == "merchantAccount")!.Value.Should().Be("TestMerchant");
    }

    [Fact]
    public void ValidateJwt_WithExpiredToken_ThrowsSecurityTokenExpiredException()
    {
        const string secret = "UnitTestSecret_This_Is_A_32+_Byte_Key_For_HS256!!!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        // Ensure expires is far in past (>60s) but still after notBefore
        var token = new JwtSecurityToken(
            claims: new[] { new Claim("sub", Guid.NewGuid().ToString()) },
            notBefore: now.AddHours(-2),
            expires: now.AddMinutes(-61),
            signingCredentials: creds);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        Action act = () => InvokeAuthorizerValidate(jwt, secret);
        act.Should().Throw<TargetInvocationException>()
           .WithInnerException<SecurityTokenExpiredException>();
    }

    [Fact]
    public void ValidateJwt_TokenMissingMerchantAccount_StillValidatesButClaimAbsent()
    {
        const string secret = "UnitTestSecret_This_Is_A_32+_Byte_Key_For_HS256!!!";
        var jwt = CreateJwt(secret, TimeSpan.FromMinutes(5),
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenantId", Guid.NewGuid().ToString()));

        var principal = InvokeAuthorizerValidate(jwt, secret);
        principal.Identity?.IsAuthenticated.Should().BeTrue();
        principal.Claims.Any(c => c.Type == "merchantAccount").Should().BeFalse();
    }
}


