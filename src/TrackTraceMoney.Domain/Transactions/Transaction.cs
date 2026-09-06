using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// Base for every money movement (README §9). <b>Not every transaction is spend</b> — see §9.1:
/// a transfer or a debt payment moves money but must never be counted as "gasto" in reports,
/// budgets, or dashboards. <see cref="CountsAsExpense"/> and <see cref="SpendCategoryId"/> are the
/// single source of truth callers must use instead of pattern-matching on concrete types, so that
/// later phases (e.g. CreditCardPurchase) can opt in without touching existing aggregation code.
/// </summary>
public abstract class Transaction : Entity
{
    public DateOnly Date { get; private set; }

    public decimal Amount { get; private set; }

    public string? Description { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Whether this transaction counts as spend for reports/budgets/dashboards.</summary>
    public virtual bool CountsAsExpense => false;

    /// <summary>The category this transaction's spend should be attributed to, if any.</summary>
    public virtual Guid? SpendCategoryId => null;

    protected Transaction()
    {
    }

    protected Transaction(DateOnly date, decimal amount, string? description, string? notes)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transaction amount must be positive.");

        Date = date;
        Amount = amount;
        Description = description;
        Notes = notes;
    }
}
