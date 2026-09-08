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

    /// <summary>Whether this transaction counts as income for reports/dashboards.</summary>
    public virtual bool CountsAsIncome => false;

    /// <summary>The category this transaction's spend should be attributed to, if any.</summary>
    public virtual Guid? SpendCategoryId => null;

    /// <summary>
    /// The account whose currency governs this transaction's spend amount, if any (only meaningful
    /// when <see cref="CountsAsExpense"/> is true). Exposed the same way as <see cref="SpendCategoryId"/>
    /// so aggregation code never needs to pattern-match on concrete transaction types to find "the"
    /// account for currency-aware grouping.
    /// </summary>
    public virtual Guid? SpendAccountId => null;

    /// <summary>
    /// The account whose currency governs this transaction's income amount, if any (only meaningful
    /// when <see cref="CountsAsIncome"/> is true). Mirrors <see cref="SpendAccountId"/> for the income side.
    /// </summary>
    public virtual Guid? IncomeAccountId => null;

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
