using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Domain.Budgets;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Wraps <see cref="BudgetListItem"/> (Dashboard's existing model — reused as-is so the 🟢/🟡/🔴
/// semáforo threshold logic isn't duplicated) with the extra bar-width this report renders that
/// Dashboard's tile doesn't need. Unlike Dashboard's tile (which only ever shows over-budget
/// categories), the Budget vs. spending report shows EVERY <see cref="BudgetStatus"/> — the bar's
/// ratio is the budget's own percent-used, capped at 100% width for a budget that's over.
/// </summary>
public sealed record BudgetSpendReportItem(BudgetListItem ListItem, double BarWidthRequest)
{
    public static BudgetSpendReportItem FromDomain(Budget budget, BudgetStatus status, string categoryName)
    {
        var listItem = BudgetListItem.FromDomain(budget, status, categoryName);
        var ratio = (double)status.PercentUsed;
        return new BudgetSpendReportItem(listItem, ReportBarWidth.ComputeFromRatio(ratio));
    }
}
