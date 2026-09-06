using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Budgets;

namespace TrackTraceMoney.Application.Budgets;

public sealed class BudgetEvaluator : IBudgetEvaluator
{
    public IReadOnlyList<BudgetStatus> Evaluate(IEnumerable<Budget> budgets, SpendingSummary spending) =>
        budgets
            .Select(b => new BudgetStatus(b.CategoryId, b.Amount, spending.SpentByCategory.GetValueOrDefault(b.CategoryId)))
            .ToList();
}
