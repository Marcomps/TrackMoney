using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Budgets;

/// <summary>
/// A category's budget compared to what was actually spent (README §33-34). Deliberately exposes
/// only numbers — the 🟢/🟡/🔴 semáforo coloring is a presentation concern for the App layer.
/// <see cref="Currency"/> is the budget's own currency — <see cref="Spent"/> is always the spend
/// already restricted to that same currency (see <c>BudgetEvaluator</c>), never a cross-currency sum.
/// </summary>
public sealed record BudgetStatus(Guid CategoryId, decimal BudgetAmount, decimal Spent, CurrencyCode Currency)
{
    public decimal Remaining => BudgetAmount - Spent;

    public decimal PercentUsed => BudgetAmount == 0 ? 0 : Spent / BudgetAmount;

    public bool IsOverBudget => Spent > BudgetAmount;
}
