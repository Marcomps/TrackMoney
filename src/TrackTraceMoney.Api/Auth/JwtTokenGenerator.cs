using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Auth;

/// <summary>
/// Issues HS256-signed JWTs for <see cref="CloudUser"/>. 30-day expiry, no refresh-token flow
/// this slice (Phase 4 slice 3) — see CLAUDE.md's roadmap-phase-boundaries section before adding one.
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string AccessToken, DateTimeOffset ExpiresAtUtc) GenerateToken(CloudUser user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"]
            ?? throw new InvalidOperationException("Missing required configuration value 'Jwt:Key'.");
        var issuer = jwtSection["Issuer"]
            ?? throw new InvalidOperationException("Missing required configuration value 'Jwt:Issuer'.");
        var audience = jwtSection["Audience"]
            ?? throw new InvalidOperationException("Missing required configuration value 'Jwt:Audience'.");
        var expiryDays = jwtSection.GetValue<int?>("ExpiryDays") ?? 30;

        var expiresAtUtc = DateTimeOffset.UtcNow.AddDays(expiryDays);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return (accessToken, expiresAtUtc);
    }
}
