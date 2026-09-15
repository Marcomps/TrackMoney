namespace TrackTraceMoney.App.Services.Cloud;

public enum CloudBackupResultError
{
    NotAuthenticated,    // missing/expired session
    NetworkUnavailable,  // no connectivity / timeout / DNS / connection refused
    NoBackupFound,       // 404 — download only
    ValidationFailed,    // 400
    Unknown              // any other unexpected status or unparseable response
}
