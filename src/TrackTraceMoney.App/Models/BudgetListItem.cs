using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record BudgetListItem(
    Guid Id,
    string CategoryName,
    decimal BudgetAmount,
    decimal Spent,
    decimal Remaining,
    decimal PercentUsed,
    string SemaforoEmoji,
    CurrencyCode Currency)
{
    /// <summary>Near-limit threshold for the 🟡 semáforo state (README §33-34): 90% of the budget used.</summary>
    private const decimal NearLimitThreshold = 0.9m;

    public static BudgetListItem FromDomain(Budget budget, BudgetStatus status, string categoryName)
    {
        var emoji = status.IsOverBudget
            ? "\U0001F534" // 🔴
            : status.PercentUsed >= NearLimitThreshold
                ? "\U0001F7E1" // 🟡
                : "\U0001F7E2"; // 🟢

        return new BudgetListItem(
            budget.Id,
            categoryName,
            status.BudgetAmount,
            status.Spent,
            status.Remaining,
            status.PercentUsed,
            emoji,
            status.Currency);
    }
}
