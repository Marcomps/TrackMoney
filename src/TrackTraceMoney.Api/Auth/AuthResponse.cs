namespace TrackTraceMoney.Api.Auth;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);
