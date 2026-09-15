using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrackTraceMoney.Api.Auth;
using TrackTraceMoney.Api.Backups;
using TrackTraceMoney.Api.Persistence;
using TrackTraceMoney.Api.Users;

var builder = WebApplication.CreateBuilder(args);

// Phase 4 slice 3: email/password auth (register/login), no backup/sync endpoints yet. See
// CLAUDE.md's roadmap-phase-boundaries section before adding anything beyond auth here.
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

// Cloud-side PostgreSQL context, entirely separate from the on-device SQLite
// TrackTraceMoneyDbContext in TrackTraceMoney.Infrastructure. Connection string comes from
// configuration (appsettings.Development.json for local dev, or the ConnectionStrings__CloudDatabase
// environment variable in any other environment) — never hardcoded. See docker-compose.yml at the
// repo root for the local dev Postgres container these dev-only credentials point at.
builder.Services.AddDbContext<TrackTraceMoneyCloudDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CloudDatabase")));

// Standalone Microsoft.AspNetCore.Identity password hasher only (PBKDF2-HMAC-SHA256) — NOT full
// ASP.NET Core Identity (no IdentityDbContext/UserManager/SignInManager/roles/cookie auth). See
// this slice's spec before adding any of those.
builder.Services.AddSingleton<IPasswordHasher<CloudUser>, PasswordHasher<CloudUser>>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// JWT bearer auth: HS256, 30-day expiry, no refresh token this slice. Secret comes from
// configuration (Jwt:Key) — dev-only value in appsettings.Development.json, never hardcoded.
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, the handler remaps short JWT claim names (e.g. "sub") to their long
        // ClaimTypes.* URIs on the way in, which breaks any endpoint reading claims by their
        // original JwtRegisteredClaimNames constant (see BackupEndpoints.GetUserId).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapBackupEndpoints();

app.Run();

public partial class Program { }
