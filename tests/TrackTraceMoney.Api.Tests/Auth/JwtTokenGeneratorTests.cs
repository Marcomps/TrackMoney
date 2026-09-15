using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using TrackTraceMoney.Api.Auth;
using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Tests.Auth;

public class JwtTokenGeneratorTests
{
    private static JwtTokenGenerator CreateGenerator(int expiryDays = 30)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-only-signing-key-not-a-real-secret-0123456789",
                ["Jwt:Issuer"] = "TrackTraceMoney.Api",
                ["Jwt:Audience"] = "TrackTraceMoney.App",
                ["Jwt:ExpiryDays"] = expiryDays.ToString(),
            })
            .Build();

        return new JwtTokenGenerator(configuration);
    }

    [Fact]
    public void GenerateToken_EncodesSubAndEmailClaims()
    {
        var generator = CreateGenerator();
        var user = new CloudUser("someone@example.com", passwordHash: "irrelevant-for-this-test");

        var (accessToken, _) = generator.GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("TrackTraceMoney.Api", jwt.Issuer);
        Assert.Contains("TrackTraceMoney.App", jwt.Audiences);
    }

    [Fact]
    public void GenerateToken_ExpiresApproximatelyThirtyDaysFromNow()
    {
        var generator = CreateGenerator(expiryDays: 30);
        var user = new CloudUser("someone@example.com", passwordHash: "irrelevant-for-this-test");

        var beforeCall = DateTimeOffset.UtcNow;
        var (_, expiresAtUtc) = generator.GenerateToken(user);
        var afterCall = DateTimeOffset.UtcNow;

        Assert.InRange(expiresAtUtc, beforeCall.AddDays(30).AddMinutes(-1), afterCall.AddDays(30).AddMinutes(1));
    }
}
