using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// End-to-end coverage for Recurring Income (Settings -&gt; "Manage recurring income"), the flow
/// manually live-verified on 2026-09-19/2026-09-21 but never previously exercised by an automated
/// test. Drives the full lifecycle through the real installed app: add a recurring income, confirm
/// the half-month/monthly equivalent calculator reads correctly before saving, confirm a due
/// occurrence posts a real <c>Income</c> transaction that moves the destination account's balance
/// by the exact amount, confirm editing the amount afterward is prospective-only (the
/// already-posted occurrence's effect on the account balance is untouched --
/// <c>RecurringIncome.UpdateAmount</c>'s documented contract), and confirm a second, independent
/// recurring income can coexist (this is not a singleton "salary" field).
/// </summary>
public sealed class RecurringIncomeFlowTests : E2ETestBase
{
    private const string Scenario = "RecurringIncomeFlow";
    private const string AccountName = "E2E RecurIncome Cash";
    private const string FirstIncomeName = "Salario";
    private const string SecondIncomeName = "Freelance Bonus";

    [Fact]
    public void AddConfirmEditRecurringIncome_BehavesCorrectlyEndToEnd()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E RecurIncomeProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            // --- Arrange: a destination account is required before any recurring income can post
            // an Income transaction (same precondition as TransactionFlowTests). ---
            TapTab("Accounts");
            Tap("AccountsList_AddButton");
            EnterText("AddAccount_NameEntry", AccountName);
            Tap("AddAccount_SaveButton"); // Kind=Cash, Currency=USD, opening balance=0 are pre-filled defaults
            WaitForId($"AccountsList_Item_{AccountName}");
            Screenshot(Scenario, "01-account-created");

            Assert.Equal("0.00", WaitForId($"AccountsList_Balance_{AccountName}").Text);

            // --- Add a recurring income: "Salario" $960 twice a month, due immediately (StartDate
            // defaults to today, and a never-yet-confirmed recurring income's NextOccurrenceDate ==
            // StartDate -- see RecurringIncome.NextOccurrenceDate/IsDue) ---
            TapTab("Settings");
            Tap("Settings_ManageRecurringIncomeButton");
            Tap("RecurringIncomes_AddButton");

            EnterText("AddRecurringIncome_NameEntry", FirstIncomeName);
            EnterText("AddRecurringIncome_AmountEntry", "960");
            SelectFirstPickerOption("AddRecurringIncome_CategoryPicker");
            SelectPickerOption("AddRecurringIncome_AccountPicker", AccountName);
            SelectPickerOption("AddRecurringIncome_FrequencyPicker", "Twice a month (15th and last day)");

            // --- Assert: the equivalent-amount calculator reads correctly before saving --
            // $960 twice a month => 960.00 per half-month (itself) and 960 * 2 = 1,920.00 monthly
            // (see RecurringIncomeEquivalentCalculator) -- not 960 * 26/12, the every-14-days figure. ---
            Assert.Equal("≈ 960.00 per half-month", WaitForId("AddRecurringIncome_SemiMonthlyEquivalentLabel").Text);
            Assert.Equal("≈ 1,920.00 monthly", WaitForId("AddRecurringIncome_MonthlyEquivalentLabel").Text);
            Screenshot(Scenario, "02-equivalents-shown-before-save");

            Tap("AddRecurringIncome_SaveButton");

            var confirmButtonId = $"RecurringIncomes_ConfirmButton_{FirstIncomeName}";
            WaitForId(confirmButtonId);
            Screenshot(Scenario, "03-recurring-income-due");

            // --- Act: confirm the due occurrence ---
            Tap(confirmButtonId);
            WaitForAbsenceOfId(confirmButtonId); // next occurrence is 14 days out, so it drops off the Due list
            Screenshot(Scenario, "04-occurrence-confirmed");

            // --- Assert: a real Income transaction was posted (not a silent no-op) ---
            TapTab("Transactions");
            WaitForId($"TransactionsList_Item_{FirstIncomeName}");
            Screenshot(Scenario, "05-income-transaction-posted");

            // --- Assert: the destination account's balance moved by exactly the confirmed amount ---
            TapTab("Accounts");
            Assert.Equal("960.00", WaitForId($"AccountsList_Balance_{AccountName}").Text);
            Screenshot(Scenario, "06-balance-after-first-occurrence");

            // --- Act: raise the amount afterward (e.g. a salary raise). Switching tabs resets the
            // Settings tab to its root (AppShell.OnNavigated), so drill back into the list. ---
            TapTab("Settings");
            Tap("Settings_ManageRecurringIncomeButton");
            WaitForId($"RecurringIncomes_EditAmountButton_{FirstIncomeName}");
            Tap($"RecurringIncomes_EditAmountButton_{FirstIncomeName}");

            Assert.Equal("960.00", WaitForId("EditRecurringIncomeAmount_AmountEntry").Text);
            EnterText("EditRecurringIncomeAmount_AmountEntry", "1200");
            Tap("EditRecurringIncomeAmount_SaveButton");
            WaitForId($"RecurringIncomes_EditAmountButton_{FirstIncomeName}"); // back on the list, reloaded
            Screenshot(Scenario, "07-amount-edited");

            // --- Assert: prospective-only -- the already-posted occurrence's effect on the account
            // balance is untouched by the raise (RecurringIncome.UpdateAmount never retroactively
            // touches past balances) ---
            TapTab("Accounts");
            Assert.Equal("960.00", WaitForId($"AccountsList_Balance_{AccountName}").Text);
            Screenshot(Scenario, "08-balance-unchanged-after-edit");

            // --- Act: add a second, independent recurring income (not a singleton "salary" field).
            // Same tab-reset behavior as above: drill back in from the Settings root. ---
            TapTab("Settings");
            Tap("Settings_ManageRecurringIncomeButton");
            WaitForId("RecurringIncomes_AddButton");
            Tap("RecurringIncomes_AddButton");

            EnterText("AddRecurringIncome_NameEntry", SecondIncomeName);
            EnterText("AddRecurringIncome_AmountEntry", "300");
            SelectFirstPickerOption("AddRecurringIncome_CategoryPicker");
            SelectPickerOption("AddRecurringIncome_AccountPicker", AccountName);
            // Frequency left at its default (Monthly) -- this scenario doesn't care which cadence.
            Tap("AddRecurringIncome_SaveButton");

            // --- Assert: both recurring incomes coexist independently ---
            WaitForId($"RecurringIncomes_EditAmountButton_{SecondIncomeName}");
            Assert.True(ElementExists($"RecurringIncomes_EditAmountButton_{FirstIncomeName}"));
            Assert.True(ElementExists($"RecurringIncomes_EditAmountButton_{SecondIncomeName}"));
            Screenshot(Scenario, "09-second-recurring-income-added");
        });
    }
}
