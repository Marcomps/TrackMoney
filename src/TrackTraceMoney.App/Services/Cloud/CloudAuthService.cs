using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace TrackTraceMoney.App.Services.Cloud;

public sealed class CloudAuthService : ICloudAuthService
{
    private const string AccessTokenKey = "cloudauth.access_token";
    private const string ExpiresAtUtcKey = "cloudauth.expires_at_utc";
    private const string UserIdKey = "cloudauth.user_id";
    private const string EmailKey = "cloudauth.email";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ISecureStorage _secureStorage;

    public CloudAuthService(HttpClient httpClient, ISecureStorage secureStorage)
    {
        _httpClient = httpClient;
        _secureStorage = secureStorage;
    }

    public Task<CloudAuthResult> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new CloudRegisterRequestDto(email, password), JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return SendAndMapAsync("/api/auth/register", content, ct);
    }

    public Task<CloudAuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new CloudLoginRequestDto(email, password), JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return SendAndMapAsync("/api/auth/login", content, ct);
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        // Named-key removal only — never SecureStorage.RemoveAll(), which would also wipe any
        // unrelated secure entries a later slice adds.
        _secureStorage.Remove(AccessTokenKey);
        _secureStorage.Remove(ExpiresAtUtcKey);
        _secureStorage.Remove(UserIdKey);
        _secureStorage.Remove(EmailKey);
        return Task.CompletedTask;
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default) =>
        await GetAccessTokenAsync(ct) is not null;

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var token = await _secureStorage.GetAsync(AccessTokenKey);
        var expiresRaw = await _secureStorage.GetAsync(ExpiresAtUtcKey);

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expiresRaw))
            return null;

        if (!DateTimeOffset.TryParse(
                expiresRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc))
            return null;

        return expiresAtUtc > DateTimeOffset.UtcNow ? token : null;
    }

    public async Task<string?> GetCurrentEmailAsync(CancellationToken ct = default)
    {
        // Mirrors GetAccessTokenAsync's validity check — a missing/expired session has no
        // "current" email even if a stale value is still sitting in secure storage.
        if (await GetAccessTokenAsync(ct) is null)
            return null;

        return await _secureStorage.GetAsync(EmailKey);
    }

    private async Task<CloudAuthResult> SendAndMapAsync(string relativePath, HttpContent content, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(relativePath, content, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
        {
            // Covers unreachable host, DNS failure, connection refused, and HttpClient.Timeout.
            return CloudAuthResult.Failed(CloudAuthResultError.NetworkUnavailable);
        }

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var body = JsonSerializer.Deserialize<CloudAuthResponseDto>(responseJson, JsonOptions);
            if (body is null)
                return CloudAuthResult.Failed(CloudAuthResultError.Unknown);

            await StoreSessionAsync(body);
            return CloudAuthResult.Succeeded(body.UserId, body.Email);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => CloudAuthResult.Failed(CloudAuthResultError.DuplicateEmail),
            HttpStatusCode.Unauthorized => CloudAuthResult.Failed(CloudAuthResultError.InvalidCredentials),
            HttpStatusCode.BadRequest => CloudAuthResult.Failed(CloudAuthResultError.ValidationFailed),
            _ => CloudAuthResult.Failed(CloudAuthResultError.Unknown)
        };
    }

    private async Task StoreSessionAsync(CloudAuthResponseDto body)
    {
        await _secureStorage.SetAsync(AccessTokenKey, body.AccessToken);
        await _secureStorage.SetAsync(ExpiresAtUtcKey, body.ExpiresAtUtc.ToString("O"));
        await _secureStorage.SetAsync(UserIdKey, body.UserId.ToString());
        await _secureStorage.SetAsync(EmailKey, body.Email);
    }
}
