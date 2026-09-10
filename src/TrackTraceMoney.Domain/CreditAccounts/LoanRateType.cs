namespace TrackTraceMoney.Domain.CreditAccounts;

/// <summary>
/// Whether Loan.InterestRate is contractually fixed for the loan's term or can change. README §19 names
/// a "Rate type" field without enumerating its values — Fixed/Variable is the standard, bank-agnostic
/// categorization.
/// </summary>
public enum LoanRateType
{
    Fixed,
    Variable
}
