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

    /// <summary>
    /// Rolls this matured term deposit into a brand-new one (README §22's AutoRenewal flag, unwired
    /// until now) and deactivates this one, mirroring how a bank actually closes the old CD/plazo fijo
    /// and opens a fresh one rather than mutating the original in place — keeping the original record
    /// immutable/historical, the same way <c>CreditCardPurchase</c>/<c>CreditCardPayment</c> stay two
    /// separate events rather than one mutated record.
    /// </summary>
    /// <remarks>
    /// The new deposit's <c>initialPrincipal</c>/<c>openingBalance</c> are set to this deposit's current
    /// <see cref="Balance"/> — deliberately NOT recomputed as <c>InitialPrincipal + InterestReceived</c>.
    /// <see cref="Balance"/> is the authoritative ledger value for this account and can legitimately
    /// diverge from that sum (e.g. via a Transfer into/out of this account, or a plain Income transaction
    /// that bypassed <see cref="RecordInterestReceived"/>), so re-deriving the renewal amount from the
    /// two interest-tracking fields instead of trusting the ledger would silently renew the wrong amount
    /// whenever those diverge.
    /// <para>
    /// The new deposit's <see cref="StartDate"/> is set to this deposit's <see cref="MaturityDate"/>, not
    /// to <paramref name="asOfDate"/> — a renewal check that runs a few days late (e.g. the user didn't
    /// open the app on the exact maturity date) must not shorten the new term or otherwise penalize the
    /// user for the app's own lateness in noticing.
    /// </para>
    /// <para>
    /// The new term length is preserved using day-count arithmetic (<c>DayNumber</c> subtraction/addition)
    /// rather than calendar-field arithmetic (e.g. "add N months"), because day counts are robust across
    /// leap years and variable month lengths — a 1-year term starting Feb 1 must renew to the same
    /// day-count length even though calendar month/day addition can land on a different day count
    /// depending on leap years.
    /// </para>
    /// </remarks>
    public TermDeposit RenewAtMaturity(DateOnly asOfDate)
    {
        if (!AutoRenewal)
            throw new InvalidOperationException("This term deposit does not have auto-renewal enabled.");
        if (asOfDate < MaturityDate)
            throw new InvalidOperationException("Cannot renew before maturity.");
        if (Balance <= 0)
            throw new InvalidOperationException("Cannot renew a term deposit with no remaining balance.");

        var termLengthDays = MaturityDate.DayNumber - StartDate.DayNumber;
        var newStartDate = MaturityDate;
        var newMaturityDate = newStartDate.AddDays(termLengthDays);

        var renewal = new TermDeposit(
            Name,
            Currency,
            Institution,
            initialPrincipal: Balance,
            openingBalance: Balance,
            Rate,
            RateType,
            newStartDate,
            newMaturityDate,
            InterestFrequency,
            IsCompounding,
            AutoRenewal,
            estimatedInterest: null,
            interestReceived: 0m,
            Notes);

        Deactivate();
        return renewal;
    }
}
