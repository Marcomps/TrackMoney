using System.Diagnostics;

namespace TrackTraceMoney.E2E.Tests.Infrastructure;

/// <summary>
/// Thin wrapper around shelling out to adb.exe directly — used only for whole-device/app-data
/// operations Appium itself has no first-class capability for (clearing app data between tests,
/// granting a runtime permission proactively, checking a device is attached before wasting time
/// starting an Appium session against nothing). Everything that happens INSIDE the app once it's
/// running goes through Appium's own element-finding (<see cref="E2ETestBase"/>), never this class —
/// see this repo's E2E task notes for why raw `uiautomator dump` shelling was a manual-testing-only
/// workaround, not a pattern to carry into the automated suite.
/// </summary>
public static class AdbClient
{
    public static (int ExitCode, string StdOut, string StdErr) Run(string arguments, int timeoutMs = 30_000)
    {
        var startInfo = new ProcessStartInfo(TestConfig.AdbPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{TestConfig.AdbPath} {arguments}'.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'adb {arguments}' did not exit within {timeoutMs}ms.");
        }

        return (process.ExitCode, stdOutTask.GetAwaiter().GetResult(), stdErrTask.GetAwaiter().GetResult());
    }

    /// <summary>True when `adb devices` lists at least one entry in "device" (fully booted, authorized) state.</summary>
    public static bool HasReadyDevice()
    {
        var (_, stdOut, _) = Run("devices");
        return stdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1) // header line: "List of devices attached"
            .Any(line => line.Contains('\t') && line.TrimEnd().EndsWith("device", StringComparison.Ordinal));
    }

    /// <summary>
    /// Wipes the app's local SQLite/profile-catalog data, equivalent to a fresh install from the
    /// running device's point of view — this is how every E2E test guarantees it starts from the same
    /// "brand-new install" state as every other test, regardless of what earlier tests left behind.
    /// </summary>
    public static void ClearAppData() => Run($"shell pm clear {TestConfig.PackageName}");

    /// <summary>
    /// Proactively grants POST_NOTIFICATIONS so a runtime permission prompt can never unexpectedly
    /// block a foreground UI flow if some code path this suite exercises schedules a local
    /// notification — defensive; none of the current scenarios are expected to hit that path, but the
    /// failure mode if one did (an untappable system dialog silently stalling a `WebDriverWait`) is
    /// expensive enough to debug that granting it upfront is worth the one extra adb call per test.
    /// </summary>
    public static void GrantNotificationPermission() =>
        Run($"shell pm grant {TestConfig.PackageName} android.permission.POST_NOTIFICATIONS");

    public static bool IsPackageInstalled()
    {
        var (_, stdOut, _) = Run($"shell pm list packages {TestConfig.PackageName}");
        return stdOut.Contains(TestConfig.PackageName, StringComparison.Ordinal);
    }

    public static void Install(string apkPath) => Run($"install -r \"{apkPath}\"", timeoutMs: 120_000);
}
