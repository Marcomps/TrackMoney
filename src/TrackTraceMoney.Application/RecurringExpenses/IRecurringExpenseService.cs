namespace TrackTraceMoney.Application.RecurringExpenses;

/// <summary>
/// Confirms a due recurring expense occurrence by posting a real <c>Expense</c> transaction (via
/// <c>ITransactionEntryService</c>) and advancing the recurring expense's confirmed-through date.
/// </summary>
public interface IRecurringExpenseService
{
    Task ConfirmOccurrenceAsync(Guid recurringExpenseId, CancellationToken ct = default);
}
