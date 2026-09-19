using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.CreditAccounts;

public sealed class CreditAccountLifecycleService : ICreditAccountLifecycleService
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ICreditCardStatementRepository _creditCardStatementRepository;

    public CreditAccountLifecycleService(
        ICreditAccountRepository creditAccountRepository,
        ITransactionRepository transactionRepository,
        IRecurringExpenseRepository recurringExpenseRepository,
        ICreditCardStatementRepository creditCardStatementRepository)
    {
        _creditAccountRepository = creditAccountRepository;
        _transactionRepository = transactionRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
        _creditCardStatementRepository = creditCardStatementRepository;
    }

    public async Task<bool> CanHardDeleteAsync(Guid creditAccountId, CancellationToken ct = default)
    {
        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        if (await _transactionRepository.HasAnyTransactionReferencingCreditAccountAsync(creditAccountId, ct))
            return false;

        if (await _recurringExpenseRepository.HasAnyReferencingAccountAsync(creditAccountId, ct))
            return false;

        // Loans have no CreditCardStatement concept (§3.2) -- only check for CreditCard.
        if (creditAccount is CreditCard)
        {
            var statements = await _creditCardStatementRepository.GetForCardAsync(creditAccountId, ct);
            if (statements.Count > 0)
                return false;
        }

        return true;
    }

    public async Task DeleteAsync(Guid creditAccountId, CancellationToken ct = default)
    {
        // Never trust a caller's cached "yes" -- re-check immediately before deleting.
        if (!await CanHardDeleteAsync(creditAccountId, ct))
            throw new InvalidOperationException(
                $"Credit account '{creditAccountId}' cannot be hard-deleted; it is still referenced by a transaction, recurring expense, or statement.");

        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        _creditAccountRepository.Remove(creditAccount);
        await _creditAccountRepository.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid creditAccountId, CancellationToken ct = default)
    {
        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        creditAccount.Deactivate();
        await _creditAccountRepository.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(Guid creditAccountId, CancellationToken ct = default)
    {
        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        creditAccount.Reactivate();
        await _creditAccountRepository.SaveChangesAsync(ct);
    }

    public async Task<bool> CanChangeCurrencyAsync(Guid creditAccountId, CancellationToken ct = default) =>
        !await _transactionRepository.HasAnyTransactionReferencingCreditAccountAsync(creditAccountId, ct);
}
