using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.Application.Accounts;

public sealed class FinancialAccountLifecycleService : IFinancialAccountLifecycleService
{
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;

    public FinancialAccountLifecycleService(
        IFinancialAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IRecurringExpenseRepository recurringExpenseRepository,
        IRecurringIncomeRepository recurringIncomeRepository)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
        _recurringIncomeRepository = recurringIncomeRepository;
    }

    public async Task<bool> CanHardDeleteAsync(Guid accountId, CancellationToken ct = default)
    {
        if (await _transactionRepository.HasAnyTransactionReferencingFinancialAccountAsync(accountId, ct))
            return false;

        if (await _recurringExpenseRepository.HasAnyReferencingAccountAsync(accountId, ct))
            return false;

        if (await _recurringIncomeRepository.HasAnyReferencingAccountAsync(accountId, ct))
            return false;

        return true;
    }

    public async Task DeleteAsync(Guid accountId, CancellationToken ct = default)
    {
        // Never trust a caller's cached "yes" -- re-check immediately before deleting.
        if (!await CanHardDeleteAsync(accountId, ct))
            throw new InvalidOperationException(
                $"Account '{accountId}' cannot be hard-deleted; it is still referenced by a transaction or recurring definition.");

        var account = await _accountRepository.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account '{accountId}' was not found.");

        _accountRepository.Remove(account);
        await _accountRepository.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account '{accountId}' was not found.");

        account.Deactivate();
        await _accountRepository.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account '{accountId}' was not found.");

        account.Reactivate();
        await _accountRepository.SaveChangesAsync(ct);
    }

    public async Task<bool> CanChangeCurrencyAsync(Guid accountId, CancellationToken ct = default) =>
        !await _transactionRepository.HasAnyTransactionReferencingFinancialAccountAsync(accountId, ct);
}
