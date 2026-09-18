namespace TrackTraceMoney.App.Models;

/// <summary>One calendar month's spend bar for the "Monthly expenses trend" report, one currency at a time.</summary>
public sealed record MonthlyTrendBarItem(string MonthLabel, decimal Amount, double BarWidthRequest);
