namespace TrackTraceMoney.Api.Backups;

public sealed record BackupStatusResponse(bool Exists, DateTimeOffset? LastBackupAtUtc, long? SizeBytes);
