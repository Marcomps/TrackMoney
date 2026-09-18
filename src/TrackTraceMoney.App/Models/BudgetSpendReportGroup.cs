using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>One currency's worth of budgets for the "Budget vs. spending" report (CLAUDE.md: never blend currencies).</summary>
public sealed class BudgetSpendReportGroup : List<BudgetSpendReportItem>
{
    public CurrencyCode Currency { get; }

    public BudgetSpendReportGroup(CurrencyCode currency, IEnumerable<BudgetSpendReportItem> items) : base(items) => Currency = currency;
}
