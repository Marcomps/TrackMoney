namespace TrackTraceMoney.Domain.TransactionPresets;

/// <summary>
/// The closed set of <c>TransactionType</c>s a <see cref="TransactionPreset"/> may pre-fill (Transaction
/// Type Customization slice spec §B.3) -- exactly the three types with no cross-account-type or
/// schedule-mutation entanglement. Deliberately excludes <c>CreditCardPurchase</c>/<c>CreditCardPayment</c>/
/// <c>LoanPayment</c>/<c>InvestmentContribution</c>/<c>InvestmentWithdrawal</c>/<c>InterestIncome</c>/
/// <c>Reimbursement</c> -- every one of those either targets a second, non-<c>FinancialAccount</c> entity,
/// mutates a schedule, or is already auto-routed/context-detected rather than user-picked. Stays exactly
/// these 3 values even for the card-backed-preset follow-up (§B.7.2) -- see
/// <see cref="TransactionPreset.DefaultCreditAccountId"/>'s doc comment for why that's a widened field,
/// not a widened <see cref="TransactionPresetBaseType"/>.
/// </summary>
public enum TransactionPresetBaseType
{
    Expense,
    Income,
    Transfer
}
