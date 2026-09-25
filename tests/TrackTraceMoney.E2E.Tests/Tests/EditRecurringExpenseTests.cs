using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// A recurring expense can be fully edited (not just deactivated) and deleted after confirming.
/// </summary>
public sealed class EditRecurringExpenseTests : E2ETestBase
{
    private const string Scenario = "EditRecurringExpense";
    private const string AccountName = "E2E Subs Wallet";

    [Fact]
    public void RecurringExpense_CanBeEdited_AndDeleted()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();
            EnterText("CreateFirstProfile_NameEntry", "E2E SubsProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            TapTab("Accounts");
            Tap("AccountsList_AddButton");
            EnterText("AddAccount_NameEntry", AccountName);
            Tap("AddAccount_SaveButton");
            WaitForId($"AccountsList_Item_{AccountName}");

            TapTab("Settings");
            Tap("Settings_ManageRecurringExpensesButton");
            Tap("RecurringExpenses_AddButton");
            EnterText("AddRecurringExpense_NameEntry", "Netflix");
            EnterText("AddRecurringExpense_AmountEntry", "6.99");
            SelectFirstPickerOption("AddRecurringExpense_CategoryPicker");
            SelectPickerOption("AddRecurringExpense_AccountPicker", AccountName);
            Tap("AddRecurringExpense_SaveButton");

            // --- Edit: rename and change the amount ---
            Tap("RecurringExpenses_EditButton_Netflix");
            Assert.Equal("6.99", WaitForId("AddRecurringExpense_AmountEntry").Text);
            Assert.Equal("Netflix", WaitForId("AddRecurringExpense_NameEntry").Text);
            EnterText("AddRecurringExpense_NameEntry", "Netflix Premium");
            EnterText("AddRecurringExpense_AmountEntry", "7.99");
            Tap("AddRecurringExpense_SaveButton");

            WaitForId("RecurringExpenses_EditButton_Netflix Premium");
            WaitForAbsenceOfId("RecurringExpenses_EditButton_Netflix");
            Assert.True(ExactTextExists("Netflix Premium"));
            Screenshot(Scenario, "01-edited");

            // --- Delete, confirming the prompt ---
            Tap("RecurringExpenses_EditButton_Netflix Premium");
            Tap("AddRecurringExpense_DeleteButton");
            AcceptSystemDialog();

            WaitForId("RecurringExpenses_AddButton");
            WaitForAbsenceOfId("RecurringExpenses_EditButton_Netflix Premium");
            Screenshot(Scenario, "02-deleted");
        });
    }
}
