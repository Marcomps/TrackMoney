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
    /// Records a payment against this account, decreasing <see cref="AmountOwed"/>. Overpayment is
    /// deliberately rejected in this slice — there is no defined meaning yet for a negative
    /// <see cref="AmountOwed"/> ("credit balance"), so this throws rather than clamping to zero or
    /// going negative.
    /// </summary>
    public void RegisterPayment(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");

        if (amount > AmountOwed)
            throw new InvalidOperationException("Payment amount cannot exceed the amount owed.");

        AmountOwed -= amount;
    }
}
