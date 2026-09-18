using TrackTraceMoney.Application.Security;

namespace TrackTraceMoney.Application.Tests.Security;

/// <summary>
/// README §43 app-lock's PIN hashing (PBKDF2-HMACSHA256 via <c>Rfc2898DeriveBytes</c>, mirroring the
/// algorithm choice of the Api's own password hashing) — kept in the Application layer, dependency-free,
/// specifically so it's directly unit-testable without any MAUI/<c>ISecureStorage</c> involvement.
/// </summary>
public sealed class PinHasherTests
{
    [Fact]
    public void Hash_NeverReturnsThePlaintextPinAsTheHash()
    {
        var (hash, salt) = PinHasher.Hash("123456");

        Assert.NotEmpty(hash);
        Assert.NotEmpty(salt);
        // The hash is raw bytes, not the ASCII/UTF8 encoding of the PIN string.
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes("123456"), hash);
    }

    [Fact]
    public void Verify_WithTheCorrectPin_ReturnsTrue()
    {
        var (hash, salt) = PinHasher.Hash("123456");

        Assert.True(PinHasher.Verify("123456", hash, salt));
    }

    [Fact]
    public void Verify_WithAnIncorrectPin_ReturnsFalse()
    {
        var (hash, salt) = PinHasher.Hash("123456");

        Assert.False(PinHasher.Verify("654321", hash, salt));
    }

    [Fact]
    public void Verify_WithACloseButNotExactPin_ReturnsFalse()
    {
        // Proves this isn't doing anything looser than an exact match (e.g. a prefix comparison) --
        // a single differing digit must fail verification.
        var (hash, salt) = PinHasher.Hash("123456");

        Assert.False(PinHasher.Verify("123457", hash, salt));
    }

    [Fact]
    public void Hash_GeneratesADifferentRandomSaltEveryCall_EvenForTheSamePin()
    {
        var (hash1, salt1) = PinHasher.Hash("123456");
        var (hash2, salt2) = PinHasher.Hash("123456");

        Assert.NotEqual(salt1, salt2);
        // Different salts -> different hashes, even for an identical PIN (defense against a precomputed
        // rainbow-table-style attack across multiple stored PINs).
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_AgainstTheWrongSalt_ReturnsFalse_EvenWithTheCorrectPin()
    {
        var (hash, _) = PinHasher.Hash("123456");
        var (_, otherSalt) = PinHasher.Hash("999999");

        Assert.False(PinHasher.Verify("123456", hash, otherSalt));
    }
}
