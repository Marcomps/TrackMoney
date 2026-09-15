namespace TrackTraceMoney.App.Services.Cloud;

public enum CloudAuthResultError
{
    DuplicateEmail,      // 409 — register only
    InvalidCredentials,  // 401 — login only
    ValidationFailed,    // 400
    NetworkUnavailable,  // no connectivity / timeout / DNS / connection refused
    Unknown              // any other unexpected status or unparseable response
}

public sealed record CloudAuthResult
{
    public bool Success { get; private init; }
    public CloudAuthResultError? Error { get; private init; }
    public Guid? UserId { get; private init; }
    public string? Email { get; private init; }

    public static CloudAuthResult Succeeded(Guid userId, string email) =>
        new() { Success = true, UserId = userId, Email = email };

    public static CloudAuthResult Failed(CloudAuthResultError error) =>
        new() { Success = false, Error = error };
}
