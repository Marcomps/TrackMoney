namespace TrackTraceMoney.Application.RecurringExpenses;

/// <summary>
/// Confirms a due recurring expense occurrence by posting a real <c>Expense</c> transaction (via
/// <c>ITransactionEntryService</c>) and advancing the recurring expense's confirmed-through date.
/// </summary>
public interface IRecurringExpenseService
{
    /// <summary>
    /// Confirms the next occurrence as paid. It may be confirmed up to
    /// <c>RecurringExpense.EarlyConfirmationWindowDays</c> before its scheduled date, in which case the
    /// transaction is dated <paramref name="today"/>; throws if it is further out than that.
    /// </summary>
    Task ConfirmOccurrenceAsync(Guid recurringExpenseId, DateOnly today, CancellationToken ct = default);
}
