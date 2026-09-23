namespace TrackTraceMoney.Domain.RecurringIncomes;

/// <summary>
/// Fixed set of cadences a <see cref="RecurringIncome"/> can repeat on. Mirrors
/// RecurringExpenseFrequency's "fixed set, not arbitrary N-day interval" shape, but is its own
/// enum — see the recurring-income slice spec's "why not reuse RecurringExpenseFrequency"
/// decision (Decision D): nothing on the expense side needs a biweekly cadence, and widening a
/// shared, already-shipped enum purely to serve a second, unrelated entity's need is unnecessary
/// coupling for zero reuse benefit.
/// </summary>
public enum RecurringIncomeFrequency
{
    Weekly,
    Biweekly,
    Monthly,
    Yearly,

    /// <summary>
    /// Twice a month on the 15th and the last day of the month (24 paydays a year) — the usual
    /// "quincenal" payroll, unlike <see cref="Biweekly"/> which is every 14 days (26 a year).
    /// Appended last: stored as its name, but keeps existing ordinal positions stable too.
    /// </summary>
    SemiMonthly
}
