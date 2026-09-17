using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// A newly-created Person must immediately be selectable as both Payer and Beneficiary on a new
/// Expense — the gap closed by commit 498255b (Person/IPersonRepository were already fully modeled and
/// already consumed by these two pickers, but no screen could create a Person, making the pickers
/// unreachable in practice).
/// </summary>
public sealed class PersonPickerFlowTests : E2ETestBase
{
    private const string Scenario = "PersonPickerFlow";
    private const string PersonName = "E2E Alex";

    [Fact]
    public void NewPerson_AppearsInBothPayerAndBeneficiaryPickers()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E PersonProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            // --- Create the person ---
            TapTab("Settings");
            Tap("Settings_ManagePeopleButton");
            Tap("People_AddButton");
            EnterText("AddPerson_NameEntry", PersonName);
            Tap("AddPerson_SaveButton");

            WaitForId($"People_Item_{PersonName}");
            Screenshot(Scenario, "01-person-created");

            // --- Confirm it appears in a new Expense's Payer picker ---
            TapTab("Transactions");
            Tap("TransactionsList_AddButton"); // SelectedType defaults to Expense, so Payer/Beneficiary render immediately

            Tap("AddTransaction_PayerPicker");
            WaitForExactText(PersonName);
            Screenshot(Scenario, "02-person-in-payer-picker");
            TapByExactText(PersonName); // select it, also closes the native dialog

            // --- Confirm it appears in the same Expense's Beneficiary picker ---
            Tap("AddTransaction_BeneficiaryPicker");
            WaitForExactText(PersonName);
            Screenshot(Scenario, "03-person-in-beneficiary-picker");
            TapByExactText(PersonName);
        });
    }
}
