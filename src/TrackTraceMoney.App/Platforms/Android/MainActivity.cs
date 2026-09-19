using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace TrackTraceMoney.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // README §43 app-lock Story 4 (product-owner's own recommended-but-not-mandatory inclusion, see
    // the app-lock-slice-spec's Decision 6): blanks this app's thumbnail in Android's recent-apps
    // switcher and blocks screenshots/screen recording, so a dashboard balance can't leak as a
    // plaintext image even from outside the app itself. Deliberately unconditional within a build
    // configuration (always applied in Release, not toggled based on whether an app-lock PIN is set)
    // -- simpler than re-deriving app-lock state inside this Android-specific Activity on every
    // resume/toggle change, and a strictly safer default either way (screenshot leakage is a real
    // risk for this app's financial data regardless of whether the user has opted into the PIN gate
    // yet). Skipped in DEBUG builds only (found via checkpoint code review): FLAG_SECURE also blocks
    // Android's standard screen-capture path, which silently blanked every future
    // tests/TrackTraceMoney.E2E.Tests screenshot (E2ETestBase.Screenshot -- Appium/UiAutomator2's
    // Driver.GetScreenshot()) for every scenario, not just app-lock ones -- the E2E suite always builds
    // Debug (see this repo's CLAUDE.md Commands section), so gating on DEBUG preserves the real
    // security property in the Release builds users actually run without breaking that tooling.
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
#if !DEBUG
        Window?.AddFlags(WindowManagerFlags.Secure);
#endif
    }
}
