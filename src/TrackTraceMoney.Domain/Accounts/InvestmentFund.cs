using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Accounts;

/// <summary>
/// An investment fund asset (README §23) — the fifth concrete subtype of <see cref="FinancialAccount"/>,
/// joining the hierarchy the same way <see cref="TermDeposit"/> did: no new IFinancialAccountRepository
/// method needed. §47's conceptual data model gives this entity a child, <see cref="InvestmentValuation"/>
/// — unlike TermDeposit, which has no such child.
/// </summary>
public sealed class InvestmentFund : FinancialAccount
{
    /// <summary>
    /// Legacy free-text institution, superseded by <see cref="InstitutionId"/> (see the
    /// financial-institution-card-network-slice-spec's Decision 2). Kept — nullable, no longer
    /// constructor-validated — only so <c>FinancialInstitutionBackfillService</c> can read a
    /// not-yet-backfilled row's value; no code path after this slice writes it. Never dropped this
    /// slice (zero production users; see the spec).
    /// </summary>
    public string? Institution { get; private set; }

    /// <summary>
    /// The fund's <c>FinancialInstitution</c>. Nullable only to let EF materialize legacy rows
    /// (pre-dating this slice) that have not yet been backfilled — every public constructor still
    /// requires a real, non-empty id; <see langword="null"/> is only ever reachable via EF's private
    /// parameterless constructor.
    /// </summary>
    public Guid? InstitutionId { get; private set; }

    public DateOnly InvestmentDate { get; private set; }

    /// <summary>Running total of money put into the fund, including the amount at construction.
    /// Mutated only via <see cref="RecordContribution"/>, never settable directly after construction.</summary>
    public decimal Contributions { get; private set; }

    /// <summary>Running total of money taken out of the fund. Mutated only via
    /// <see cref="RecordWithdrawal"/>, never settable directly after construction.</summary>
    public decimal Withdrawals { get; private set; }

    /// <summary>Running total of fees paid against this fund — informational only, deliberately NOT
    /// netted into <see cref="Gain"/>/<see cref="ReturnPercentage"/> (no way to know whether an
    /// institution's reported "Current value" is already fee-net).</summary>
    public decimal Fees { get; private set; }

    private InvestmentFund() { }

    public InvestmentFund(
        string name,
        CurrencyCode currency,
        Guid institutionId,
        DateOnly investmentDate,
        decimal contributions,
        decimal openingBalance,
        decimal withdrawals = 0m,
        decimal fees = 0m,
        string? notes = null)
        : base(name, currency, openingBalance, notes)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id is required.", nameof(institutionId));
        if (contributions <= 0)
            throw new ArgumentOutOfRangeException(nameof(contributions), "Contributions must be positive.");
        if (openingBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(openingBalance), "Opening balance cannot be negative.");
        if (withdrawals < 0)
            throw new ArgumentOutOfRangeException(nameof(withdrawals), "Withdrawals cannot be negative.");
        if (fees < 0)
            throw new ArgumentOutOfRangeException(nameof(fees), "Fees cannot be negative.");

        InstitutionId = institutionId;
        InvestmentDate = investmentDate;
        Contributions = contributions;
        Withdrawals = withdrawals;
        Fees = fees;
    }

    /// <summary>
    /// Sets <see cref="InstitutionId"/> on a legacy row (constructed via EF's private materialization
    /// path, so its <see cref="InstitutionId"/> is transiently null) — called only by
    /// <c>FinancialInstitutionBackfillService</c>. Every row reachable through the public constructor
    /// already has a non-null <see cref="InstitutionId"/> and never needs this.
    /// </summary>
    public void SetInstitutionId(Guid institutionId)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id is required.", nameof(institutionId));

        InstitutionId = institutionId;
    }

    /// <summary>README §23's "Gain" — Current value minus net money contributed. Can be negative.
    /// Always derived, never stored, so it can't drift out of sync with Balance/Contributions/Withdrawals.</summary>
    public decimal Gain => Balance - (Contributions - Withdrawals);

    /// <summary>README §23's "Return" as a raw fraction (0.0423m, not "4.23"), consistent with how
    /// TermDeposit.Rate is stored. Null when net contributed is exactly zero, to avoid a divide-by-zero.</summary>
    public decimal? ReturnPercentage
    {
        get
        {
            var netContributed = Contributions - Withdrawals;
            return netContributed == 0m ? null : Gain / netContributed;
        }
    }

    public override bool CountsAsAvailableBalance => false;

    /// <summary>Mirrors TermDeposit.RecordInterestReceived's precedent: defined now, deliberately
    /// unwired to any transaction type — that wiring is a later slice (InvestmentContribution).</summary>
    public void RecordContribution(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Contribution amount must be positive.");

        Credit(amount);
        Contributions += amount;
    }

    /// <summary>Same "defined but unwired" status as RecordContribution — a later slice's job
    /// (InvestmentWithdrawal). Mirrors Debit's existing lax convention: no overdraft guard.</summary>
    public void RecordWithdrawal(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive.");

        Debit(amount);
        Withdrawals += amount;
    }

    /// <summary>Sets Current value to a freshly observed number (README §23's "history... to generate
    /// charts"). An absolute set, not a delta — a market revaluation is a re-observation of worth, not
    /// money moved. This IS fully wired this slice (called from RecordInvestmentValuationViewModel),
    /// unlike RecordContribution/RecordWithdrawal above.</summary>
    public void RecordValuation(decimal value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value cannot be negative.");

        var delta = value - Balance;
        if (delta > 0) Credit(delta);
        else if (delta < 0) Debit(-delta);
    }
}
