namespace TrackTraceMoney.App.Services.Cloud;

public sealed record CloudBackupUploadResult
{
    public bool Success { get; private init; }
    public CloudBackupResultError? Error { get; private init; }
    public DateTimeOffset? LastBackupAtUtc { get; private init; }

    public static CloudBackupUploadResult Succeeded(DateTimeOffset lastBackupAtUtc) =>
        new() { Success = true, LastBackupAtUtc = lastBackupAtUtc };

    public static CloudBackupUploadResult Failed(CloudBackupResultError error) =>
        new() { Success = false, Error = error };
}
