using TrackTraceMoney.E2E.Tests.Infrastructure;

namespace TrackTraceMoney.E2E.Tests.Tests;

/// <summary>
/// A card purchase in the open cycle (no statement recorded yet) can be edited from History — replaced,
/// never duplicated, with the card's debt adjusted exactly once — and deleted, which removes the charge.
/// </summary>
public sealed class EditCreditCardPurchaseTests : E2ETestBase
{
    private const string Scenario = "EditCreditCardPurchase";
    private const string InstitutionName = "E2E Card Bank";
    private const string NetworkName = "E2E Card Network";
    private const string CardName = "E2E Mi Super";
    private const string Description = "Claude";

    [Fact]
    public void CardPurchase_CanBeEdited_WithoutDuplicating_AndDeleted()
    {
        RunScenario(Scenario, () =>
        {
            ResetApp();
            EnterText("CreateFirstProfile_NameEntry", "E2E CardEditProfile");
            Tap("CreateFirstProfile_SaveButton");
            WaitForId("Dashboard_HealthLabel");

            // --- A card needs an institution and a network ---
            TapTab("Settings");
            Tap("Settings_ManageFinancialInstitutionsButton");
            Tap("FinancialInstitutions_AddButton");
            EnterText("AddFinancialInstitution_NameEntry", InstitutionName);
            Tap("AddFinancialInstitution_SaveButton");
            WaitForId($"FinancialInstitutions_Item_{InstitutionName}");
            GoBack();
            Tap("Settings_ManageCardNetworksButton");
            Tap("CardNetworks_AddButton");
            EnterText("AddCardNetwork_NameEntry", NetworkName);
            Tap("AddCardNetwork_SaveButton");
            WaitForId($"CardNetworks_Item_{NetworkName}");
            GoBack();

            TapTab("Credit");
            Tap("CreditCardsList_AddButton");
            EnterText("AddCreditCard_NameEntry", CardName);
            SelectPickerOption("AddCreditCard_InstitutionPicker", InstitutionName);
            SelectPickerOption("AddCreditCard_NetworkPicker", NetworkName);
            EnterText("AddCreditCard_CreditLimitEntry", "1000");
            EnterText("AddCreditCard_StatementCutOffDayEntry", "25");
            EnterText("AddCreditCard_PaymentDueDayEntry", "10");
            Tap("AddCreditCard_SaveButton");
            WaitForId($"CreditCardsList_Row_{CardName}");

            // --- The purchase: 25 on the card ---
            TapTab("Transactions");
            Tap("TransactionsList_AddButton");
            EnterText("AddTransaction_AmountEntry", "25");
            SelectPickerOption("AddTransaction_ExpenseAccountPicker", $"💳 {CardName}");
            SelectFirstPickerOption("AddTransaction_ExpenseCategoryPicker");
            UntickMedicalIfTicked();
            EnterText("AddTransaction_DescriptionEntry", Description);
            Tap("AddTransaction_SaveButton");
            WaitForId($"TransactionsList_Item_{Description}");
            AssertCardOwes("25.00");

            // --- Edit from History: 25 -> 20 ---
            TapTab("Transactions");
            Tap("Transactions_ViewHistoryButton");
            Tap($"History_Item_{Description}");
            Tap("TransactionDetail_EditButton");
            EnterText("AddTransaction_AmountEntry", "20");
            Tap("AddTransaction_SaveButton");
            WaitForId($"History_Item_{Description}");
            Assert.Equal(1, CountById($"History_Item_{Description}"));
            Screenshot(Scenario, "01-history-after-edit");
            AssertCardOwes("20.00");

            // --- Delete it: the charge goes away ---
            TapTab("Transactions");
            Tap("Transactions_ViewHistoryButton");
            Tap($"History_Item_{Description}");
            Tap("TransactionDetail_DeleteButton");
            AcceptSystemDialog();
            WaitForAbsenceOfId($"History_Item_{Description}");
            AssertCardOwes("0.00");
            Screenshot(Scenario, "02-card-after-delete");
        });
    }

    private void AssertCardOwes(string expected)
    {
        TapTab("Credit");
        Assert.Equal(expected, WaitForId($"CreditCardsList_AmountOwed_{CardName}").Text);
    }

    /// <summary>Some categories auto-tick "medical expense"; this test is about a plain purchase.</summary>
    private void UntickMedicalIfTicked()
    {
        if (ElementExists("AddTransaction_MedicalCheckbox") && WaitForId("AddTransaction_MedicalCheckbox").GetAttribute("checked") == "true")
            Tap("AddTransaction_MedicalCheckbox");
    }
}
