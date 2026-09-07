using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Transactions;

public sealed class TransactionEntryService : ITransactionEntryService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;

    public TransactionEntryService(ITransactionRepository transactionRepository, IFinancialAccountRepository accountRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
    }

    public async Task RecordExpenseAsync(
        DateOnly date,
        decimal amount,
        Guid accountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account '{accountId}' was not found.");

        var expense = new Expense(date, amount, accountId, categoryId, beneficiaryPersonId, payerPersonId, description, notes);

        account.Debit(amount);

        await _transactionRepository.AddAsync(expense, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordIncomeAsync(
        DateOnly date,
        decimal amount,
        Guid destinationAccountId,
        Guid categoryId,
        Guid? personId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var destinationAccount = await _accountRepository.GetByIdAsync(destinationAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{destinationAccountId}' was not found.");

        var income = new Income(date, amount, destinationAccountId, categoryId, personId, description, notes);

        destinationAccount.Credit(amount);

        await _transactionRepository.AddAsync(income, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordTransferAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var sourceAccount = await _accountRepository.GetByIdAsync(sourceAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{sourceAccountId}' was not found.");

        var destinationAccount = await _accountRepository.GetByIdAsync(destinationAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{destinationAccountId}' was not found.");

        // README §6/§10: currency is explicit per account and never assumed global. Without an
        // exchange-rate/conversion feature (not in scope for MVP), applying the same numeric amount
        // to accounts in different currencies would silently corrupt both balances.
        if (sourceAccount.Currency != destinationAccount.Currency)
            throw new InvalidOperationException(
                $"Cannot transfer between accounts with different currencies ({sourceAccount.Currency} -> {destinationAccount.Currency}) without currency conversion support.");

        var transfer = new Transfer(date, amount, sourceAccountId, destinationAccountId, description, notes);

        sourceAccount.Debit(amount);
        destinationAccount.Credit(amount);

        await _transactionRepository.AddAsync(transfer, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }
}
