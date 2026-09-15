using Microsoft.AspNetCore.Identity;
using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Tests.Auth;

/// <summary>
/// Confirms the standalone <see cref="PasswordHasher{TUser}"/> (not full ASP.NET Core Identity)
/// round-trips correctly for <see cref="CloudUser"/> — this is the same hasher AuthEndpoints
/// uses for register/login.
/// </summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher<CloudUser> _hasher = new PasswordHasher<CloudUser>();

    [Fact]
    public void HashPassword_ThenVerify_WithCorrectPassword_Succeeds()
    {
        var user = new CloudUser("someone@example.com", passwordHash: string.Empty);
        var hash = _hasher.HashPassword(user, "correct-horse-battery");

        var result = _hasher.VerifyHashedPassword(user, hash, "correct-horse-battery");

        Assert.True(result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact]
    public void HashPassword_ThenVerify_WithWrongPassword_Fails()
    {
        var user = new CloudUser("someone@example.com", passwordHash: string.Empty);
        var hash = _hasher.HashPassword(user, "correct-horse-battery");

        var result = _hasher.VerifyHashedPassword(user, hash, "wrong-password");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }
}
