using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Accounts;

/// <summary>
/// A term deposit asset (README §22) — the fourth concrete subtype of <see cref="FinancialAccount"/>,
/// joining the existing hierarchy the same way <c>Loan</c> joined <c>CreditAccount</c> in Phase 2: no
/// new repository interface needed, <see cref="TrackTraceMoney.Application.Abstractions.IFinancialAccountRepository"/>
/// already works polymorphically across every subtype.
/// </summary>
public sealed class TermDeposit : FinancialAccount
{
    public string Institution { get; private set; } = null!;

    /// <summary>Fixed at origination — never mutated after construction, distinct from the inherited
    /// <see cref="FinancialAccount.Balance"/> (current value, which grows as interest is credited).</summary>
    public decimal InitialPrincipal { get; private set; }

    /// <summary>Informational only — never used to derive <see cref="EstimatedInterest"/>.</summary>
    public decimal Rate { get; private set; }

    public TermDepositRateType RateType { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly MaturityDate { get; private set; }

    public TermDepositInterestFrequency InterestFrequency { get; private set; }

    /// <summary>Stored flag only — no compound-interest math is computed anywhere from this.</summary>
    public bool IsCompounding { get; private set; }

    /// <summary>Optional, always user-entered — never derived from Rate/term/IsCompounding.</summary>
    public decimal? EstimatedInterest { get; private set; }

    /// <summary>Running total of interest actually credited. Mutated only via
    /// <see cref="RecordInterestReceived"/>, never settable directly after construction.</summary>
    public decimal InterestReceived { get; private set; }

    public bool AutoRenewal { get; private set; }

    private TermDeposit()
    {
    }

    public TermDeposit(
        string name,
        CurrencyCode currency,
        string institution,
        decimal initialPrincipal,
        decimal openingBalance,
        decimal rate,
        TermDepositRateType rateType,
        DateOnly startDate,
        DateOnly maturityDate,
        TermDepositInterestFrequency interestFrequency,
        bool isCompounding,
        bool autoRenewal,
        decimal? estimatedInterest = null,
        decimal interestReceived = 0m,
        string? notes = null)
        : base(name, currency, openingBalance, notes)
    {
        if (string.IsNullOrWhiteSpace(institution))
            throw new ArgumentException("Institution cannot be empty.", nameof(institution));
        if (institution.Trim().Length > 200)
            throw new ArgumentException("Institution cannot exceed 200 characters.", nameof(institution));
        if (initialPrincipal <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialPrincipal), "Initial principal must be positive.");
        if (openingBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(openingBalance), "Opening balance cannot be negative.");
        if (rate < 0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Rate cannot be negative.");
        if (maturityDate <= startDate)
            throw new ArgumentException("Maturity date must be after start date.", nameof(maturityDate));
        if (estimatedInterest < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedInterest), "Estimated interest cannot be negative.");
        if (interestReceived < 0)
            throw new ArgumentOutOfRangeException(nameof(interestReceived), "Interest received cannot be negative.");

        Institution = institution.Trim();
        InitialPrincipal = initialPrincipal;
        Rate = rate;
        RateType = rateType;
        StartDate = startDate;
        MaturityDate = maturityDate;
        InterestFrequency = interestFrequency;
        IsCompounding = isCompounding;
        EstimatedInterest = estimatedInterest;
        InterestReceived = interestReceived;
        AutoRenewal = autoRenewal;
    }

    /// <summary>
    /// Whether this account's balance counts toward "available balance" (README §30 Q1) — a term
    /// deposit's funds are locked until <see cref="MaturityDate"/>, so unlike every other
    /// <see cref="FinancialAccount"/> subtype it overrides the base default to <see langword="false"/>.
    /// </summary>
    public override bool CountsAsAvailableBalance => false;

    /// <summary>
    /// Credits interest actually received, mirroring CreditAccount.RegisterCharge's precedent: this
    /// method exists on the entity now but is deliberately unwired to any transaction type yet — that
    /// wiring is a later Phase 3 slice (a generalized "interest income" transaction).
    /// </summary>
    public void RecordInterestReceived(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Interest amount must be positive.");

        Credit(amount);
        InterestReceived += amount;
    }
}
