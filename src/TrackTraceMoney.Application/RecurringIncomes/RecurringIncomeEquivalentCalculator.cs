using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.Application.RecurringIncomes;

/// <summary>
/// Converts a recurring income's entered Amount+Frequency into biweekly/monthly equivalent
/// figures for display (recurring-income slice spec §3) — a pure projection off the recurring
/// definition itself, not derived from posted transaction history. Static, not an injected
/// interface+implementation like ISpendingCalculator/IIncomeCalculator: this takes no
/// repository/I/O dependency at all (just a decimal and an enum), mirroring the existing
/// static-pure-helper convention already used in this codebase for the same reason (e.g.
/// ReportBarWidth.Compute) rather than the DI-calculator convention reserved for calculators that
/// aggregate across repositories/currencies.
/// </summary>
public static class RecurringIncomeEquivalentCalculator
{
    // Standard periods-per-year used to convert between cadences.
    private const int WeeksPerYear = 52;
    private const int BiweeklyPeriodsPerYear = 26;
    private const int MonthsPerYear = 12;

    public static decimal ToMonthlyEquivalent(decimal amount, RecurringIncomeFrequency frequency) =>
        frequency switch
        {
            RecurringIncomeFrequency.Weekly => amount * WeeksPerYear / MonthsPerYear,
            RecurringIncomeFrequency.Biweekly => amount * BiweeklyPeriodsPerYear / MonthsPerYear,
            RecurringIncomeFrequency.Monthly => amount,
            RecurringIncomeFrequency.Yearly => amount / MonthsPerYear,
            _ => throw new InvalidOperationException($"Unknown frequency '{frequency}'.")
        };

    public static decimal ToBiweeklyEquivalent(decimal amount, RecurringIncomeFrequency frequency) =>
        frequency switch
        {
            RecurringIncomeFrequency.Weekly => amount * WeeksPerYear / BiweeklyPeriodsPerYear,
            RecurringIncomeFrequency.Biweekly => amount,
            RecurringIncomeFrequency.Monthly => amount * MonthsPerYear / BiweeklyPeriodsPerYear,
            RecurringIncomeFrequency.Yearly => amount / BiweeklyPeriodsPerYear,
            _ => throw new InvalidOperationException($"Unknown frequency '{frequency}'.")
        };
}
