namespace TrackTraceMoney.Application.RecurringIncomes;

/// <summary>
/// Confirms a due recurring income occurrence by posting a real <c>Income</c> transaction (via
/// <c>ITransactionEntryService</c>) and advancing the recurring income's confirmed-through date.
/// </summary>
public interface IRecurringIncomeService
{
    Task ConfirmOccurrenceAsync(Guid recurringIncomeId, CancellationToken ct = default);
}
