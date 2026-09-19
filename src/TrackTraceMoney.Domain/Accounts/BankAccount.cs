using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Accounts;

public sealed class BankAccount : FinancialAccount
{
    /// <summary>
    /// Legacy free-text bank name, superseded by <see cref="InstitutionId"/> (see the
    /// financial-institution-card-network-slice-spec's Decision 2, now extended to this entity too — it
    /// was the 5th free-text institution field, joining CreditCard.Issuer/Loan.Institution/
    /// TermDeposit.Institution/InvestmentFund.Institution, but was missed in the original slice). Kept —
    /// no longer constructor-validated — only so <c>FinancialInstitutionBackfillService</c> can read a
    /// not-yet-backfilled row's value; no code path after this slice writes it (the Add Account screen
    /// only ever sets <see cref="InstitutionId"/> now). Never dropped this slice (zero production users;
    /// see the spec).
    /// </summary>
    public string? BankName { get; private set; }

    /// <summary>
    /// The account's <c>FinancialInstitution</c>. Unlike CreditCard/Loan/TermDeposit/InvestmentFund's
    /// InstitutionId (which preserve a previously-*required* free-text field, so their public
    /// constructors still require a real id), <see cref="BankName"/> was always optional
    /// (<c>string? bankName = null</c>) — this property preserves that same already-optional-ness rather
    /// than newly tightening a field that was never required. <see langword="null"/> means either "no
    /// institution chosen" (a genuinely valid end state for a bank account) or "legacy row not yet
    /// backfilled" — both are indistinguishable and both are fine to leave null indefinitely, unlike the
    /// other four entities where null is only ever a transient pre-backfill state.
    /// </summary>
    public Guid? InstitutionId { get; private set; }

    public string? AccountNumberLast4 { get; private set; }

    private BankAccount()
    {
    }

    public BankAccount(
        string name,
        CurrencyCode currency,
        decimal openingBalance = 0m,
        Guid? institutionId = null,
        string? accountNumberLast4 = null,
        string? notes = null)
        : base(name, currency, openingBalance, notes)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id must not be empty when provided.", nameof(institutionId));

        InstitutionId = institutionId;
        AccountNumberLast4 = accountNumberLast4;
    }

    /// <summary>
    /// Sets <see cref="InstitutionId"/> on a legacy row (constructed via EF's private materialization
    /// path) — called only by <c>FinancialInstitutionBackfillService</c>. Mirrors
    /// CreditCard/Loan/TermDeposit/InvestmentFund's identically-named method, even though here it's
    /// backfilling an optional field rather than a required one.
    /// </summary>
    public void SetInstitutionId(Guid institutionId)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id is required.", nameof(institutionId));

        InstitutionId = institutionId;
    }

    /// <summary>
    /// Edits this account's institution (edit/delete slice spec §1.1). Deliberately allows
    /// <see langword="null"/> — unlike <see cref="SetInstitutionId"/> (backfill-only, throws on
    /// <see cref="Guid.Empty"/>), a <see cref="BankAccount"/>'s institution is genuinely optional per
    /// this class's own constructor, so a user must be able to clear it back to "no institution
    /// chosen", not just switch between two non-null values.
    /// </summary>
    public void UpdateInstitution(Guid? institutionId)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution id must not be empty when provided.", nameof(institutionId));

        InstitutionId = institutionId;
    }

    /// <summary>Edits this account's masked account number display (edit/delete slice spec §1.1).</summary>
    public void UpdateAccountNumber(string? last4) => AccountNumberLast4 = last4;
}
