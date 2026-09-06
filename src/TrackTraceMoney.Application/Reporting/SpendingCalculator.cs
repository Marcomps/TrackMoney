using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Aggregates spend the way README §9.1 requires: transfers, income, and any other movement that
/// isn't flagged via <see cref="Transaction.CountsAsExpense"/> are excluded, so a card payment or
/// account transfer can never inflate "gastos del período" — see CLAUDE.md's non-obvious domain
/// rules for why this matters.
/// </summary>
public sealed class SpendingCalculator : ISpendingCalculator
{
    public SpendingSummary Calculate(IEnumerable<Transaction> transactions)
    {
        decimal total = 0m;
        var byCategory = new Dictionary<Guid, decimal>();

        foreach (var transaction in transactions)
        {
            if (!transaction.CountsAsExpense)
                continue;

            total += transaction.Amount;

            if (transaction.SpendCategoryId is { } categoryId)
                byCategory[categoryId] = byCategory.GetValueOrDefault(categoryId) + transaction.Amount;
        }

        return new SpendingSummary { TotalSpent = total, SpentByCategory = byCategory };
    }
}
