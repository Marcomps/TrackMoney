namespace TrackTraceMoney.Application.Budgets;

/// <summary>
/// A category's budget compared to what was actually spent (README §33-34). Deliberately exposes
/// only numbers — the 🟢/🟡/🔴 semáforo coloring is a presentation concern for the App layer.
/// </summary>
public sealed record BudgetStatus(Guid CategoryId, decimal BudgetAmount, decimal Spent)
{
    public decimal Remaining => BudgetAmount - Spent;

    public decimal PercentUsed => BudgetAmount == 0 ? 0 : Spent / BudgetAmount;

    public bool IsOverBudget => Spent > BudgetAmount;
}
