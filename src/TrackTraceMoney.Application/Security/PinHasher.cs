using System.Security.Cryptography;

namespace TrackTraceMoney.Application.Security;

/// <summary>
/// README §43's app-lock PIN, hashed — never stored or compared as plaintext, anywhere, ever (not
/// even transiently longer than a single verification call needs it in memory). Mirrors the
/// *algorithm choice* (not the code) of <c>TrackTraceMoney.Api</c>'s <c>AuthEndpoints</c>/<c>CloudUser</c>,
/// which hash cloud passwords via ASP.NET Core Identity's <c>PasswordHasher&lt;TUser&gt;</c> —
/// PBKDF2-HMACSHA256 with a random per-hash salt. The <c>App</c> project (MAUI/Android) has no reason
/// to pull in <c>Microsoft.AspNetCore.Identity</c> (an ASP.NET-hosting package), so this implements the
/// same algorithm directly against <see cref="Rfc2898DeriveBytes"/>, a plain BCL primitive — no new
/// NuGet dependency. A pure, stateless computation deliberately kept dependency-free (no MAUI/
/// <c>ISecureStorage</c> reference) so it's directly unit-testable; the App-layer
/// <c>IAppLockService</c> implementation owns reading/writing the resulting bytes to secure storage.
/// </summary>
public static class PinHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    /// <summary>
    /// Iteration count deliberately set higher than ASP.NET Core Identity's own
    /// <c>PasswordHasher&lt;TUser&gt;</c> default (100,000 as of the version this Api project uses) —
    /// a 6-digit numeric PIN is only 1,000,000 possible values, far lower entropy than a real
    /// password, so this compensates with a higher iteration count to keep offline brute-force of the
    /// full PIN space impractical (matches OWASP's current PBKDF2-HMACSHA256 guidance).
    /// </summary>
    private const int IterationCount = 210_000;

    /// <summary>Hashes a PIN with a freshly generated random salt. Returns both — the caller persists both.</summary>
    public static (byte[] Hash, byte[] Salt) Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, IterationCount, HashAlgorithmName.SHA256, HashSizeBytes);
        return (hash, salt);
    }

    /// <summary>
    /// Recomputes the hash for <paramref name="pin"/> against the stored <paramref name="salt"/> and
    /// compares it to <paramref name="expectedHash"/> in fixed time (<see cref="CryptographicOperations.FixedTimeEquals"/>)
    /// rather than a plain array/sequence comparison, so verification timing can't leak how many
    /// leading bytes matched.
    /// </summary>
    public static bool Verify(string pin, byte[] expectedHash, byte[] salt)
    {
        var computedHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, IterationCount, HashAlgorithmName.SHA256, HashSizeBytes);
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }
}
