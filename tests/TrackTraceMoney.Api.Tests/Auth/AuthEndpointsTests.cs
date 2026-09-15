using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TrackTraceMoney.Api.Auth;
using TrackTraceMoney.Api.Tests.Infrastructure;

namespace TrackTraceMoney.Api.Tests.Auth;

/// <summary>
/// Integration tests against real endpoints + a real, ephemeral PostgreSQL container
/// (Testcontainers.PostgreSql). Requires Docker running locally — see
/// <see cref="ApiWebApplicationFactory"/>'s remarks. If Docker isn't available in the execution
/// environment, these tests will fail/hang at container startup rather than being silently
/// skipped; run them separately in an environment with Docker to verify.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AuthEndpointsTests
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_WithNewEmail_Returns201AndValidToken()
    {
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "correct-horse-battery"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal(email, body!.Email);
        Assert.NotEqual(Guid.Empty, body.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.ExpiresAtUtc > DateTimeOffset.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task Register_WithAlreadyRegisteredEmail_Returns409()
    {
        var email = UniqueEmail();
        var request = new RegisterRequest(email, "correct-horse-battery");

        var first = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Email already registered", problem?.Title);
    }

    [Theory]
    [InlineData("not-an-email", "correct-horse-battery")]
    [InlineData("valid@example.com", "short")]
    public async Task Register_WithInvalidInput_Returns400(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200AndToken()
    {
        var email = UniqueEmail();
        const string password = "correct-horse-battery";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal(email, body!.Email);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithGenericMessage()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "correct-horse-battery"));

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "totally-wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password", problem?.Title);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401WithSameMessageAsWrongPassword()
    {
        // Deliberately asserts the SAME status/message as the wrong-password case above —
        // prevents user-enumeration via error-message difference. Do not "fix" these into two
        // distinct messages without re-reading the slice's spec.
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(UniqueEmail(), "irrelevant-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password", problem?.Title);
    }
}
