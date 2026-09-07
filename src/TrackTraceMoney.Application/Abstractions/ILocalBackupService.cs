namespace TrackTraceMoney.Application.Abstractions;

/// <summary>
/// Creates and restores local backups of the on-device SQLite database (README §42).
/// Implementations must remain fully offline — no network calls.
/// </summary>
public interface ILocalBackupService
{
    /// <summary>
    /// Produces a copy of the current local database as a readable stream, positioned at 0.
    /// The caller owns the returned stream and is responsible for disposing it.
    /// </summary>
    Task<Stream> ExportAsync(CancellationToken ct = default);

    /// <summary>
    /// Replaces the local database's contents with the given backup data.
    /// The caller owns <paramref name="backupData"/> and is responsible for disposing it.
    /// </summary>
    /// <remarks>
    /// Restoring overwrites all locally persisted data. Depending on the implementation, changes
    /// may not be visible to the rest of the running app session — see the implementation's
    /// remarks for whether an app restart is required.
    /// </remarks>
    Task RestoreAsync(Stream backupData, CancellationToken ct = default);
}
