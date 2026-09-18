namespace TrackTraceMoney.Application.Abstractions;

/// <summary>
/// README §43's app-lock PIN — one device-wide lock gating the whole app (including the profile
/// list itself), not a per-<c>LocalProfile</c> setting (see the app-lock-slice-spec's Decision 2).
/// Implemented in the App layer, not Infrastructure: the underlying secure storage (MAUI's
/// <c>ISecureStorage</c>) is not available to Infrastructure, which has no MAUI reference — mirrors
/// <see cref="IActiveProfileStore"/>'s own App-implemented-interface pattern exactly, just backed by
/// <c>ISecureStorage</c> instead of <c>Preferences</c>, since a PIN hash is a secret (see
/// <c>CloudAuthService</c>'s own precedent for the one other device-local secret this app stores).
/// </summary>
public interface IAppLockService
{
    /// <summary>Whether a PIN is currently set — the single source of truth for whether the lock screen gates entry.</summary>
    Task<bool> IsEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Hashes and stores <paramref name="pin"/> (never the raw PIN itself), enabling app-lock.
    /// Overwrites any previously stored PIN.
    /// </summary>
    Task EnablePinAsync(string pin, CancellationToken ct = default);

    /// <summary>Clears the stored hash/salt, disabling app-lock. Caller is responsible for verifying the current PIN first (see Story 1).</summary>
    Task DisableAsync(CancellationToken ct = default);

    /// <summary>Verifies <paramref name="pin"/> against the stored hash. Returns <see langword="false"/> (never throws) if no PIN is set.</summary>
    Task<bool> VerifyPinAsync(string pin, CancellationToken ct = default);
}
