namespace TrackTraceMoney.Domain.RecurringExpenses;

/// <summary>
/// Fixed set of cadences a <see cref="RecurringExpense"/> can repeat on. Deliberately not an
/// arbitrary "every N days" interval — see CLAUDE.md/README scope for Phase 1 recurring expenses.
/// </summary>
public enum RecurringExpenseFrequency
{
    Weekly,
    Monthly,
    Yearly
}
