using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;

namespace TrackTraceMoney.App.Services.Cloud;

public sealed class CloudBackupService : ICloudBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ICloudAuthService _cloudAuthService;

    public CloudBackupService(HttpClient httpClient, ICloudAuthService cloudAuthService)
    {
        _httpClient = httpClient;
        _cloudAuthService = cloudAuthService;
    }

    public async Task<CloudBackupUploadResult> UploadAsync(string localBackupFilePath, CancellationToken ct = default)
    {
        var token = await _cloudAuthService.GetAccessTokenAsync(ct);
        if (token is null)
            return CloudBackupUploadResult.Failed(CloudBackupResultError.NotAuthenticated);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/backup");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await using var fileStream = File.OpenRead(localBackupFilePath);
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content = streamContent;

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
        {
            // Covers unreachable host, DNS failure, connection refused, and HttpClient.Timeout.
            return CloudBackupUploadResult.Failed(CloudBackupResultError.NetworkUnavailable);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<CloudBackupStatusResponseDto>(responseJson, JsonOptions);
            if (dto?.LastBackupAtUtc is null)
                return CloudBackupUploadResult.Failed(CloudBackupResultError.Unknown);

            return CloudBackupUploadResult.Succeeded(dto.LastBackupAtUtc.Value);
        }

        return CloudBackupUploadResult.Failed(MapErrorStatus(response.StatusCode));
    }

    public async Task<CloudBackupDownloadResult> DownloadAsync(CancellationToken ct = default)
    {
        var token = await _cloudAuthService.GetAccessTokenAsync(ct);
        if (token is null)
            return CloudBackupDownloadResult.Failed(CloudBackupResultError.NotAuthenticated);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/backup");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            // ResponseHeadersRead so the response body streams incrementally instead of being
            // fully buffered into memory before this method returns — important for backup files
            // that can be large relative to a lower-end Android device's available memory.
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
        {
            return CloudBackupDownloadResult.Failed(CloudBackupResultError.NetworkUnavailable);
        }

        if (response.IsSuccessStatusCode)
        {
            // Deliberately not disposing response/stream here — the caller owns the returned
            // stream (mirrors ILocalBackupService.RestoreAsync(Stream)'s existing contract).
            return CloudBackupDownloadResult.Succeeded(await response.Content.ReadAsStreamAsync(ct));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return CloudBackupDownloadResult.Failed(CloudBackupResultError.NoBackupFound);

        return CloudBackupDownloadResult.Failed(MapErrorStatus(response.StatusCode));
    }

    public async Task<CloudBackupStatusResult> GetStatusAsync(CancellationToken ct = default)
    {
        var token = await _cloudAuthService.GetAccessTokenAsync(ct);
        if (token is null)
            return CloudBackupStatusResult.Failed(CloudBackupResultError.NotAuthenticated);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/backup/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
        {
            return CloudBackupStatusResult.Failed(CloudBackupResultError.NetworkUnavailable);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<CloudBackupStatusResponseDto>(responseJson, JsonOptions);
            if (dto is null)
                return CloudBackupStatusResult.Failed(CloudBackupResultError.Unknown);

            return CloudBackupStatusResult.Succeeded(dto.Exists, dto.LastBackupAtUtc, dto.SizeBytes);
        }

        return CloudBackupStatusResult.Failed(MapErrorStatus(response.StatusCode));
    }

    private static CloudBackupResultError MapErrorStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => CloudBackupResultError.ValidationFailed,
        HttpStatusCode.Unauthorized => CloudBackupResultError.NotAuthenticated,
        // 413 — request body exceeded Kestrel's default size limit. No dedicated error case for
        // this (server-side request-size configuration is explicitly out of scope this round);
        // ValidationFailed keeps the message accurate ("something about this upload was
        // rejected") without needing a new enum case or resx key.
        HttpStatusCode.RequestEntityTooLarge => CloudBackupResultError.ValidationFailed,
        _ => CloudBackupResultError.Unknown
    };
}
