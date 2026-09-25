using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// The Accounts tab shows bank/cash accounts, term deposits and investment funds on one scrolling view,
/// and the Credit tab shows cards and loans together — nothing hidden behind "View …" buttons anymore.
/// A loan added from the Credit tab must appear in that tab's loans section on return.
/// </summary>
public sealed class CombinedTabsTests : E2ETestBase
{
    private const string Scenario = "CombinedTabs";
    private const string InstitutionName = "E2E Loan Bank";
    private const string LoanName = "E2E Car Loan";

    [Fact]
    public void AccountsAndCreditTabs_ShowEverySectionInline()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E TabsProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            // --- Accounts tab: all three sections present, no drill-in needed ---
            TapTab("Accounts");
            WaitForId("AccountsList_AddButton");
            WaitForId("TermDepositsList_AddButton");
            WaitForId("InvestmentFundsList_AddButton");
            Screenshot(Scenario, "01-accounts-tab-sections");

            // --- A loan needs an institution ---
            TapTab("Settings");
            Tap("Settings_ManageFinancialInstitutionsButton");
            Tap("FinancialInstitutions_AddButton");
            EnterText("AddFinancialInstitution_NameEntry", InstitutionName);
            Tap("AddFinancialInstitution_SaveButton");
            WaitForId($"FinancialInstitutions_Item_{InstitutionName}");

            // --- Credit tab: cards + loans sections; add a loan from the loans section ---
            TapTab("Credit");
            WaitForId("CreditCardsList_AddButton");
            Tap("LoansList_AddButton");
            EnterText("AddLoan_NameEntry", LoanName);
            SelectPickerOption("AddLoan_InstitutionPicker", InstitutionName);
            EnterText("AddLoan_OriginalAmountEntry", "5000");
            EnterText("AddLoan_CurrentBalanceEntry", "4000");
            EnterText("AddLoan_InterestRateEntry", "12");
            EnterText("AddLoan_MonthlyInstallmentEntry", "150");
            EnterText("AddLoan_RequiredPaymentEntry", "150");
            Tap("AddLoan_SaveButton");

            // --- Assert: back on the Credit tab, the loan is listed inline, next to the cards ---
            WaitForId($"LoansList_Item_{LoanName}");
            WaitForId("CreditCardsList_AddButton");
            WaitForId("CreditCardsList_SnowballButton");
            Screenshot(Scenario, "02-credit-tab-with-loan");

            // --- Snowball: owed 4,000, minimum 150, +100 extra => 250/month, paid off in 16 months ---
            Tap("CreditCardsList_SnowballButton");
            WaitForId($"SnowballPlan_Line_{LoanName}");
            EnterText("SnowballPlan_ExtraEntry_USD", "100");
            Assert.Equal("Pay this month: 250.00", WaitForId("SnowballPlan_PayThisMonth_USD").Text);
            Assert.StartsWith("Debt-free in ≈ 16 months", WaitForId("SnowballPlan_DebtFree_USD").Text);
            Screenshot(Scenario, "03-snowball-plan");
        });
    }
}
