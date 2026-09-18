using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>One category's spend for the "Expenses by category" report (README §40 slice 1).</summary>
public sealed record CategorySpendBarItem(string CategoryName, decimal Amount, CurrencyCode Currency, double BarWidthRequest);
