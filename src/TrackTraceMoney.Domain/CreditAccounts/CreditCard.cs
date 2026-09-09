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
}
