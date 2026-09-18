using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// One currency's worth of <see cref="CategorySpendBarItem"/> rows for the "Expenses by category"
/// report — currencies are never blended (CLAUDE.md), so each group is its own independent bar chart.
/// </summary>
public sealed class CategorySpendReportGroup : List<CategorySpendBarItem>
{
    public CurrencyCode Currency { get; }

    public decimal Total { get; }

    public CategorySpendReportGroup(CurrencyCode currency, decimal total, IEnumerable<CategorySpendBarItem> items) : base(items)
    {
        Currency = currency;
        Total = total;
    }
}
