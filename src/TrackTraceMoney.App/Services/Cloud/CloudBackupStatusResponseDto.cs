namespace TrackTraceMoney.App.Services.Cloud;

/// <summary>Field-for-field mirror of TrackTraceMoney.Api's BackupStatusResponse — the App must
/// never reference TrackTraceMoney.Api directly (same convention as slices 4-5's auth DTOs).</summary>
public sealed record CloudBackupStatusResponseDto(bool Exists, DateTimeOffset? LastBackupAtUtc, long? SizeBytes);
