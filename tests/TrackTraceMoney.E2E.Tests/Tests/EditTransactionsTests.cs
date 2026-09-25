using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// Editing from History must replace the transaction, never add a second one, and move balances exactly
/// once — for every editable type (expense is covered by <see cref="EditHealthExpenseTests"/>).
/// </summary>
public sealed class EditTransactionsTests : E2ETestBase
{
    [Fact]
    public void EditingIncome_ReplacesIt_WithoutDuplicatingOrDoubleCrediting()
    {
        const string scenario = "EditIncome";
        const string account = "E2E Income Wallet";
        const string description = "sueldo extra";

        RunScenario(scenario, () =>
        {
            StartWithAccounts(("E2E IncomeProfile", [(account, "0")]));

            TapTab("Transactions");
            Tap("TransactionsList_AddButton");
            SelectPickerOption("AddTransaction_TypePicker", "Income");
            EnterText("AddTransaction_AmountEntry", "100");
            SelectPickerOption("AddTransaction_IncomeDestinationAccountPicker", account);
            SelectFirstPickerOption("AddTransaction_IncomeCategoryPicker");
            EnterText("AddTransaction_DescriptionEntry", description);
            Tap("AddTransaction_SaveButton");
            WaitForId($"TransactionsList_Item_{description}");

            EditAmountFromHistory(description, "150");
            Screenshot(scenario, "01-history-after-edit");

            TapTab("Accounts");
            Assert.Equal("150.00", WaitForId($"AccountsList_Balance_{account}").Text);
        });
    }

    [Fact]
    public void EditingTransfer_ReplacesIt_WithoutDuplicatingOrDoubleMoving()
    {
        const string scenario = "EditTransfer";
        const string from = "E2E From";
        const string to = "E2E To";
        const string description = "ahorro";

        RunScenario(scenario, () =>
        {
            StartWithAccounts(("E2E TransferProfile", [(from, "500"), (to, "0")]));

            TapTab("Transactions");
            Tap("TransactionsList_AddButton");
            SelectPickerOption("AddTransaction_TypePicker", "Transfer");
            EnterText("AddTransaction_AmountEntry", "100");
            SelectPickerOption("AddTransaction_TransferSourceAccountPicker", from);
            SelectPickerOption("AddTransaction_TransferDestinationAccountPicker", to);
            EnterText("AddTransaction_DescriptionEntry", description);
            Tap("AddTransaction_SaveButton");
            WaitForId($"TransactionsList_Item_{description}");

            EditAmountFromHistory(description, "60");
            Screenshot(scenario, "01-history-after-edit");

            TapTab("Accounts");
            Assert.Equal("440.00", WaitForId($"AccountsList_Balance_{from}").Text);
            Assert.Equal("60.00", WaitForId($"AccountsList_Balance_{to}").Text);
        });
    }

    private void StartWithAccounts((string Profile, (string Name, string OpeningBalance)[] Accounts) setup)
    {
        ResetApp();
        EnterText("CreateFirstProfile_NameEntry", setup.Profile);
        Tap("CreateFirstProfile_SaveButton");
        WaitForId("Dashboard_HealthLabel");

        TapTab("Accounts");
        foreach (var (name, openingBalance) in setup.Accounts)
        {
            Tap("AccountsList_AddButton");
            EnterText("AddAccount_NameEntry", name);
            EnterText("AddAccount_OpeningBalanceEntry", openingBalance);
            Tap("AddAccount_SaveButton");
            WaitForId($"AccountsList_Item_{name}");
        }
    }

    /// <summary>Opens the transaction from History, changes its amount, saves, and asserts it's still listed once.</summary>
    private void EditAmountFromHistory(string description, string newAmount)
    {
        Tap("Transactions_ViewHistoryButton");
        Tap($"History_Item_{description}");
        Tap("TransactionDetail_EditButton");
        EnterText("AddTransaction_AmountEntry", newAmount);
        Tap("AddTransaction_SaveButton");

        WaitForId($"History_Item_{description}");
        Assert.Equal(1, CountById($"History_Item_{description}"));
    }
}
