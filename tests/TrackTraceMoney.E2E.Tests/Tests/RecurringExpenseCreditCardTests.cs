using System.Globalization;
using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// A recurring expense backed by a credit card must post a real <c>CreditCardPurchase</c> when its due
/// occurrence is confirmed — not a plain <c>Expense</c>, and not a silent no-op — so the card's
/// <c>AmountOwed</c> actually moves (commit 498255b; manually verified once this session: confirming a
/// $20/month charge increased a card's AmountOwed by exactly 20.00). This test asserts the exact same
/// numeric delta automatically instead of trusting a one-off manual run.
/// </summary>
public sealed class RecurringExpenseCreditCardTests : E2ETestBase
{
    private const string Scenario = "RecurringExpenseCreditCard";
    private const string InstitutionName = "E2E Recur Bank";
    private const string CardName = "E2E Recur Visa";
    private const string CardPickerText = "💳 " + CardName; // AddRecurringExpenseViewModel's own display prefix
    private const string RecurringExpenseName = "E2E ChatGPT";

    [Fact]
    public void ConfirmingCreditCardBackedOccurrence_IncreasesAmountOwedByExactAmount()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E RecurProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            // --- Arrange: an institution + a credit card to charge the recurring expense against ---
            TapTab("Settings");
            Tap("Settings_ManageFinancialInstitutionsButton");
            Tap("FinancialInstitutions_AddButton");
            EnterText("AddFinancialInstitution_NameEntry", InstitutionName);
            Tap("AddFinancialInstitution_SaveButton");
            WaitForId($"FinancialInstitutions_Item_{InstitutionName}");
            GoBack();

            WaitForId("Settings_ManageFinancialInstitutionsButton");
            TapTab("Credit");
            Tap("CreditCardsList_AddButton");
            EnterText("AddCreditCard_NameEntry", CardName);
            SelectPickerOption("AddCreditCard_InstitutionPicker", InstitutionName);
            EnterText("AddCreditCard_CreditLimitEntry", "1000");
            EnterText("AddCreditCard_StatementCutOffDayEntry", "25");
            EnterText("AddCreditCard_PaymentDueDayEntry", "10");
            Tap("AddCreditCard_SaveButton");
            WaitForId($"CreditCardsList_Row_{CardName}");
            Screenshot(Scenario, "01-card-created");

            var amountOwedBeforeText = WaitForId($"CreditCardsList_AmountOwed_{CardName}").Text;
            var amountOwedBefore = decimal.Parse(amountOwedBeforeText, NumberStyles.Number, CultureInfo.InvariantCulture);

            // --- Create a recurring expense charged to that card, due immediately (StartDate defaults
            // to today, and a never-yet-confirmed recurring expense's NextOccurrenceDate == StartDate —
            // see RecurringExpense.NextOccurrenceDate/IsDue) ---
            TapTab("Settings");
            Tap("Settings_ManageRecurringExpensesButton");
            Tap("RecurringExpenses_AddButton");
            EnterText("AddRecurringExpense_NameEntry", RecurringExpenseName);
            EnterText("AddRecurringExpense_AmountEntry", "20");
            SelectFirstPickerOption("AddRecurringExpense_CategoryPicker");
            SelectPickerOption("AddRecurringExpense_AccountPicker", CardPickerText);
            Screenshot(Scenario, "02-recurring-expense-form-filled");
            Tap("AddRecurringExpense_SaveButton");

            var confirmButtonId = $"RecurringExpenses_ConfirmButton_{RecurringExpenseName}";
            WaitForId(confirmButtonId);
            Screenshot(Scenario, "03-recurring-expense-due");

            // --- Act: confirm the due occurrence ---
            Tap(confirmButtonId);
            WaitForAbsenceOfId(confirmButtonId); // next occurrence is a month out, so it drops off the Due list
            Screenshot(Scenario, "04-occurrence-confirmed");

            // --- Assert: the card's AmountOwed increased by exactly the recurring expense's amount,
            // and this was recorded as a real CreditCardPurchase (spend + debt), not a no-op ---
            TapTab("Credit");
            var amountOwedAfterText = WaitForId($"CreditCardsList_AmountOwed_{CardName}").Text;
            var amountOwedAfter = decimal.Parse(amountOwedAfterText, NumberStyles.Number, CultureInfo.InvariantCulture);
            Screenshot(Scenario, "05-amount-owed-after");

            Assert.Equal(20.00m, amountOwedAfter - amountOwedBefore);
        });
    }
}
