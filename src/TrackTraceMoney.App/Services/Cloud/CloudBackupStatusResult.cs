namespace TrackTraceMoney.App.Services.Cloud;

public sealed record CloudBackupStatusResult
{
    public bool Success { get; private init; }
    public CloudBackupResultError? Error { get; private init; }
    public bool Exists { get; private init; }
    public DateTimeOffset? LastBackupAtUtc { get; private init; }
    public long? SizeBytes { get; private init; }

    public static CloudBackupStatusResult Succeeded(bool exists, DateTimeOffset? lastBackupAtUtc, long? sizeBytes) =>
        new() { Success = true, Exists = exists, LastBackupAtUtc = lastBackupAtUtc, SizeBytes = sizeBytes };

    public static CloudBackupStatusResult Failed(CloudBackupResultError error) =>
        new() { Success = false, Error = error };
}
