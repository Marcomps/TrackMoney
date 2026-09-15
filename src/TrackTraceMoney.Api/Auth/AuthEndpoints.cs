using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Api.Persistence;
using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Auth;

/// <summary>
/// Phase 4 slice 3: email/password registration and login only. No email verification
/// (registration logs the user straight in), no password reset, no refresh tokens — see the
/// slice's explicit non-scope list before extending this file.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
    }

    private static async Task<Results<Created<AuthResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request,
        TrackTraceMoneyCloudDbContext dbContext,
        IPasswordHasher<CloudUser> passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailAlreadyRegistered = await dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailAlreadyRegistered)
        {
            return TypedResults.Problem(
                title: "Email already registered",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Placeholder hash — PasswordHasher<TUser> only needs the instance's identity-agnostic
        // hashing algorithm, but the API requires a constructed TUser to call HashPassword. The
        // password hash itself doesn't depend on any other CloudUser field.
        var user = new CloudUser(normalizedEmail, passwordHash: string.Empty);
        var hash = passwordHasher.HashPassword(user, request.Password);
        user.UpdatePasswordHash(hash);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAtUtc) = tokenGenerator.GenerateToken(user);

        var response = new AuthResponse(user.Id, user.Email, accessToken, expiresAtUtc);
        return TypedResults.Created($"/api/auth/users/{user.Id}", response);
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        TrackTraceMoneyCloudDbContext dbContext,
        IPasswordHasher<CloudUser> passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = passwordHasher.HashPassword(user, request.Password);
            user.UpdatePasswordHash(newHash);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var (accessToken, expiresAtUtc) = tokenGenerator.GenerateToken(user);

        return TypedResults.Ok(new AuthResponse(user.Id, user.Email, accessToken, expiresAtUtc));
    }

    // Deliberately identical status/message for "unknown email" and "wrong password" — prevents
    // user-enumeration via error-message difference. Do not split these into different messages.
    private static ProblemHttpResult InvalidCredentials() =>
        TypedResults.Problem(
            title: "Invalid email or password",
            statusCode: StatusCodes.Status401Unauthorized);
}
