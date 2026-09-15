namespace TrackTraceMoney.App.Services.Cloud;

public sealed record CloudBackupDownloadResult
{
    public bool Success { get; private init; }
    public CloudBackupResultError? Error { get; private init; }

    /// <summary>Caller owns disposal — mirrors ILocalBackupService.RestoreAsync(Stream)'s existing contract.</summary>
    public Stream? Data { get; private init; }

    public static CloudBackupDownloadResult Succeeded(Stream data) =>
        new() { Success = true, Data = data };

    public static CloudBackupDownloadResult Failed(CloudBackupResultError error) =>
        new() { Success = false, Error = error };
}
