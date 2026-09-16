using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Profiles;

namespace TrackTraceMoney.Infrastructure.Profiles;

/// <summary>
/// Creates and deletes local, password-less profiles (README-external feature; unrelated to the cloud
/// "Cuenta en la nube" login). Mirrors <see cref="Persistence.LocalBackupService"/>'s style: a public
/// Infrastructure service that receives platform-specific paths as opaque strings rather than calling
/// MAUI's <c>FileSystem.AppDataDirectory</c> directly, since this project has no MAUI reference.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="CreateProfileAsync"/>:</b> slice 1 of this feature only ever has one
/// <see cref="LocalProfileGroup"/> per device (named literally <c>"General"</c>, never shown in UI —
/// there is no screen to create a second group yet). Because there is no dedicated repository for the
/// group itself (it has no independent query surface any ViewModel needs), this get-or-creates it
/// directly against <see cref="ProfileCatalogDbContext"/>, guarded by <see cref="IProfileCatalogDbAccessGate"/>
/// for that operation's own span — not wrapped into one larger transaction together with the
/// subsequent <see cref="ILocalProfileRepository"/> call, since nothing here requires cross-step
/// atomicity (unlike e.g. <c>RecurringExpenseService.ConfirmOccurrenceAsync</c>'s genuine need for one).
/// </para>
/// <para>
/// <b><see cref="DeleteProfileAsync"/>:</b> removes the catalog row first, then deletes the profile's
/// on-device database file — that ordering is deliberate: if the process dies between the two steps,
/// the failure mode is an orphaned, unreferenced <c>.db3</c> file wasting disk space, never a catalog
/// entry pointing at a missing file. File deletion mirrors <see cref="Persistence.LocalBackupService.RestoreAsync"/>'s
/// existing precedent: <see cref="SqliteConnection.ClearAllPools"/> releases any idle pooled native
/// connection before touching the file on disk (necessary on Windows, where an open handle without
/// delete/write sharing could otherwise block deletion), and the <c>-journal</c>/<c>-wal</c>/<c>-shm</c>
/// sidecar files are cleaned up alongside the main file.
/// </para>
/// </remarks>
public sealed class ProfileManagementService : IProfileManagementService
{
    private const string DefaultGroupName = "General";

    private readonly ProfileCatalogDbContext _catalogDbContext;
    private readonly ILocalProfileRepository _profileRepository;
    private readonly IActiveProfileStore _activeProfileStore;
    private readonly IProfileCatalogDbAccessGate _gate;
    private readonly string _profileDataDirectory;

    public ProfileManagementService(
        ProfileCatalogDbContext catalogDbContext,
        ILocalProfileRepository profileRepository,
        IActiveProfileStore activeProfileStore,
        IProfileCatalogDbAccessGate gate,
        string profileDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDataDirectory);

        _catalogDbContext = catalogDbContext;
        _profileRepository = profileRepository;
        _activeProfileStore = activeProfileStore;
        _gate = gate;
        _profileDataDirectory = profileDataDirectory;
    }

    public async Task<LocalProfile> CreateProfileAsync(string profileName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var group = await GetOrCreateDefaultGroupAsync(ct).ConfigureAwait(false);

        var profile = new LocalProfile(group.Id, profileName);
        await _profileRepository.AddAsync(profile, ct).ConfigureAwait(false);
        await _profileRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        return profile;
    }

    public async Task DeleteProfileAsync(Guid profileId, CancellationToken ct = default)
    {
        var profile = await _profileRepository.GetByIdAsync(profileId, ct).ConfigureAwait(false);
        if (profile is null)
        {
            // Already gone -- deleting a non-existent profile is a no-op, not an error, so callers
            // (e.g. a retry after a partially-failed delete) don't need special-case handling.
            return;
        }

        _profileRepository.Remove(profile);
        await _profileRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        DeleteProfileDatabaseFiles(profileId);

        var activeProfileId = await _activeProfileStore.GetActiveProfileIdAsync(ct).ConfigureAwait(false);
        if (activeProfileId == profileId)
        {
            await _activeProfileStore.ClearActiveProfileIdAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task<LocalProfileGroup> GetOrCreateDefaultGroupAsync(CancellationToken ct)
    {
        using var gateScope = await _gate.AcquireAsync(ct).ConfigureAwait(false);

        var group = await _catalogDbContext.Groups.FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (group is not null)
            return group;

        group = new LocalProfileGroup(DefaultGroupName);
        await _catalogDbContext.Groups.AddAsync(group, ct).ConfigureAwait(false);
        await _catalogDbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return group;
    }

    private void DeleteProfileDatabaseFiles(Guid profileId)
    {
        var dbPath = Path.Combine(_profileDataDirectory, $"tracktracemoney_{profileId}.db3");

        // Release any idle pooled native connections before touching the file on disk -- see this
        // class's remarks and LocalBackupService.RestoreAsync for why.
        SqliteConnection.ClearAllPools();

        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
        {
            var sidecar = dbPath + suffix;
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }
    }
}
