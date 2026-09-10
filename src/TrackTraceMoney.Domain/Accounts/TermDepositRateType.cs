namespace TrackTraceMoney.Domain.Accounts;

/// <summary>
/// Nominal vs. effective annual rate — not Fixed/Variable (LoanRateType's distinction) because a term
/// deposit is contractually fixed-rate for its term by definition; nominal-vs-effective is the
/// distinction that actually matters here.
/// </summary>
public enum TermDepositRateType
{
    Nominal,
    Effective
}
