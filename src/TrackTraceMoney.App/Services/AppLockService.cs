using Microsoft.Maui.Storage;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Security;

namespace TrackTraceMoney.App.Services;

/// <summary>
/// <see cref="IAppLockService"/> backed by <see cref="ISecureStorage"/> for the PIN hash/salt
/// (mirrors <c>CloudAuthService</c>'s own use of <c>ISecureStorage</c> for the one other
/// device-local secret this app stores, its cloud access token) — but the "is app lock enabled"
/// flag itself lives in <see cref="IPreferences"/> instead, deliberately NOT alongside the hash/salt.
/// <para>
/// This split exists because of a real deadlock found via live device testing: <c>App.xaml.cs</c>'s
/// <c>CreateWindow</c> override has no async version in MAUI, so it must call
/// <c>IsEnabledAsync().GetAwaiter().GetResult()</c> synchronously on the UI thread at cold start —
/// and Android's <c>SecureStorage</c> implementation genuinely awaits Keystore I/O that needs to
/// resume back on that same (blocked) UI thread, hanging forever. <c>Preferences</c>
/// (<c>Services.Profiles.PreferencesActiveProfileStore</c>'s exact precedent, confirmed by reading
/// it) has no such problem — its methods complete synchronously under an async-shaped signature, so
/// <c>.GetAwaiter().GetResult()</c> on it returns immediately, no deadlock risk. Whether a PIN is
/// set isn't itself sensitive (unlike the hash/salt), so <c>Preferences</c> is an appropriate,
/// safe home for just that one boolean.
/// </para>
/// Hash and salt are stored as Base64 strings (the only shape <see cref="ISecureStorage"/> accepts);
/// the raw PIN itself is never written anywhere, and never held longer than the single
/// <see cref="PinHasher"/> call each method needs it for.
/// </summary>
public sealed class AppLockService : IAppLockService
{
    private const string EnabledKey = "applock.enabled";
    private const string PinHashKey = "applock.pin_hash";
    private const string PinSaltKey = "applock.pin_salt";

    private readonly ISecureStorage _secureStorage;
    private readonly IPreferences _preferences;

    public AppLockService(ISecureStorage secureStorage, IPreferences preferences)
    {
        _secureStorage = secureStorage;
        _preferences = preferences;
    }

    public Task<bool> IsEnabledAsync(CancellationToken ct = default) =>
        Task.FromResult(_preferences.Get(EnabledKey, false));

    public async Task EnablePinAsync(string pin, CancellationToken ct = default)
    {
        var (hash, salt) = PinHasher.Hash(pin);
        // Order matters: write the hash/salt before flipping the enabled flag, so a failure partway
        // through can never leave "enabled" true with no (or a stale) hash behind it.
        await _secureStorage.SetAsync(PinHashKey, Convert.ToBase64String(hash));
        await _secureStorage.SetAsync(PinSaltKey, Convert.ToBase64String(salt));
        _preferences.Set(EnabledKey, true);
    }

    public Task DisableAsync(CancellationToken ct = default)
    {
        // Named-key removal only, mirroring CloudAuthService.LogoutAsync -- never
        // SecureStorage.RemoveAll(), which would also wipe any unrelated secure entries (e.g. the
        // cloud auth token) this app stores under other keys.
        _preferences.Remove(EnabledKey);
        _secureStorage.Remove(PinHashKey);
        _secureStorage.Remove(PinSaltKey);
        return Task.CompletedTask;
    }

    public async Task<bool> VerifyPinAsync(string pin, CancellationToken ct = default)
    {
        var hashBase64 = await _secureStorage.GetAsync(PinHashKey);
        var saltBase64 = await _secureStorage.GetAsync(PinSaltKey);

        if (string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(saltBase64))
            return false;

        return PinHasher.Verify(pin, Convert.FromBase64String(hashBase64), Convert.FromBase64String(saltBase64));
    }
}
