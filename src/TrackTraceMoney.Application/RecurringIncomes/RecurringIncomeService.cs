using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;

namespace TrackTraceMoney.Application.RecurringIncomes;

public sealed class RecurringIncomeService : IRecurringIncomeService
{
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;
    private readonly ITransactionEntryService _transactionEntryService;
    private readonly IUnitOfWork _unitOfWork;

    public RecurringIncomeService(
        IRecurringIncomeRepository recurringIncomeRepository,
        ITransactionEntryService transactionEntryService,
        IUnitOfWork unitOfWork)
    {
        _recurringIncomeRepository = recurringIncomeRepository;
        _transactionEntryService = transactionEntryService;
        _unitOfWork = unitOfWork;
    }

    public async Task ConfirmOccurrenceAsync(Guid recurringIncomeId, CancellationToken ct = default)
    {
        var recurringIncome = await _recurringIncomeRepository.GetByIdAsync(recurringIncomeId, ct)
            ?? throw new InvalidOperationException($"Recurring income '{recurringIncomeId}' was not found.");

        // Capture before RecordIncomeAsync/MarkConfirmed run: NextOccurrenceDate is computed from
        // LastConfirmedDate, so it would change out from under a second read after MarkConfirmed.
        var occurrenceDate = recurringIncome.NextOccurrenceDate;

        // Posting the Income (which credits the account, inside RecordIncomeAsync's own
        // SaveChangesAsync) and marking this recurring income confirmed (a second, separate
        // SaveChangesAsync) must commit or roll back together. Without this transaction, a crash or
        // a failed second save between the two would leave the Income already committed while the
        // recurring income still reports as due — a retry would then post the same real-world
        // deposit a second time, double-counting income and crediting the account twice.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        // Must be a plain synchronous call, right here — see IUnitOfWorkTransaction.EnterAmbientScope's
        // doc comment for why delegating this into another awaited method would silently fail to cover
        // the repository calls below.
        using var ambientScope = transaction.EnterAmbientScope();
        try
        {
            await _transactionEntryService.RecordIncomeAsync(
                date: occurrenceDate,
                amount: recurringIncome.Amount, // always the CURRENT amount -- see RecurringIncome.UpdateAmount
                destinationAccountId: recurringIncome.DestinationAccountId,
                categoryId: recurringIncome.CategoryId,
                personId: null, // RecurringIncome never captures Person fields -- mirrors RecurringExpense's own precedent
                description: recurringIncome.Name,
                notes: null,
                ct: ct);

            recurringIncome.MarkConfirmed(occurrenceDate);

            await _recurringIncomeRepository.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
