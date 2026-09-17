using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace TrackTraceMoney.E2E.Tests.Infrastructure;

/// <summary>
/// Base class for every E2E scenario in this project.
///
/// <para>
/// <b>How a developer runs this suite locally</b> (this is the one place this is documented — every
/// test class inherits it, so no test file repeats it):
/// </para>
/// <list type="number">
/// <item>Start (or confirm already running) an Android emulator/device visible to `adb devices` as
/// state "device" (not "offline"/"unauthorized"). E.g.:
/// <code>C:\Users\PC\android-sdk-local\emulator\emulator.exe -avd TrackMoneyTest -no-boot-anim</code></item>
/// <item>Build and install the current debug build (a real installable APK — NOT plain `dotnet build`,
/// which uses Fast Deployment and produces no standalone package):
/// <code>
/// dotnet build src/TrackTraceMoney.App/TrackTraceMoney.App.csproj -f net10.0-android -p:RuntimeIdentifier=android-x64 -p:AndroidPackageFormat=apk -p:EmbedAssembliesIntoApk=true
/// "C:\Users\PC\android-sdk-local\platform-tools\adb.exe" install -r src\TrackTraceMoney.App\bin\Debug\net10.0-android\android-x64\com.tracktracemoney.mobile-Signed.apk
/// </code></item>
/// <item>Start an Appium server (Node/npm-based; installed once via `npm install -g appium` plus
/// `appium driver install uiautomator2`):
/// <code>appium --address 127.0.0.1 --port 4723</code></item>
/// <item><code>dotnet test tests/TrackTraceMoney.E2E.Tests</code></item>
/// </list>
/// <para>
/// Every <c>[Fact]</c> calls <see cref="ResetApp"/> as its first step, which clears the app's local
/// data via `adb shell pm clear` and relaunches it — so every test starts from the same guaranteed
/// brand-new-install state (onboarding/<c>CreateFirstProfilePage</c>) regardless of what any other
/// test in the suite left behind, and the suite is safe to run in any order, repeatedly, without
/// reinstalling the APK between runs. <see cref="AssemblyInfo"/> disables xUnit's cross-class test
/// parallelization for this assembly, since every test class drives the one shared emulator/device —
/// two Appium sessions fighting over the same device's UI at once would make both flaky.
/// </para>
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    protected AndroidDriver Driver { get; private set; } = null!;

    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(20);

    public virtual Task InitializeAsync()
    {
        if (!AdbClient.HasReadyDevice())
        {
            throw new InvalidOperationException(
                "No Android device/emulator is visible to adb in 'device' state. Start the " +
                "TrackMoneyTest AVD (see E2ETestBase's doc comment) before running this suite.");
        }

        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UiAutomator2",
        };
        options.AddAdditionalAppiumOption("appium:appPackage", TestConfig.PackageName);
        options.AddAdditionalAppiumOption("appium:appActivity", TestConfig.MainActivity);
        // noReset: this suite manages app data resets itself (see ResetApp), one `pm clear` per test
        // rather than a full uninstall/reinstall per test, which would also be far slower.
        options.AddAdditionalAppiumOption("appium:noReset", true);
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("appium:newCommandTimeout", 180);

        Driver = new AndroidDriver(TestConfig.AppiumServerUri, options, TimeSpan.FromMinutes(3));

        AdbClient.GrantNotificationPermission();

        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        try
        {
            Driver?.Quit();
        }
        catch
        {
            // Best-effort cleanup only — a session that's already dead (e.g. app crashed the
            // instrumentation process) must not fail test teardown.
        }

        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // App lifecycle
    // ---------------------------------------------------------------------

    /// <summary>
    /// Clears all local app data and relaunches — every scenario's first line, guaranteeing a fresh,
    /// onboarding-state start regardless of test execution order.
    /// </summary>
    protected void ResetApp()
    {
        try
        {
            Driver.TerminateApp(TestConfig.PackageName);
        }
        catch
        {
            // Not running yet is fine (e.g. very first test in the run).
        }

        AdbClient.ClearAppData();
        AdbClient.GrantNotificationPermission();
        Driver.ActivateApp(TestConfig.PackageName);
        DismissSystemUiAnrIfPresent();
    }

    /// <summary>
    /// Terminates and relaunches WITHOUT clearing data — used mid-scenario where the app's own
    /// documented behavior requires a cold start to observe (e.g. active-profile switch/deletion only
    /// takes effect for the finance database connection on next launch; see
    /// <c>ProfilesList_SwitchConfirmMessage</c>/<c>App.xaml.cs</c>'s root-page selection).
    /// </summary>
    protected void RelaunchApp()
    {
        try
        {
            Driver.TerminateApp(TestConfig.PackageName);
        }
        catch
        {
            // ignore
        }

        Driver.ActivateApp(TestConfig.PackageName);
        DismissSystemUiAnrIfPresent();
    }

    /// <summary>
    /// A loaded host (concurrent builds, a freshly-booted emulator still settling background services)
    /// can occasionally trip a "System UI isn't responding" ANR dialog right after `pm clear` + an
    /// immediate relaunch — a known, previously-observed environment flakiness source (see
    /// MEMORY.md/commit b58350d), not an app bug: the app's own Activity is still resumed underneath
    /// it. Tapping "Wait" lets SystemUI recover instead of failing the whole test on an unrelated OS
    /// hiccup. Best-effort and quick (short timeout, swallows a miss) — must never mask a REAL app
    /// crash, so it only ever taps "Wait", never "Close app".
    /// </summary>
    private void DismissSystemUiAnrIfPresent()
    {
        try
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(3));
            var waitButton = wait.Until(_ =>
                Driver.FindElements(By.XPath("//*[@text='Wait']")).FirstOrDefault(e => e.Displayed));
            waitButton.Click();
            Thread.Sleep(1000);
        }
        catch (WebDriverTimeoutException)
        {
            // No ANR dialog appeared -- the common case.
        }
    }

    protected void GoBack() => Driver.Navigate().Back();

    // ---------------------------------------------------------------------
    // App-XAML element helpers (AutomationId-based — see CLAUDE.md's add-domain-feature skill /
    // AGENTS' shared convention: every element these tests touch must carry an AutomationId, not a
    // text/XPath guess, because app-owned text is localized and changes across languages).
    //
    // IMPORTANT, discovered empirically (via `adb shell uiautomator dump` on a live CreateFirstProfilePage
    // — see this suite's own setup notes): .NET MAUI's Android renderer maps a control's AutomationId to
    // its native view's `resource-id` (e.g. "com.tracktracemoney.mobile:id/CreateFirstProfile_NameEntry"),
    // NOT `content-desc` — so Appium's "accessibility id" strategy (MobileBy.AccessibilityId, which
    // matches content-desc) never finds these elements on this app, even though it's the strategy most
    // Appium/MAUI documentation leads with.
    //
    // Plain Selenium `By.Id(...)` doesn't work either: Selenium's .NET client rewrites `By.Id` into a
    // CSS-selector locator client-side (there is no native "id" strategy in the base W3C WebDriver spec),
    // and UiAutomator2 doesn't resolve CSS selectors against a native Android app's accessibility tree —
    // it silently matches nothing instead of erroring, which looks identical to "element not present yet"
    // until you dump the UI tree and see the resource-id really is there. The reliable option this
    // Appium.WebDriver version actually exposes for a resource-id lookup is UiAutomator2's own "-android
    // uiautomator" strategy (MobileBy.AndroidUIAutomator + a UiSelector DSL string) — still Appium's own
    // element-finding (a first-class, documented UiAutomator2 locator strategy), not a shell-out to
    // `adb shell uiautomator dump`.
    // ---------------------------------------------------------------------

    private static By ResourceId(string automationId)
    {
        var fullyQualifiedId = $"{TestConfig.PackageName}:id/{automationId}";
        var escaped = fullyQualifiedId.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return MobileBy.AndroidUIAutomator($"new UiSelector().resourceId(\"{escaped}\")");
    }

    protected AppiumElement WaitForId(string automationId, TimeSpan? timeout = null)
    {
        try
        {
            return new WebDriverWait(Driver, timeout ?? DefaultWait)
                .Until(_ =>
                {
                    var element = Driver.FindElements(ResourceId(automationId)).FirstOrDefault();
                    if (element is { Displayed: true })
                        return element;

                    // Present in the tree but not on-screen (this app's longer Add* forms scroll), or
                    // not present yet -- ask UiAutomator2 to scroll it into view and retry once. A
                    // best-effort no-op on a page with no scrollable container (e.g. the Save/Cancel
                    // buttons pinned outside every Add* page's ScrollView are always on-screen already).
                    TryScrollIntoView(automationId);
                    return Driver.FindElements(ResourceId(automationId)).FirstOrDefault(e => e.Displayed);
                });
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException(
                $"Element with AutomationId '{automationId}' was not found/displayed within {(timeout ?? DefaultWait).TotalSeconds}s.",
                ex);
        }
    }

    private void TryScrollIntoView(string automationId)
    {
        try
        {
            var fullyQualifiedId = $"{TestConfig.PackageName}:id/{automationId}";
            var escaped = fullyQualifiedId.Replace("\\", "\\\\").Replace("\"", "\\\"");
            Driver.FindElement(MobileBy.AndroidUIAutomator(
                "new UiScrollable(new UiSelector().scrollable(true)).scrollIntoView(" +
                $"new UiSelector().resourceId(\"{escaped}\"))"));
        }
        catch
        {
            // No scrollable container on this page, or the element genuinely isn't there yet -- the
            // caller's own WebDriverWait polling loop handles the "not there yet" case; a page with no
            // ScrollView at all (so nothing needed scrolling in the first place) is not a failure here.
        }
    }

    /// <summary>Polls for the element to disappear (or never appear) — used for delete/empty-list assertions.</summary>
    protected void WaitForAbsenceOfId(string automationId, TimeSpan? timeout = null)
    {
        new WebDriverWait(Driver, timeout ?? DefaultWait)
            .Until(d => d.FindElements(ResourceId(automationId)).Count == 0);
    }

    protected bool ElementExists(string automationId) =>
        Driver.FindElements(ResourceId(automationId)).Count > 0;

    protected void Tap(string automationId, TimeSpan? timeout = null) => WaitForId(automationId, timeout).Click();

    protected void EnterText(string automationId, string text, TimeSpan? timeout = null)
    {
        var element = WaitForId(automationId, timeout);
        element.Clear();
        element.SendKeys(text);
        HideKeyboardSafe();
    }

    protected void HideKeyboardSafe()
    {
        try
        {
            Driver.HideKeyboard();
        }
        catch
        {
            // No keyboard was showing — not an error.
        }
    }

    /// <summary>
    /// Taps a Picker (identified by its own AutomationId) and selects the option with the given exact
    /// visible text from the native single-choice AlertDialog list it opens. The list items themselves
    /// are OS-rendered (Android's own AlertDialog/CheckedTextView), not this app's XAML, so — like
    /// <see cref="TapByExactText"/> below — text is the only selector available; this is Appium's own
    /// element-finding (XPath via the accessibility tree), not a shell-out to `uiautomator dump`.
    /// </summary>
    protected void SelectPickerOption(string pickerAutomationId, string optionText, TimeSpan? timeout = null)
    {
        Tap(pickerAutomationId);
        TapByExactText(optionText, timeout);
    }

    /// <summary>
    /// Same as <see cref="SelectPickerOption"/> but picks whichever option is listed first — for
    /// pickers where this suite doesn't care which value is chosen (e.g. "any category"), avoiding a
    /// dependency on seeded category names/localized text that isn't the thing under test.
    /// </summary>
    protected void SelectFirstPickerOption(string pickerAutomationId, TimeSpan? timeout = null)
    {
        Tap(pickerAutomationId);
        var option = new WebDriverWait(Driver, timeout ?? DefaultWait)
            .Until(d => d.FindElements(By.ClassName("android.widget.CheckedTextView")).FirstOrDefault(e => e.Displayed));
        option.Click();
    }

    // ---------------------------------------------------------------------
    // OS-rendered widget helpers (bottom tab bar, DisplayAlert dialogs, Picker dialogs) — none of these
    // are this app's own XAML, so exact visible text is the correct selector, not a workaround. Device
    // locale for every documented test run is en-US (see AVD setup), so text is asserted in English.
    // ---------------------------------------------------------------------

    protected void TapByExactText(string text, TimeSpan? timeout = null)
    {
        var element = new WebDriverWait(Driver, timeout ?? DefaultWait)
            .Until(d => d.FindElements(TextXPath(text)).FirstOrDefault(e => e.Displayed));
        element.Click();
    }

    protected void WaitForExactText(string text, TimeSpan? timeout = null)
    {
        new WebDriverWait(Driver, timeout ?? DefaultWait)
            .Until(d => d.FindElements(TextXPath(text)).Any(e => e.Displayed));
    }

    protected bool ExactTextExists(string text) =>
        Driver.FindElements(TextXPath(text)).Count > 0;

    protected static By TextXPath(string text) => By.XPath($"//*[@text={XPathLiteral(text)}]");

    /// <summary>
    /// Builds a valid XPath 1.0 string literal for arbitrary text — needed because this app's own
    /// English strings include an apostrophe (e.g. ProfilesList_Empty: "You haven't created any
    /// profiles yet."), and naively wrapping text in <c>'single quotes'</c> breaks the XPath expression
    /// the instant the text itself contains one (confirmed live: UiAutomator2's XPath engine rejected
    /// <c>//*[@text='You haven't ...']</c> with a parser error at the embedded apostrophe). Falls back
    /// to XPath's own <c>concat()</c> trick for the rare case where a string contains both quote types.
    /// </summary>
    private static string XPathLiteral(string value)
    {
        if (!value.Contains('\''))
            return $"'{value}'";
        if (!value.Contains('"'))
            return $"\"{value}\"";

        var parts = value.Split('\'');
        var pieces = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                pieces.Add("\"'\"");
            if (parts[i].Length > 0)
                pieces.Add($"'{parts[i]}'");
        }

        return $"concat({string.Join(", ", pieces)})";
    }

    /// <summary>Bottom TabBar navigation — MAUI Shell renders each Tab's Title as a BottomNavigationView item.</summary>
    protected void TapTab(string tabTitle) => TapByExactText(tabTitle);

    // ---------------------------------------------------------------------
    // Evidence
    // ---------------------------------------------------------------------

    /// <summary>
    /// Saves a screenshot under Evidence/&lt;scenarioName&gt;/&lt;stepName&gt;.png. Called at every
    /// meaningful step of a scenario (before/after an action) — see each Tests/*.cs file for where —
    /// plus automatically on any failure via <see cref="RunScenario"/>.
    /// </summary>
    protected void Screenshot(string scenarioName, string stepName)
    {
        var directory = Path.Combine(TestConfig.EvidenceRoot, scenarioName);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{stepName}.png");
        Driver.GetScreenshot().SaveAsFile(path);
    }

    /// <summary>
    /// Every <c>[Fact]</c> in this suite wraps its whole body in this, so a screenshot is captured of
    /// whatever was on-screen at the moment of failure — an assertion, a timed-out element wait, or any
    /// other exception — without every single test needing its own try/catch. Re-throws unchanged after
    /// capturing, so xUnit's own failure reporting/message is unaffected; the screenshot is purely
    /// additive evidence.
    /// </summary>
    protected void RunScenario(string scenarioName, Action steps)
    {
        try
        {
            steps();
        }
        catch (Exception)
        {
            try
            {
                Screenshot(scenarioName, "FAILURE");
            }
            catch
            {
                // The driver/session may itself be in a bad state after certain failures (e.g. the app
                // crashed outright) -- losing the failure screenshot must never mask the real exception.
            }

            throw;
        }
    }
}
