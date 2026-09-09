using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Accounts;

/// <summary>
/// Base for every asset account the app tracks (README §8). Phase 1 ships the asset subtypes below
/// (<see cref="CashAccount"/>, <see cref="BankAccount"/>, <see cref="SavingsAccount"/>); term deposits/
/// investment funds (Phase 3) are deliberately not modeled yet — see CLAUDE.md's roadmap phase
/// boundaries. Credit cards/loans (Phase 2) are liabilities and live in a separate, sibling hierarchy
/// rooted at <see cref="TrackTraceMoney.Domain.CreditAccounts.CreditAccount"/> — not a subtype of this
/// class and not part of this TPH hierarchy — because <see cref="Credit"/>/<see cref="Debit"/> model
/// *available funds*, which means the opposite of what a liability's "amount owed" means.
/// </summary>
public abstract class FinancialAccount : Entity
{
    public string Name { get; private set; } = null!;

    public CurrencyCode Currency { get; private set; }

    public decimal Balance { get; private set; }

    public bool IsActive { get; private set; } = true;

    public string? Notes { get; private set; }

    protected FinancialAccount()
    {
    }

    protected FinancialAccount(string name, CurrencyCode currency, decimal openingBalance, string? notes)
    {
        Rename(name);
        Currency = currency;
        Balance = openingBalance;
        Notes = notes;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name cannot be empty.", nameof(name));

        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public void UpdateNotes(string? notes) => Notes = notes;

    public void Credit(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be non-negative.");

        Balance += amount;
    }

    public void Debit(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be non-negative.");

        Balance -= amount;
    }
}
