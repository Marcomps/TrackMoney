namespace TrackTraceMoney.Application.RecurringIncomes;

/// <summary>
/// Confirms a due recurring income occurrence by posting a real <c>Income</c> transaction (via
/// <c>ITransactionEntryService</c>) and advancing the recurring income's confirmed-through date.
/// </summary>
public interface IRecurringIncomeService
{
    /// <summary>
    /// Confirms the next occurrence as received. It may be confirmed up to
    /// <c>RecurringIncome.EarlyConfirmationWindowDays</c> before its scheduled date, in which case the
    /// Income is dated <paramref name="today"/>; throws if it is further out than that.
    /// </summary>
    Task ConfirmOccurrenceAsync(Guid recurringIncomeId, DateOnly today, CancellationToken ct = default);
}
