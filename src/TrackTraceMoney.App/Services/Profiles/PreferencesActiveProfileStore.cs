using Microsoft.Maui.Storage;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.Services.Profiles;

/// <summary>
/// <see cref="IActiveProfileStore"/> backed by MAUI's <see cref="IPreferences"/> (injected rather than
/// the static <see cref="Preferences"/> wrapper, so this class stays testable) — device-local key/value
/// storage, appropriate here since "which profile is active" is per-device state, not something that
/// belongs in either SQLite database this feature touches (the profile catalog lists which profiles
/// exist, not which one is currently open).
/// </summary>
public sealed class PreferencesActiveProfileStore : IActiveProfileStore
{
    private const string ActiveProfileIdKey = "Profiles.ActiveLocalProfileId";

    private readonly IPreferences _preferences;

    public PreferencesActiveProfileStore(IPreferences preferences)
    {
        _preferences = preferences;
    }

    public Task<Guid?> GetActiveProfileIdAsync(CancellationToken ct = default)
    {
        if (!_preferences.ContainsKey(ActiveProfileIdKey))
            return Task.FromResult<Guid?>(null);

        var stored = _preferences.Get(ActiveProfileIdKey, string.Empty);
        return Task.FromResult(Guid.TryParse(stored, out var id) ? id : (Guid?)null);
    }

    public Task SetActiveProfileIdAsync(Guid profileId, CancellationToken ct = default)
    {
        _preferences.Set(ActiveProfileIdKey, profileId.ToString());
        return Task.CompletedTask;
    }

    public Task ClearActiveProfileIdAsync(CancellationToken ct = default)
    {
        _preferences.Remove(ActiveProfileIdKey);
        return Task.CompletedTask;
    }
}
