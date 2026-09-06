using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Budgets;

namespace TrackTraceMoney.Application.Budgets;

public interface IBudgetEvaluator
{
    IReadOnlyList<BudgetStatus> Evaluate(IEnumerable<Budget> budgets, SpendingSummary spending);
}
