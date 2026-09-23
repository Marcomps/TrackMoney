using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// Switching tabs resets the tab being left back to its root page (AppShell.OnNavigated), so coming
/// back to Settings after drilling into Manage people shows Settings' main view, not the People list.
/// </summary>
public sealed class TabNavigationTests : E2ETestBase
{
    private const string Scenario = "TabNavigation";

    [Fact]
    public void ReturningToSettingsTab_AfterDrillingIn_ShowsSettingsRootPage()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E TabProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            TapTab("Settings");
            Tap("Settings_ManagePeopleButton");
            WaitForId("People_AddButton");
            Screenshot(Scenario, "01-people-list-open");

            TapTab("Dashboard");
            WaitForId("Dashboard_HealthLabel");

            TapTab("Settings");
            WaitForId("Settings_ManagePeopleButton");
            WaitForAbsenceOfId("People_AddButton");
            Screenshot(Scenario, "02-settings-root-after-tab-switch");
        });
    }
}
