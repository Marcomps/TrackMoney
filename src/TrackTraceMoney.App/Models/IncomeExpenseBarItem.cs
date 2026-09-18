namespace TrackTraceMoney.App.Models;

/// <summary>One bar (income or expenses) within a currency group on the "Income vs. expenses" report.</summary>
public sealed record IncomeExpenseBarItem(string Label, decimal Amount, double BarWidthRequest);
