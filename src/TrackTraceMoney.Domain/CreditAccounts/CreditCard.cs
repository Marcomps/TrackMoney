using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.CreditAccounts;

/// <summary>
/// A credit card liability (README §8, §47 Phase 2). Statement-cycle concepts (cutoff/due dates are
/// stored here as the card's recurring configuration, but the per-cycle statement itself — with its
/// own "pago mínimo"/"pago para evitar intereses" — is a later slice's <c>CreditCardStatement</c>,
/// deliberately not modeled yet).
/// </summary>
public sealed class CreditCard : CreditAccount
{
    /// <summary>
    /// Legacy free-text issuer, superseded by <see cref="InstitutionId"/> (see the
    /// financial-institution-card-network-slice-spec's Decision 2). Kept — nullable, no longer
    /// constructor-validated — only so <c>FinancialInstitutionBackfillService</c> can read a
    /// not-yet-backfilled row's value; no code path after this slice writes it (new cards only ever set
    /// <see cref="InstitutionId"/>). Never dropped this slice (zero production users; see the spec).
    /// </summary>
    public string? Issuer { get; private set; }

    /// <summary>
    /// The card's issuing <c>FinancialInstitution</c> — a card's issuer bank IS its institution, so this
    /// folds in what <see cref="Issuer"/> used to mean. Nullable only to let EF materialize legacy rows
    /// (pre-dating this slice) that have not yet been backfilled — every public constructor still
    /// requires a real, non-empty id; <see langword="null"/> is only ever reachable via EF's private
    /// parameterless constructor.
    /// </summary>
    public Guid? InstitutionId { get; private set; }

    /// <summary>
    /// The card's network/brand (e.g. Visa, Mastercard) — Slice B of the slice spec. Genuinely optional
    /// (unlike <see cref="InstitutionId"/>, which preserves <see cref="Issuer"/>'s old required-ness):
    /// this is a brand-new enrichment field with no prior free-text data to replace, and nothing in this
    /// app's accounting rules (semáforo, purchased-vs-paid, snowball) ever references it.
    /// </summary>
    public Guid? NetworkId { get; private set; }

    /// <summary>
    /// Optional, masked identifier for display (e.g. "•••• 1234"). When provided, must be exactly
    /// 4 numeric characters — never a full card number.
    /// </summary>
    public string? LastFourDigits { get; private set; }

    public decimal CreditLimit { get; private set; }

    /// <summary>
    /// Informational only. Never used to derive the minimum payment or the pay-in-full amount — those
    /// are separate, user-entered fields on a later slice's <c>CreditCardStatement</c>.
    /// </summary>
    public decimal? AnnualInterestRate { get; private set; }

    /// <summary>
    /// Informational only — see <see cref="AnnualInterestRate"/>'s remarks.
    /// </summary>
    public decimal? MonthlyInterestRate { get; private set; }

    public int StatementCutOffDay { get; private set; }

    /// <summary>
    /// Independent of <see cref="StatementCutOffDay"/> — no ordering constraint between them, since a
    /// due date can fall in the month after the cutoff.
    /// </summary>
    public int PaymentDueDay { get; private set; }

    /// <summary>
    /// Computed, not stored. Can go negative when <see cref="CreditAccount.AmountOwed"/> exceeds
    /// <see cref="CreditLimit"/> — over-limit is allowed and expected in real life, so this is never
    /// validated against.
    /// </summary>
    public decimal AvailableCredit => CreditLimit - AmountOwed;

    private CreditCard()
    {
    }

    public CreditCard(
        string name,
        CurrencyCode currency,
        Guid institutionId,
        decimal creditLimit,
        int statementCutOffDay,
        int paymentDueDay,
        decimal openingAmountOwed = 0m,
        string? lastFourDigits = null,
        decimal? annualInterestRate = null,
        decimal? monthlyInterestRate = null,
        string? notes = null,
        Guid? networkId = null)
        : base(name, currency, openingAmountOwed, notes)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id is required.", nameof(institutionId));

        if (lastFourDigits is not null && (lastFourDigits.Length != 4 || !lastFourDigits.All(char.IsDigit)))
            throw new ArgumentException("Last four digits must be exactly 4 numeric characters.", nameof(lastFourDigits));

        if (creditLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(creditLimit), "Credit limit must be positive.");

        if (statementCutOffDay is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(statementCutOffDay), "Statement cut-off day must be between 1 and 31.");

        if (paymentDueDay is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(paymentDueDay), "Payment due day must be between 1 and 31.");

        if (annualInterestRate is < 0)
            throw new ArgumentOutOfRangeException(nameof(annualInterestRate), "Annual interest rate cannot be negative.");

        if (monthlyInterestRate is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyInterestRate), "Monthly interest rate cannot be negative.");

        InstitutionId = institutionId;
        NetworkId = networkId;
        LastFourDigits = lastFourDigits;
        CreditLimit = creditLimit;
        StatementCutOffDay = statementCutOffDay;
        PaymentDueDay = paymentDueDay;
        AnnualInterestRate = annualInterestRate;
        MonthlyInterestRate = monthlyInterestRate;
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
    /// The next date on/after <paramref name="onOrAfter"/> whose day-of-month is <paramref name="dayOfMonth"/>,
    /// clamping to the last day of a short month (e.g. day 31 in February becomes the 28th/29th) rather than
    /// rolling into the next month. Always recomputed from the target year/month — never derived by AddMonths-ing
    /// a previously clamped date — so a short month never permanently shifts the configured day going forward
    /// (unlike RecurringExpense.Advance's accepted AddMonths-drift limitation, which doesn't apply here because
    /// StatementCutOffDay/PaymentDueDay are stable stored ints to recompute from every time, not a running date).
    /// </summary>
    public DateOnly GetNextDateForDayOfMonth(int dayOfMonth, DateOnly onOrAfter)
    {
        var candidate = ClampedDate(onOrAfter.Year, onOrAfter.Month, dayOfMonth);
        return candidate >= onOrAfter ? candidate : ClampedDate(onOrAfter.AddMonths(1).Year, onOrAfter.AddMonths(1).Month, dayOfMonth);
    }

    public DateOnly GetCutOffDateOnOrAfter(DateOnly date) => GetNextDateForDayOfMonth(StatementCutOffDay, date);

    /// <summary>
    /// The payment due date for a cycle that closed on <paramref name="cutOffDate"/>. Uses the same
    /// "next date on/after" search starting the day after the cutoff, correctly landing in the same month
    /// when PaymentDueDay > StatementCutOffDay, or the following month otherwise — matches README §15's
    /// worked example (cutoff 25 → due date Oct 10) with no stored ordering assumption between the two
    /// day-of-month fields (there is none, per this class's existing remarks).
    /// </summary>
    public DateOnly GetPaymentDueDateForCycleEndingOn(DateOnly cutOffDate) =>
        GetNextDateForDayOfMonth(PaymentDueDay, cutOffDate.AddDays(1));

    private static DateOnly ClampedDate(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}
