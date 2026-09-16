using TrackTraceMoney.Domain.Profiles;

namespace TrackTraceMoney.Application.Abstractions;

/// <summary>
/// Creates and deletes local, password-less profiles (each backed by its own on-device SQLite file)
/// and keeps the profile catalog (<see cref="ILocalProfileRepository"/>) and
/// <see cref="IActiveProfileStore"/> consistent as that happens. Unrelated to the cloud
/// "Cuenta en la nube" login/backup feature.
/// </summary>
public interface IProfileManagementService
{
    Task<LocalProfile> CreateProfileAsync(string profileName, CancellationToken ct = default);

    Task DeleteProfileAsync(Guid profileId, CancellationToken ct = default);
}
