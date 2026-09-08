using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;

namespace TrackTraceMoney.Application.RecurringExpenses;

public sealed class RecurringExpenseService : IRecurringExpenseService
{
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ITransactionEntryService _transactionEntryService;
    private readonly IUnitOfWork _unitOfWork;

    public RecurringExpenseService(
        IRecurringExpenseRepository recurringExpenseRepository,
        ITransactionEntryService transactionEntryService,
        IUnitOfWork unitOfWork)
    {
        _recurringExpenseRepository = recurringExpenseRepository;
        _transactionEntryService = transactionEntryService;
        _unitOfWork = unitOfWork;
    }

    public async Task ConfirmOccurrenceAsync(Guid recurringExpenseId, CancellationToken ct = default)
    {
        var recurringExpense = await _recurringExpenseRepository.GetByIdAsync(recurringExpenseId, ct)
            ?? throw new InvalidOperationException($"Recurring expense '{recurringExpenseId}' was not found.");

        // Capture before RecordExpenseAsync/MarkConfirmed run: NextOccurrenceDate is computed from
        // LastConfirmedDate, so it would change out from under a second read after MarkConfirmed.
        var occurrenceDate = recurringExpense.NextOccurrenceDate;

        // Posting the Expense (which debits the account, inside RecordExpenseAsync's own
        // SaveChangesAsync) and marking this recurring expense confirmed (a second, separate
        // SaveChangesAsync) must commit or roll back together. Without this transaction, a crash or a
        // failed second save between the two would leave the Expense already committed while the
        // recurring expense still reports as due — a retry would then post the same real-world
        // payment a second time, double-counting spend and debiting the account twice.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        // Must be a plain synchronous call, right here — see IUnitOfWorkTransaction.EnterAmbientScope's
        // doc comment for why delegating this into another awaited method would silently fail to cover
        // the repository calls below (and why that isn't just a style preference).
        using var ambientScope = transaction.EnterAmbientScope();
        try
        {
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

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
