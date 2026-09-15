using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Auth;

public interface IJwtTokenGenerator
{
    /// <summary>Issues a signed JWT for the given user, returning the token and its expiry.</summary>
    (string AccessToken, DateTimeOffset ExpiresAtUtc) GenerateToken(CloudUser user);
}
