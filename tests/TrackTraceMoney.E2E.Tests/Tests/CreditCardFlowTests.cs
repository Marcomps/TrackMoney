using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// Add a credit card backed by a brand-new Financial Institution and Card Network created from scratch
/// in the same flow — the most recently built, highest-complexity feature as of this session (commit
/// 498255b), and the one most likely to have an undiscovered edge case (the InstitutionId/TPH
/// column-collision fix, the pooled backfill across four entity types, etc.).
/// </summary>
public sealed class CreditCardFlowTests : E2ETestBase
{
    private const string Scenario = "CreditCardFlow";
    private const string InstitutionName = "E2E Bank";
    private const string NetworkName = "E2E Network";
    private const string CardName = "E2E Visa";

    [Fact]
    public void AddCreditCard_WithNewInstitutionAndNetwork_AppearsCorrectlyInCreditList()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();

            EnterText("CreateFirstProfile_NameEntry", "E2E CardProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            // --- Create the Financial Institution from Settings ---
            TapTab("Settings");
            Tap("Settings_ManageFinancialInstitutionsButton");
            Tap("FinancialInstitutions_AddButton");
            EnterText("AddFinancialInstitution_NameEntry", InstitutionName);
            Tap("AddFinancialInstitution_SaveButton");

            WaitForId($"FinancialInstitutions_Item_{InstitutionName}");
            Screenshot(Scenario, "01-institution-created");
            GoBack(); // back to Settings

            // --- Create the Card Network from Settings ---
            WaitForId("Settings_ManageCardNetworksButton");
            Tap("Settings_ManageCardNetworksButton");
            Tap("CardNetworks_AddButton");
            EnterText("AddCardNetwork_NameEntry", NetworkName);
            Tap("AddCardNetwork_SaveButton");

            WaitForId($"CardNetworks_Item_{NetworkName}");
            Screenshot(Scenario, "02-network-created");
            GoBack(); // back to Settings

            // --- Add the credit card, referencing both brand-new lists ---
            TapTab("Credit");
            Tap("CreditCardsList_AddButton");
            EnterText("AddCreditCard_NameEntry", CardName);
            SelectPickerOption("AddCreditCard_InstitutionPicker", InstitutionName);
            SelectPickerOption("AddCreditCard_NetworkPicker", NetworkName);
            EnterText("AddCreditCard_CreditLimitEntry", "1000");
            EnterText("AddCreditCard_StatementCutOffDayEntry", "25");
            EnterText("AddCreditCard_PaymentDueDayEntry", "10");
            Screenshot(Scenario, "03-credit-card-form-filled");
            Tap("AddCreditCard_SaveButton");

            // --- Assert: renders correctly in the Credit list, referencing the new institution ---
            var row = WaitForId($"CreditCardsList_Row_{CardName}");
            Assert.True(row.Displayed);
            Screenshot(Scenario, "04-card-in-credit-list");

            var amountOwed = WaitForId($"CreditCardsList_AmountOwed_{CardName}");
            Assert.Equal("0.00", amountOwed.Text); // AddCreditCardViewModel's OpeningAmountOwedText default, left untouched
        });
    }
}
