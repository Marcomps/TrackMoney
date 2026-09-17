using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// Records one Expense and one Income against a freshly-created Cash account and confirms both (a)
/// appear in the Transactions list and (b) roll up correctly into Dashboard's Income/Expenses/Available
/// tile for the current month — the exact flow the user drove manually, repeatedly, earlier this
/// session. <see cref="TrackTraceMoney.App.Models.CurrencyIncomeExpense.Available"/> is a plain
/// <c>Income - Expenses</c>, asserted here as an exact string match, not just "some number appeared".
/// </summary>
public sealed class TransactionFlowTests : E2ETestBase
{
    private const string Scenario = "TransactionFlow";
    private const string AccountName = "E2E Cash";
    private const string ExpenseDescription = "E2E Expense Groceries";
    private const string IncomeDescription = "E2E Income Salary";

    [Fact]
    public void RecordExpenseAndIncome_RollUpCorrectlyOnDashboard()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E TxProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            // --- Arrange: a Cash/USD account is required before any Expense/Income can be recorded
            // (no account is seeded by default — see AddAccountViewModel's Cash/USD defaults). ---
            TapTab("Accounts");
            Tap("AccountsList_AddButton");
            EnterText("AddAccount_NameEntry", AccountName);
            Tap("AddAccount_SaveButton"); // Kind=Cash, Currency=USD, opening balance=0 are all pre-filled defaults
            WaitForId($"AccountsList_Item_{AccountName}");
            Screenshot(Scenario, "01-account-created");

            // --- Record an Expense (default SelectedType is already Expense) ---
            TapTab("Transactions");
            Tap("TransactionsList_AddButton");
            EnterText("AddTransaction_AmountEntry", "45.50");
            SelectPickerOption("AddTransaction_ExpenseAccountPicker", AccountName);
            SelectFirstPickerOption("AddTransaction_ExpenseCategoryPicker");
            EnterText("AddTransaction_DescriptionEntry", ExpenseDescription);
            Screenshot(Scenario, "02-expense-form-filled");
            Tap("AddTransaction_SaveButton");

            WaitForId($"TransactionsList_Item_{ExpenseDescription}");
            Screenshot(Scenario, "03-after-expense-saved");

            // --- Record an Income ---
            Tap("TransactionsList_AddButton");
            SelectPickerOption("AddTransaction_TypePicker", "Income");
            EnterText("AddTransaction_AmountEntry", "1800");
            SelectPickerOption("AddTransaction_IncomeDestinationAccountPicker", AccountName);
            SelectFirstPickerOption("AddTransaction_IncomeCategoryPicker");
            EnterText("AddTransaction_DescriptionEntry", IncomeDescription);
            Screenshot(Scenario, "04-income-form-filled");
            Tap("AddTransaction_SaveButton");

            WaitForId($"TransactionsList_Item_{IncomeDescription}");
            Screenshot(Scenario, "05-after-income-saved");

            // --- Assert: both transactions really are listed (no double-counting / no silent drop) ---
            Assert.True(ElementExists($"TransactionsList_Item_{ExpenseDescription}"));
            Assert.True(ElementExists($"TransactionsList_Item_{IncomeDescription}"));

            // --- Assert: Dashboard's Income/Expenses/Available tile reflects exactly these two
            // transactions ---
            TapTab("Dashboard"); // OnAppearing re-invokes LoadDashboardCommand every time this tab is revisited

            var income = WaitForId("Dashboard_IncomeAmount");
            var expenses = WaitForId("Dashboard_ExpensesAmount");
            var available = WaitForId("Dashboard_AvailableAmount");
            Screenshot(Scenario, "06-dashboard-reflects-transactions");

            Assert.Equal("1,800.00", income.Text);
            Assert.Equal("45.50", expenses.Text);
            Assert.Equal("1,754.50", available.Text); // Available = Income - Expenses, see CurrencyIncomeExpense
        });
    }
}
