namespace TrackTraceMoney.App.Services.Cloud;

/// <summary>
/// Single place to change the TrackTraceMoney.Api base URL — deliberately just a constant, not a
/// full settings system, since D4 (production hosting target) is still undecided. Promote this to
/// build-configuration-driven or a Settings-screen value once D4 lands.
///
/// Android-emulator gotcha: "localhost" inside the emulator refers to the emulator itself, not the
/// host machine running `dotnet run` for the Api. 10.0.2.2 is the emulator's documented alias for
/// the host loopback — use it against the Api's local dev profile. Port 5082 comes from the "http"
/// profile's applicationUrl in src/TrackTraceMoney.Api/Properties/launchSettings.json.
/// </summary>
public static class CloudApiConfig
{
    public const string BaseUrl = "http://10.0.2.2:5082";
}
