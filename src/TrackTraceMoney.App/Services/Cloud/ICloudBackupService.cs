namespace TrackTraceMoney.App.Services.Cloud;

public interface ICloudBackupService
{
    Task<CloudBackupUploadResult> UploadAsync(string localBackupFilePath, CancellationToken ct = default);
    Task<CloudBackupDownloadResult> DownloadAsync(CancellationToken ct = default);
    Task<CloudBackupStatusResult> GetStatusAsync(CancellationToken ct = default);
}
