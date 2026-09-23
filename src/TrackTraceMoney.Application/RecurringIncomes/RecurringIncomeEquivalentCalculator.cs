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
    private const int SemiMonthlyPeriodsPerMonth = 2;
    private const int MonthsPerYear = 12;

    public static decimal ToMonthlyEquivalent(decimal amount, RecurringIncomeFrequency frequency) =>
        frequency switch
        {
            RecurringIncomeFrequency.Weekly => amount * WeeksPerYear / MonthsPerYear,
            RecurringIncomeFrequency.Biweekly => amount * BiweeklyPeriodsPerYear / MonthsPerYear,
            RecurringIncomeFrequency.SemiMonthly => amount * SemiMonthlyPeriodsPerMonth,
            RecurringIncomeFrequency.Monthly => amount,
            RecurringIncomeFrequency.Yearly => amount / MonthsPerYear,
            _ => throw new InvalidOperationException($"Unknown frequency '{frequency}'.")
        };

    /// <summary>
    /// The per-"quincena" figure: half of the monthly equivalent (24 paydays a year), which is what a
    /// semi-monthly payroll slip shows. Not the every-14-days figure — see
    /// <see cref="RecurringIncomeFrequency.SemiMonthly"/> vs <see cref="RecurringIncomeFrequency.Biweekly"/>.
    /// </summary>
    public static decimal ToSemiMonthlyEquivalent(decimal amount, RecurringIncomeFrequency frequency) =>
        frequency == RecurringIncomeFrequency.SemiMonthly
            ? amount
            : ToMonthlyEquivalent(amount, frequency) / SemiMonthlyPeriodsPerMonth;
}
