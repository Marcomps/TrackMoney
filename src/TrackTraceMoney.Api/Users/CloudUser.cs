namespace TrackTraceMoney.Api.Users;

/// <summary>
/// Cloud-authentication credential entity for TrackTraceMoney.Api. Deliberately lives here,
/// not in TrackTraceMoney.Domain — a cloud login credential is Api-local infrastructure, not a
/// finance-domain concept, and pulling Microsoft.AspNetCore.Identity into Domain's dependency
/// graph would violate Domain's zero-package-dependency rule (see CLAUDE.md).
///
/// Deliberately excluded this slice (Phase 4 slice 3): EmailConfirmed, LastLoginAtUtc,
/// IsLocked/lockout counters, Roles. Do not add them without a scoped follow-up slice.
/// </summary>
public sealed class CloudUser
{
    public Guid Id { get; private set; }

    /// <summary>Stored lowercase-normalized.</summary>
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private CloudUser() { } // EF

    public CloudUser(string email, string passwordHash)
    {
        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Used for the rehash-on-login case (PasswordVerificationResult.SuccessRehashNeeded).</summary>
    public void UpdatePasswordHash(string newHash) => PasswordHash = newHash;
}
