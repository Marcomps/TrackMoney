using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.CreditAccounts;

/// <summary>
/// Base for every liability product the app tracks (README §8, §47) — a sibling hierarchy to
/// <see cref="TrackTraceMoney.Domain.Accounts.FinancialAccount"/>, deliberately not a subtype of it.
/// <see cref="TrackTraceMoney.Domain.Accounts.FinancialAccount"/>'s <c>Credit</c>/<c>Debit</c> verbs
/// model *available funds* (crediting increases what you have); a <see cref="CreditAccount"/>'s
/// <see cref="AmountOwed"/> is *debt owed*, where a purchase increases what's owed and a payment
/// decreases it. Reusing <c>Credit</c>/<c>Debit</c> on a liability would make the same method name
/// mean opposite things depending on subtype, so this hierarchy gets its own, differently-named
/// mutators (<see cref="RegisterCharge"/>/<see cref="RegisterPayment"/>) instead. Phase 2 ships
/// <see cref="CreditCard"/> here; <c>Loan</c> joins this hierarchy in a later Phase 2 slice — see
/// CLAUDE.md's roadmap phase boundaries.
/// </summary>
public abstract class CreditAccount : Entity
{
    public string Name { get; private set; } = null!;

    public CurrencyCode Currency { get; private set; }

    /// <summary>
    /// The liability balance — how much is currently owed on this account. Mutated only via
    /// <see cref="RegisterCharge"/>/<see cref="RegisterPayment"/>, never assigned directly outside
    /// the constructor.
    /// </summary>
    public decimal AmountOwed { get; private set; }

    public bool IsActive { get; private set; } = true;

    public string? Notes { get; private set; }

    protected CreditAccount()
    {
    }

    protected CreditAccount(string name, CurrencyCode currency, decimal openingAmountOwed, string? notes)
    {
        Rename(name);

        if (openingAmountOwed < 0)
            throw new ArgumentOutOfRangeException(nameof(openingAmountOwed), "Opening amount owed cannot be negative.");

        Currency = currency;
        AmountOwed = openingAmountOwed;
        Notes = notes;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name.Trim();
    }

    public void UpdateNotes(string? notes) => Notes = notes;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Records a new charge against this account (e.g. a credit card purchase), increasing
    /// <see cref="AmountOwed"/>. Wiring an actual <c>CreditCardPurchase</c> transaction type to call
    /// this is a later Phase 2 slice — this method exists on the entity now but is unwired to any
    /// screen yet, mirroring how <c>FinancialAccount.Credit</c>/<c>Debit</c> existed before
    /// Income/Expense/Transfer wired them in Phase 1.
    /// </summary>
    public void RegisterCharge(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Charge amount must be positive.");

        AmountOwed += amount;
    }

    /// <summary>
    /// Whether <see cref="AmountOwed"/> may go below zero — a credit balance ("saldo a favor") the issuer
    /// applies to future charges. Credit cards allow it (paying more than the statement balance is common,
    /// and card contracts credit the excess to future charges); loans don't — overpaying an installment
    /// loan has no meaning here.
    /// </summary>
    protected virtual bool AllowsCreditBalance => false;

    /// <summary>The credit balance in the account's favour (the negative part of <see cref="AmountOwed"/>), or 0.</summary>
    public decimal CreditBalance => AmountOwed < 0m ? -AmountOwed : 0m;

    /// <summary>
    /// Undoes a charge recorded by <see cref="RegisterCharge"/> (a card purchase deleted, or reposted by an
    /// edit). When the charge has since been paid off this leaves a credit balance, if the account allows
    /// one (see <see cref="AllowsCreditBalance"/>); otherwise it's rejected.
    /// </summary>
    public void ReverseCharge(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Charge amount must be positive.");

        if (!AllowsCreditBalance && amount > AmountOwed)
            throw new InvalidOperationException("Reversing this charge would leave a negative amount owed.");

        AmountOwed -= amount;
    }

    /// <summary>
    /// Records a payment against this account, decreasing <see cref="AmountOwed"/>. Paying more than is
    /// owed leaves a credit balance on accounts that allow one (credit cards, see
    /// <see cref="AllowsCreditBalance"/>) and is rejected otherwise (loans).
    /// </summary>
    public void RegisterPayment(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");

        if (!AllowsCreditBalance && amount > AmountOwed)
            throw new InvalidOperationException("Payment amount cannot exceed the amount owed.");

        AmountOwed -= amount;
    }
}
