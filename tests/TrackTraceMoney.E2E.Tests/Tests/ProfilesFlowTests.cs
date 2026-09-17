using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// Local, password-less multi-profile support end to end: create the first profile from onboarding,
/// add a second, switch between them, delete a non-active profile, then delete the active/last profile
/// and confirm the app falls back to onboarding on next launch (README-external feature; see
/// MEMORY.md's trackmoney_roadmap_progress note and commit b58350d).
/// </summary>
public sealed class ProfilesFlowTests : E2ETestBase
{
    private const string Scenario = "ProfilesFlow";
    private const string ProfileOne = "E2E Profile One";
    private const string ProfileTwo = "E2E Profile Two";

    [Fact]
    public void CreateSwitchAndDeleteProfiles_EndsWithFallbackToOnboarding()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();
            Screenshot(Scenario, "01-onboarding");

            // --- Create the first profile from onboarding ---
            EnterText("CreateFirstProfile_NameEntry", ProfileOne);
            Tap("CreateFirstProfile_SaveButton");

            WaitForId("Dashboard_HealthLabel"); // live root-page swap into AppShell/Dashboard, no restart
            Screenshot(Scenario, "02-dashboard-after-first-profile");

            // --- Navigate to Settings > Manage profiles ---
            TapTab("Settings");
            Tap("Settings_ManageProfilesButton");

            WaitForId($"ProfilesList_Row_{ProfileOne}");
            Assert.True(RowHasActiveBadge(ProfileOne), $"'{ProfileOne}' should be the active profile right after creation.");
            Screenshot(Scenario, "03-profiles-list-one");

            // --- Add a second profile ---
            Tap("ProfilesList_AddButton");
            EnterText("AddProfile_NameEntry", ProfileTwo);
            Tap("AddProfile_SaveButton");

            WaitForId($"ProfilesList_Row_{ProfileTwo}");
            Assert.True(RowHasActiveBadge(ProfileOne), $"'{ProfileOne}' should still be active after only adding a second profile.");
            Assert.False(RowHasActiveBadge(ProfileTwo), $"'{ProfileTwo}' must not be active before it has ever been switched to.");
            Screenshot(Scenario, "04-profiles-list-two");

            // --- Switch to the second profile ---
            Tap($"ProfilesList_Row_{ProfileTwo}");
            WaitForExactText("Switch profile"); // DisplayAlert title
            TapByExactText("Switch"); // DisplayAlert accept button

            WaitForId($"ProfilesList_Row_{ProfileTwo}");
            Assert.True(RowHasActiveBadge(ProfileTwo), $"'{ProfileTwo}' should be active immediately after confirming the switch.");
            Assert.False(RowHasActiveBadge(ProfileOne), $"'{ProfileOne}' must no longer be active after switching away from it.");
            Screenshot(Scenario, "05-after-switch");

            // --- Delete the non-active profile (Profile One) ---
            Tap($"ProfilesList_Delete_{ProfileOne}");
            WaitForExactText("Delete profile");
            TapByExactText("Delete");

            WaitForAbsenceOfId($"ProfilesList_Row_{ProfileOne}");
            Assert.True(ElementExists($"ProfilesList_Row_{ProfileTwo}"), $"'{ProfileTwo}' must survive deleting the OTHER (non-active) profile.");
            Screenshot(Scenario, "06-after-delete-nonactive");

            // --- Delete the now-active, last remaining profile ---
            Tap($"ProfilesList_Delete_{ProfileTwo}");
            WaitForExactText("Delete profile");
            TapByExactText("Delete");

            WaitForExactText("You haven't created any profiles yet.");
            Screenshot(Scenario, "07-after-delete-active-last");

            // --- Confirm the fallback to onboarding on next cold start ---
            // Deleting the active profile only clears the active-profile pointer (see
            // ProfileManagementService.DeleteProfileAsync) -- App.xaml.cs only re-evaluates "is a
            // profile active?" at cold start (CreateWindow), so the fallback is only observable after a
            // relaunch.
            RelaunchApp();

            var nameEntry = WaitForId("CreateFirstProfile_NameEntry");
            Assert.True(nameEntry.Displayed, "Deleting the last/active profile must fall back to onboarding on next launch.");
            Screenshot(Scenario, "08-fallback-to-onboarding");
        });
    }

    private bool RowHasActiveBadge(string profileName)
    {
        var row = WaitForId($"ProfilesList_Row_{profileName}");
        return row.FindElements(OpenQA.Selenium.By.XPath(".//*[@text='Active']")).Count > 0;
    }
}
