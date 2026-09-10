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
    public string Institution { get; private set; } = null!;

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
        string institution,
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
        if (string.IsNullOrWhiteSpace(institution))
            throw new ArgumentException("Institution cannot be empty.", nameof(institution));

        if (institution.Trim().Length > 200)
            throw new ArgumentException("Institution cannot exceed 200 characters.", nameof(institution));

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

        Institution = institution.Trim();
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
