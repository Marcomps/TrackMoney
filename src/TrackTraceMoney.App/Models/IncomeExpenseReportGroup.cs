using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// One currency's income/expenses/available for the "Income vs. expenses" report — same shape as
/// Dashboard Tile 2 (<see cref="CurrencyIncomeExpense"/>), plus the two bar rows this report visualizes.
/// Currencies are never blended (CLAUDE.md).
/// </summary>
public sealed class IncomeExpenseReportGroup : List<IncomeExpenseBarItem>
{
    public CurrencyCode Currency { get; }

    public decimal Income { get; }

    public decimal Expenses { get; }

    public decimal Available { get; }

    public IncomeExpenseReportGroup(
        CurrencyCode currency, decimal income, decimal expenses, decimal available, IEnumerable<IncomeExpenseBarItem> items)
        : base(items)
    {
        Currency = currency;
        Income = income;
        Expenses = expenses;
        Available = available;
    }
}
