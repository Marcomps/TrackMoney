using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Budgets;

/// <summary>
/// One category's spending limit for one calendar month, in one explicit currency (README §34).
/// A budget is "$300 for Food in USD" — never a bare number — mirroring
/// <see cref="Accounts.FinancialAccount.Currency"/>'s stance that currency is always explicit and
/// never assumed global (see CLAUDE.md). Two accounts in different currencies can both spend against
/// the same category, so a category can legitimately have one budget per currency per month.
/// </summary>
public sealed class Budget : Entity
{
    public Guid CategoryId { get; private set; }

    public decimal Amount { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public CurrencyCode Currency { get; private set; }

    private Budget()
    {
    }

    public Budget(Guid categoryId, decimal amount, int year, int month, CurrencyCode currency)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");

        CategoryId = categoryId;
        Year = year;
        Month = month;
        Currency = currency;
        UpdateAmount(amount);
    }

    public void UpdateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Budget amount must be positive.");

        Amount = amount;
    }
}
