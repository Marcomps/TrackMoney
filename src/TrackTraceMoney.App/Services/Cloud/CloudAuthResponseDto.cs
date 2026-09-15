namespace TrackTraceMoney.App.Services.Cloud;

public sealed record CloudAuthResponseDto(Guid UserId, string Email, string AccessToken, DateTimeOffset ExpiresAtUtc);
