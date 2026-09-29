namespace TrackTraceMoney.Domain.RecurringExpenses;

/// <summary>
/// Fixed set of cadences a <see cref="RecurringExpense"/> can repeat on. Deliberately not an
/// arbitrary "every N days" interval — see CLAUDE.md/README scope for Phase 1 recurring expenses.
/// </summary>
public enum RecurringExpenseFrequency
{
    Weekly,
    Monthly,
    Yearly,

    /// <summary>
    /// Twice a month on the 15th and the last day of the month — e.g. sharing each "quincena" of a
    /// semi-monthly salary. Appended last: stored as its name, but keeps existing ordinal positions too.
    /// </summary>
    SemiMonthly
}
