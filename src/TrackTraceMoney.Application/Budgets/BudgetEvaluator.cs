using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Budgets;

namespace TrackTraceMoney.Application.Budgets;

public sealed class BudgetEvaluator : IBudgetEvaluator
{
    public IReadOnlyList<BudgetStatus> Evaluate(IEnumerable<Budget> budgets, SpendingSummary spending) =>
        budgets
            // Only ever compare a budget against spend already in that budget's own currency — the
            // actual bug fix this type exists for (CLAUDE.md: currency is explicit, never blended).
            .Select(b => new BudgetStatus(b.CategoryId, b.Amount, spending.GetSpentForCategory(b.CategoryId, b.Currency), b.Currency))
            .ToList();
}
