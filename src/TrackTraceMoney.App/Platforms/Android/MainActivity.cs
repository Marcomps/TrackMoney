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
    // plaintext image even from outside the app itself. Deliberately unconditional (always applied,
    // not toggled based on whether an app-lock PIN is set) -- simpler than re-deriving app-lock state
    // inside this Android-specific Activity on every resume/toggle change, and a strictly safer
    // default either way (screenshot leakage is a real risk for this app's financial data regardless
    // of whether the user has opted into the PIN gate yet).
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Secure);
    }
}
