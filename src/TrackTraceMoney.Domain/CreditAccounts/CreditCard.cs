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
    public string Issuer { get; private set; } = null!;

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
        string issuer,
        decimal creditLimit,
        int statementCutOffDay,
        int paymentDueDay,
        decimal openingAmountOwed = 0m,
        string? lastFourDigits = null,
        decimal? annualInterestRate = null,
        decimal? monthlyInterestRate = null,
        string? notes = null)
        : base(name, currency, openingAmountOwed, notes)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            throw new ArgumentException("Issuer cannot be empty.", nameof(issuer));

        if (issuer.Trim().Length > 200)
            throw new ArgumentException("Issuer cannot exceed 200 characters.", nameof(issuer));

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

        Issuer = issuer.Trim();
        LastFourDigits = lastFourDigits;
        CreditLimit = creditLimit;
        StatementCutOffDay = statementCutOffDay;
        PaymentDueDay = paymentDueDay;
        AnnualInterestRate = annualInterestRate;
        MonthlyInterestRate = monthlyInterestRate;
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
