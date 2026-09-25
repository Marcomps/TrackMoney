using System.Globalization;
using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// The Dashboard projection: a semi-monthly salary plus four monthly subscriptions, then "I got paid"
/// from the Dashboard. Every definition starts today, so the assertions hold whatever day this runs:
/// each subscription is due once by month end (6.99 + 25 + 20 + 10.99 = 62.98), and confirming the
/// salary only moves 654.52 from "expected" into the balance — the projected result must not change.
/// </summary>
public sealed class DashboardProjectionTests : E2ETestBase
{
    private const string Scenario = "DashboardProjection";
    private const string AccountName = "Agricola";
    private const string SalaryName = "Salario";
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void SalaryAndSubscriptions_ShowInProjection_AndConfirmingSalaryMovesItIntoBalance()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E ProjectionProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            TapTab("Accounts");
            Tap("AccountsList_AddButton");
            EnterText("AddAccount_NameEntry", AccountName);
            EnterText("AddAccount_OpeningBalanceEntry", "203.81");
            Tap("AddAccount_SaveButton");
            WaitForId($"AccountsList_Item_{AccountName}");

            // --- Salary: 654.52 twice a month ---
            TapTab("Settings");
            Tap("Settings_ManageRecurringIncomeButton");
            Tap("RecurringIncomes_AddButton");
            EnterText("AddRecurringIncome_NameEntry", SalaryName);
            EnterText("AddRecurringIncome_AmountEntry", "654.52");
            SelectFirstPickerOption("AddRecurringIncome_CategoryPicker");
            SelectPickerOption("AddRecurringIncome_AccountPicker", AccountName);
            SelectPickerOption("AddRecurringIncome_FrequencyPicker", "Twice a month (15th and last day)");
            Tap("AddRecurringIncome_SaveButton");
            WaitForId($"RecurringIncomes_EditAmountButton_{SalaryName}");
            GoBack(); // back to Settings

            // --- Subscriptions: monthly, default frequency ---
            Tap("Settings_ManageRecurringExpensesButton");
            foreach (var (name, amount) in new[] { ("Netflix", "6.99"), ("Claude Pro", "25"), ("Claude Max", "20"), ("Disney", "10.99") })
            {
                Tap("RecurringExpenses_AddButton");
                EnterText("AddRecurringExpense_NameEntry", name);
                EnterText("AddRecurringExpense_AmountEntry", amount);
                SelectFirstPickerOption("AddRecurringExpense_CategoryPicker");
                SelectPickerOption("AddRecurringExpense_AccountPicker", AccountName);
                Tap("AddRecurringExpense_SaveButton");
                WaitForId("RecurringExpenses_AddButton");
            }

            Screenshot(Scenario, "01-subscriptions-added");

            // --- Assert: the projection shows the salary as expected income and the subscriptions ---
            TapTab("Dashboard");
            Assert.Equal("62.98", WaitForId("Dashboard_ProjectionMonthExpenses_USD").Text);

            var expectedIncome = Amount("Dashboard_ProjectionMonthIncome_USD");
            Assert.True(expectedIncome >= 654.52m && expectedIncome % 654.52m == 0m, $"Expected income {expectedIncome} isn't a whole number of paychecks.");

            var projected = Amount("Dashboard_ProjectionMonthResult_USD");
            Assert.Equal(203.81m + expectedIncome - 62.98m, projected);
            Screenshot(Scenario, "02-projection-before-payday");

            // --- Act: "I got paid" (the salary starts today, so it's due -- no early-confirmation prompt) ---
            Tap($"Dashboard_ConfirmIncomeButton_{SalaryName}");

            // --- Assert: 654.52 moved from expected income into the month's real income; the projected
            // end-of-month figure is unchanged (the money was already counted, now it's actually there) ---
            WaitForText("Dashboard_IncomeAmount", "654.52");
            Assert.Equal(expectedIncome - 654.52m, Amount("Dashboard_ProjectionMonthIncome_USD"));
            Assert.Equal(projected, Amount("Dashboard_ProjectionMonthResult_USD"));
            Screenshot(Scenario, "03-after-got-paid");
        });
    }

    private decimal Amount(string automationId) =>
        decimal.Parse(WaitForId(automationId).Text, NumberStyles.Number, En);

    private void WaitForText(string automationId, string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (WaitForId(automationId).Text != text)
        {
            if (DateTime.UtcNow > deadline)
                Assert.Equal(text, WaitForId(automationId).Text);
            Thread.Sleep(300);
        }
    }
}
