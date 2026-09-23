using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// Regression: editing an expense in the Health category used to post a second copy (and debit the
/// account twice). Loading the expense for editing auto-ticked the hidden "medical expense" flag, which
/// routed Save down the medical branch -- that branch records a new expense but never reverses the
/// original the way the normal edit path does.
/// </summary>
public sealed class EditHealthExpenseTests : E2ETestBase
{
    private const string Scenario = "EditHealthExpense";
    private const string AccountName = "E2E Health Wallet";
    private const string Description = "vitaminas";

    [Fact]
    public void EditingHealthExpense_ReplacesIt_WithoutDuplicatingOrDoubleDebiting()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E HealthProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            TapTab("Accounts");
            Tap("AccountsList_AddButton");
            EnterText("AddAccount_NameEntry", AccountName);
            Tap("AddAccount_SaveButton"); // Cash, USD, opening balance 0
            WaitForId($"AccountsList_Item_{AccountName}");

            // --- A plain (non-medical) Health expense: Health auto-ticks the medical box, so untick it ---
            TapTab("Transactions");
            Tap("TransactionsList_AddButton");
            EnterText("AddTransaction_AmountEntry", "22.80");
            SelectPickerOption("AddTransaction_ExpenseAccountPicker", AccountName);
            SelectPickerOption("AddTransaction_ExpenseCategoryPicker", "Health");
            var medicalCheckbox = WaitForId("AddTransaction_MedicalCheckbox");
            Assert.Equal("true", medicalCheckbox.GetAttribute("checked"));
            medicalCheckbox.Click();
            Assert.Equal("false", WaitForId("AddTransaction_MedicalCheckbox").GetAttribute("checked"));
            EnterText("AddTransaction_DescriptionEntry", Description);
            Tap("AddTransaction_SaveButton");
            WaitForId($"TransactionsList_Item_{Description}");
            Screenshot(Scenario, "01-health-expense-recorded");

            // --- Edit it from History: change the amount ---
            Tap("Transactions_ViewHistoryButton");
            Tap($"History_Item_{Description}");
            Tap("TransactionDetail_EditButton");
            EnterText("AddTransaction_AmountEntry", "25");
            Screenshot(Scenario, "02-editing");
            Tap("AddTransaction_SaveButton");

            // --- Assert: still exactly one entry, and the account was debited once, for the new amount ---
            WaitForId($"History_Item_{Description}");
            Assert.Equal(1, CountById($"History_Item_{Description}"));
            Screenshot(Scenario, "03-history-after-edit");

            TapTab("Accounts");
            Assert.Equal("-25.00", WaitForId($"AccountsList_Balance_{AccountName}").Text);
            Screenshot(Scenario, "04-balance-after-edit");
        });
    }
}
