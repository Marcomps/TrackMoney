using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// The foundation every other scenario builds on: launch the real installed app with no prior data and
/// confirm it reaches the onboarding screen without crashing. This alone catches nothing new by itself,
/// but every other test in this suite reuses <see cref="E2ETestBase.ResetApp"/>, so if this test is
/// red, every other test in the suite will be red for the same underlying reason.
/// </summary>
public sealed class SmokeTests : E2ETestBase
{
    [Fact]
    public void AppLaunchesToOnboarding_WithNoCrash()
    {
        const string scenario = "AppLaunch";

        RunScenario(scenario, () =>
        {
            ResetApp();
            Screenshot(scenario, "01-after-launch");

            // A brand-new install (no active profile yet) must land on CreateFirstProfilePage -- see
            // App.xaml.cs's CreateWindow. If the app crashed on launch, this wait times out and the
            // test fails loudly instead of silently passing on "didn't throw".
            var nameEntry = WaitForId("CreateFirstProfile_NameEntry");

            Assert.True(nameEntry.Displayed);
            Screenshot(scenario, "02-onboarding-confirmed");
        });
    }
}
