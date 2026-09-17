using System.Runtime.CompilerServices;

namespace TrackTraceMoney.E2E.Tests.Infrastructure;

/// <summary>
/// Every path/URL this suite needs, each overridable via an environment variable so the suite still
/// runs on a machine set up differently than the one this project was authored on — the hardcoded
/// defaults match this session's documented one-time environment setup (see <see cref="E2ETestBase"/>'s
/// own doc comment for the full "how to run this locally" story).
/// </summary>
public static class TestConfig
{
    /// <summary>Full path to adb.exe on the user-writable, second Android SDK root used for E2E work.</summary>
    public static string AdbPath =>
        Environment.GetEnvironmentVariable("TTM_ADB_PATH")
        ?? @"C:\Users\PC\android-sdk-local\platform-tools\adb.exe";

    /// <summary>
    /// Appium 2+/3+ serves its REST interface at the root path by default (no "/wd/hub" segment, unlike
    /// Appium 1.x) — matches how this suite's documented setup starts the server (plain
    /// <c>appium --address 127.0.0.1 --port 4723</c>, no <c>--base-path</c>).
    /// </summary>
    public static Uri AppiumServerUri =>
        new(Environment.GetEnvironmentVariable("TTM_APPIUM_URL") ?? "http://127.0.0.1:4723");

    /// <summary>Matches <c>TrackTraceMoney.App.csproj</c>'s <c>&lt;ApplicationId&gt;</c>.</summary>
    public const string PackageName = "com.tracktracemoney.mobile";

    /// <summary>
    /// MAUI's Android packaging generates this activity name from a CRC64 hash of the assembly identity
    /// — it is stable across rebuilds of the same project (not regenerated per-build), but would change
    /// if the app's package/assembly identity ever changed.
    /// </summary>
    public const string MainActivity = "crc642c86fa8021e648a2.MainActivity";

    /// <summary>
    /// The standalone, installable debug APK — NOT the default `dotnet build` output, which uses Fast
    /// Deployment and isn't a complete installable package on its own. Produced by:
    /// <code>
    /// dotnet build src/TrackTraceMoney.App/TrackTraceMoney.App.csproj -f net10.0-android
    ///     -p:RuntimeIdentifier=android-x64 -p:AndroidPackageFormat=apk -p:EmbedAssembliesIntoApk=true
    /// </code>
    /// </summary>
    public static string ApkPath => Path.Combine(
        RepoRoot,
        "src", "TrackTraceMoney.App", "bin", "Debug", "net10.0-android", "android-x64",
        "com.tracktracemoney.mobile-Signed.apk");

    /// <summary>
    /// Screenshots are evidence artifacts checked in relative to THIS project's source folder (so
    /// <c>Evidence/&lt;ScenarioName&gt;/&lt;step&gt;.png</c> paths are stable and reviewable regardless of
    /// where `dotnet test` actually executes from), not under `bin/`.
    /// </summary>
    public static string EvidenceRoot => Path.Combine(ProjectRoot, "Evidence");

    private static string ProjectRoot => GetProjectRoot();

    private static string RepoRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "..", ".."));

    private static string GetProjectRoot([CallerFilePath] string sourceFile = "")
    {
        // sourceFile == .../tests/TrackTraceMoney.E2E.Tests/Infrastructure/TestConfig.cs
        var infrastructureDir = Path.GetDirectoryName(sourceFile)!;
        return Path.GetFullPath(Path.Combine(infrastructureDir, ".."));
    }
}
