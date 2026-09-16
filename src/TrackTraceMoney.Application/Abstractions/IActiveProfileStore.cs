namespace TrackTraceMoney.Application.Abstractions;

/// <summary>
/// Persists which <see cref="TrackTraceMoney.Domain.Profiles.LocalProfile"/> is currently active on
/// this device, independent of the profile catalog itself (<see cref="ILocalProfileRepository"/>) —
/// this is "which one is open right now", not "which ones exist".
///
/// Implemented in the App layer, not Infrastructure: the underlying platform storage (e.g. MAUI
/// <c>Preferences</c>) is not available to Infrastructure, which has no MAUI reference. Task-based even
/// though that underlying platform call is synchronous — matching every other Application abstraction
/// in this codebase (<see cref="IRepository{TEntity}"/>, <see cref="IUnitOfWork"/>,
/// <see cref="ILocalNotifier"/>) rather than introducing a sync-only outlier.
/// </summary>
public interface IActiveProfileStore
{
    Task<Guid?> GetActiveProfileIdAsync(CancellationToken ct = default);

    Task SetActiveProfileIdAsync(Guid profileId, CancellationToken ct = default);

    Task ClearActiveProfileIdAsync(CancellationToken ct = default);
}
