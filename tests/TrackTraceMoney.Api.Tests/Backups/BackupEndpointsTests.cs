using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TrackTraceMoney.Api.Auth;
using TrackTraceMoney.Api.Backups;
using TrackTraceMoney.Api.Tests.Infrastructure;

namespace TrackTraceMoney.Api.Tests.Backups;

/// <summary>
/// Integration tests against real endpoints + a real, ephemeral PostgreSQL container
/// (Testcontainers.PostgreSql), mirroring <c>Auth/AuthEndpointsTests.cs</c>'s exact conventions.
/// Requires Docker running locally — see <see cref="ApiWebApplicationFactory"/>'s remarks.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BackupEndpointsTests
{
    private readonly HttpClient _client;

    public BackupEndpointsTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = UniqueEmail();
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "correct-horse-battery"));
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.AccessToken;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, HttpContent? content = null) =>
        new(method, url)
        {
            Content = content,
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

    [Fact]
    public async Task Upload_WithoutToken_Returns401()
    {
        using var content = new ByteArrayContent([1, 2, 3]);

        var response = await _client.PostAsync("/api/backup", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithEmptyBody_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        using var content = new ByteArrayContent([]);
        using var request = AuthedRequest(HttpMethod.Post, "/api/backup", token, content);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ThenStatus_ReturnsExistsTrueWithCorrectSize()
    {
        var token = await RegisterAndGetTokenAsync();
        byte[] bytes = [1, 2, 3, 4, 5];

        using (var uploadContent = new ByteArrayContent(bytes))
        using (var uploadRequest = AuthedRequest(HttpMethod.Post, "/api/backup", token, uploadContent))
        {
            var uploadResponse = await _client.SendAsync(uploadRequest);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        }

        using var statusRequest = AuthedRequest(HttpMethod.Get, "/api/backup/status", token);
        var statusResponse = await _client.SendAsync(statusRequest);
        var status = await statusResponse.Content.ReadFromJsonAsync<BackupStatusResponse>();

        Assert.True(status!.Exists);
        Assert.Equal(bytes.Length, status.SizeBytes);
    }

    [Fact]
    public async Task Upload_ThenDownload_ReturnsByteIdenticalContent()
    {
        var token = await RegisterAndGetTokenAsync();
        byte[] bytes = [10, 20, 30, 40];

        using (var uploadContent = new ByteArrayContent(bytes))
        using (var uploadRequest = AuthedRequest(HttpMethod.Post, "/api/backup", token, uploadContent))
        {
            var uploadResponse = await _client.SendAsync(uploadRequest);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        }

        using var downloadRequest = AuthedRequest(HttpMethod.Get, "/api/backup", token);
        var downloadResponse = await _client.SendAsync(downloadRequest);

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloaded = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task ReUpload_SameUser_ThenDownload_ReturnsNewContentNotOld()
    {
        // Also exercises the upload-race fix (checkpoint finding #3): a sequential re-upload
        // for the same user must overwrite, not duplicate/conflict.
        var token = await RegisterAndGetTokenAsync();
        byte[] firstBytes = [1, 1, 1];
        byte[] secondBytes = [2, 2, 2, 2];

        using (var uploadContent = new ByteArrayContent(firstBytes))
        using (var uploadRequest = AuthedRequest(HttpMethod.Post, "/api/backup", token, uploadContent))
        {
            var response = await _client.SendAsync(uploadRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var uploadContent = new ByteArrayContent(secondBytes))
        using (var uploadRequest = AuthedRequest(HttpMethod.Post, "/api/backup", token, uploadContent))
        {
            var response = await _client.SendAsync(uploadRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var downloadRequest = AuthedRequest(HttpMethod.Get, "/api/backup", token);
        var downloadResponse = await _client.SendAsync(downloadRequest);
        var downloaded = await downloadResponse.Content.ReadAsByteArrayAsync();

        Assert.Equal(secondBytes, downloaded);
        Assert.NotEqual(firstBytes, downloaded);
    }

    [Fact]
    public async Task Download_WithNoPriorUpload_Returns404()
    {
        var token = await RegisterAndGetTokenAsync();
        using var request = AuthedRequest(HttpMethod.Get, "/api/backup", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Status_WithNoPriorUpload_ReturnsExistsFalse()
    {
        var token = await RegisterAndGetTokenAsync();
        using var request = AuthedRequest(HttpMethod.Get, "/api/backup/status", token);

        var response = await _client.SendAsync(request);
        var status = await response.Content.ReadFromJsonAsync<BackupStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(status!.Exists);
    }
}
