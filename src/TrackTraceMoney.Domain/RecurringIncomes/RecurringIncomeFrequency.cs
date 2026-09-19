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
    Yearly
}
