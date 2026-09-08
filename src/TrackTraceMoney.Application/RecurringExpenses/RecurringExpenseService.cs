using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;

namespace TrackTraceMoney.Application.RecurringExpenses;

public sealed class RecurringExpenseService : IRecurringExpenseService
{
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ITransactionEntryService _transactionEntryService;

    public RecurringExpenseService(
        IRecurringExpenseRepository recurringExpenseRepository,
        ITransactionEntryService transactionEntryService)
    {
        _recurringExpenseRepository = recurringExpenseRepository;
        _transactionEntryService = transactionEntryService;
    }

    public async Task ConfirmOccurrenceAsync(Guid recurringExpenseId, CancellationToken ct = default)
    {
        var recurringExpense = await _recurringExpenseRepository.GetByIdAsync(recurringExpenseId, ct)
            ?? throw new InvalidOperationException($"Recurring expense '{recurringExpenseId}' was not found.");

        // Capture before RecordExpenseAsync/MarkConfirmed run: NextOccurrenceDate is computed from
        // LastConfirmedDate, so it would change out from under a second read after MarkConfirmed.
        var occurrenceDate = recurringExpense.NextOccurrenceDate;

        await _transactionEntryService.RecordExpenseAsync(
            date: occurrenceDate,
            amount: recurringExpense.Amount,
            accountId: recurringExpense.AccountId,
            categoryId: recurringExpense.CategoryId,
            payerPersonId: null,
            beneficiaryPersonId: null,
            description: recurringExpense.Name,
            notes: null,
            ct: ct);

        recurringExpense.MarkConfirmed(occurrenceDate);

        await _recurringExpenseRepository.SaveChangesAsync(ct);
    }
}
