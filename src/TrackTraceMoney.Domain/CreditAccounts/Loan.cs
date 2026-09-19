using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.CreditAccounts;

/// <summary>
/// A loan liability — the second concrete subtype of <see cref="CreditAccount"/> (README §19), covering
/// personal loans, auto loans, mortgages, bank credit, installment purchases, and other loan kinds
/// (<see cref="LoanKind"/>). Unlike <see cref="CreditCard"/>, a loan does not take on new "purchases" —
/// in this model its balance only ever decreases via payments, so <see cref="CreditAccount.RegisterCharge"/>
/// is inherited but deliberately left unused by every Loan code path in this and later slices.
/// </summary>
public sealed class Loan : CreditAccount
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
    /// The loan's <c>FinancialInstitution</c>. Nullable only to let EF materialize legacy rows
    /// (pre-dating this slice) that have not yet been backfilled — every public constructor still
    /// requires a real, non-empty id; <see langword="null"/> is only ever reachable via EF's private
    /// parameterless constructor.
    /// </summary>
    public Guid? InstitutionId { get; private set; }

    public LoanKind Kind { get; private set; }

    /// <summary>
    /// Principal disbursed/financed at origination. Fixed for the life of the loan in this model — never
    /// mutated after construction, distinct from the inherited <see cref="CreditAccount.AmountOwed"/>
    /// ("Current balance" per README §19), which decreases as payments are registered.
    /// </summary>
    public decimal OriginalAmount { get; private set; }

    /// <summary>
    /// The loan's contracted rate. Informational, like <see cref="CreditCard.AnnualInterestRate"/> — never
    /// used to derive <see cref="MonthlyInstallment"/> or <see cref="RequiredPayment"/>, both of which are
    /// user-entered.
    /// </summary>
    public decimal InterestRate { get; private set; }

    public LoanRateType RateType { get; private set; }

    /// <summary>
    /// The loan's scheduled recurring payment amount per its payment plan — a term fixed at origination,
    /// not a per-cycle "amount currently due." See <see cref="RequiredPayment"/> for the latter.
    /// </summary>
    public decimal MonthlyInstallment { get; private set; }

    /// <summary>
    /// Stored, not computed. Unlike CreditCard, a loan has no stored cutoff/due-day pair to recompute a
    /// next date from — README §19 models it as a single stored due date. Advancing this after a payment
    /// is registered is LoanPayment's responsibility (a later Phase 2 slice); this slice only records the
    /// value supplied at loan creation.
    /// </summary>
    public DateOnly NextPaymentDate { get; private set; }

    /// <summary>
    /// Computed, not stored, so it can't drift once LoanPayment (a later slice) starts reducing
    /// <see cref="CreditAccount.AmountOwed"/> — mirrors <see cref="CreditCard.AvailableCredit"/>'s
    /// computed-from-stored-fields pattern. An estimate under a level-installment assumption (a real
    /// amortization schedule can have a smaller final payment).
    /// </summary>
    public int RemainingPayments => MonthlyInstallment > 0
        ? (int)Math.Ceiling(AmountOwed / MonthlyInstallment)
        : 0;

    /// <summary>
    /// The amount actually due for the next payment. Normally equal to <see cref="MonthlyInstallment"/>,
    /// but can diverge (a fee added to this cycle, a smaller final payment) — always user-entered, never
    /// derived from <see cref="MonthlyInstallment"/>.
    /// </summary>
    public decimal RequiredPayment { get; private set; }

    /// <summary>
    /// One-off fees at origination (e.g. origination/appraisal fees). Informational only — never folded
    /// into <see cref="CreditAccount.AmountOwed"/> automatically.
    /// </summary>
    public decimal? Fees { get; private set; }

    private Loan()
    {
    }

    public Loan(
        string name,
        CurrencyCode currency,
        Guid institutionId,
        LoanKind kind,
        decimal originalAmount,
        decimal currentBalance,
        decimal interestRate,
        LoanRateType rateType,
        decimal monthlyInstallment,
        DateOnly nextPaymentDate,
        decimal requiredPayment,
        decimal? fees = null,
        string? notes = null)
        : base(name, currency, currentBalance, notes)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id is required.", nameof(institutionId));

        if (originalAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(originalAmount), "Original amount must be positive.");

        if (interestRate < 0)
            throw new ArgumentOutOfRangeException(nameof(interestRate), "Interest rate cannot be negative.");

        if (monthlyInstallment <= 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyInstallment), "Monthly installment must be positive.");

        if (requiredPayment <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredPayment), "Required payment must be positive.");

        if (fees < 0)
            throw new ArgumentOutOfRangeException(nameof(fees), "Fees cannot be negative.");

        InstitutionId = institutionId;
        Kind = kind;
        OriginalAmount = originalAmount;
        InterestRate = interestRate;
        RateType = rateType;
        MonthlyInstallment = monthlyInstallment;
        NextPaymentDate = nextPaymentDate;
        RequiredPayment = requiredPayment;
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

    /// <summary>
    /// Edits every in-place-editable field on this loan except its schedule (edit/delete slice spec
    /// §3.1) — validation mirrors the constructor's exactly. <see cref="CreditAccount.AmountOwed"/> and
    /// <see cref="OriginalAmount"/> are deliberately not parameters here: <c>AmountOwed</c> has no
    /// direct setter anywhere except <see cref="CreditAccount.RegisterPayment"/>, and
    /// <see cref="OriginalAmount"/> is fixed for the life of the loan per this class's own doc comment
    /// — this mutator must not become a backdoor around either invariant.
    /// <see cref="NextPaymentDate"/>/<see cref="RequiredPayment"/> are edited via the existing
    /// <see cref="AdvanceSchedule"/>, not here.
    /// </summary>
    public void UpdateDetails(
        Guid institutionId,
        LoanKind kind,
        decimal interestRate,
        LoanRateType rateType,
        decimal monthlyInstallment,
        decimal? fees)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id is required.", nameof(institutionId));

        if (interestRate < 0)
            throw new ArgumentOutOfRangeException(nameof(interestRate), "Interest rate cannot be negative.");

        if (monthlyInstallment <= 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyInstallment), "Monthly installment must be positive.");

        if (fees < 0)
            throw new ArgumentOutOfRangeException(nameof(fees), "Fees cannot be negative.");

        InstitutionId = institutionId;
        Kind = kind;
        InterestRate = interestRate;
        RateType = rateType;
        MonthlyInstallment = monthlyInstallment;
        Fees = fees;
    }

    /// <summary>
    /// Advances this loan's forward-looking schedule after a payment is recorded (README §19). Called by
    /// LoanPayment's recording flow, never invoked standalone — both values are supplied by the caller
    /// (ultimately user-entered on the Add Transaction screen), mirroring RequiredPayment's existing
    /// "always user-entered, never derived" invariant. Does not touch AmountOwed — that's
    /// CreditAccount.RegisterPayment's job, called separately.
    /// </summary>
    public void AdvanceSchedule(DateOnly nextPaymentDate, decimal requiredPayment)
    {
        if (requiredPayment <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredPayment), "Required payment must be positive.");

        NextPaymentDate = nextPaymentDate;
        RequiredPayment = requiredPayment;
    }
}
