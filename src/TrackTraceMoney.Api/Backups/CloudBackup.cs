namespace TrackTraceMoney.Api.Backups;

/// <summary>
/// Opaque whole-file cloud backup blob for TrackTraceMoney.Api (Phase 4 slice 6). Stores the raw
/// on-device SQLite backup file bytes exactly as produced by
/// <c>TrackTraceMoney.Infrastructure</c>'s <c>LocalBackupService</c> — NOT structured/mirrored
/// domain data. One backup per user: upload always overwrites, matching the local backup
/// mechanism's own "no versioning" behavior. See this slice's spec before adding
/// history/versioning/multi-device conflict handling.
/// </summary>
public sealed class CloudBackup
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public byte[] Data { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private CloudBackup() { } // EF

    public CloudBackup(Guid userId, byte[] data)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Data = data;
        SizeBytes = data.LongLength;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void ReplaceData(byte[] data)
    {
        Data = data;
        SizeBytes = data.LongLength;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
