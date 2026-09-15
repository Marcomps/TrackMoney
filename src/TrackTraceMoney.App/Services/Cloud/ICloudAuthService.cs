namespace TrackTraceMoney.App.Services.Cloud;

public interface ICloudAuthService
{
    Task<CloudAuthResult> RegisterAsync(string email, string password, CancellationToken ct = default);
    Task<CloudAuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<bool> IsAuthenticatedAsync(CancellationToken ct = default);
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}
