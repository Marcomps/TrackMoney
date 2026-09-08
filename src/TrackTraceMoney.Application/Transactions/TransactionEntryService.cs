using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Transactions;

public sealed class TransactionEntryService : ITransactionEntryService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly ILocalNotifier _localNotifier;

    public TransactionEntryService(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        ISpendingCalculator spendingCalculator,
        ILocalNotifier localNotifier)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _spendingCalculator = spendingCalculator;
        _localNotifier = localNotifier;
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

        // README §34/§37: figure out, before saving, whether this expense is the one that pushes the
        // category's monthly budget over its limit — only that crossing transition should notify, not
        // every subsequent expense once already over. Computed arithmetically from spend-before-this
        // expense rather than re-querying after save, so there's no risk of double-counting this
        // expense against itself.
        var budget = await _budgetRepository.GetForCategoryAndMonthAsync(categoryId, date.Year, date.Month, account.Currency, ct);
        decimal spentBefore = 0m;
        if (budget is not null)
        {
            var startOfMonth = new DateOnly(date.Year, date.Month, 1);
            var categoryTransactions = await _transactionRepository.GetByDateRangeAndCategoryAsync(startOfMonth, date, categoryId, ct);
            var accounts = await _accountRepository.GetAllAsync(ct);
            var accountCurrencies = accounts.ToDictionary(a => a.Id, a => a.Currency);
            var summary = _spendingCalculator.Calculate(categoryTransactions, accountCurrencies);
            spentBefore = summary.GetSpentForCategory(categoryId, budget.Currency);
        }

        var expense = new Expense(date, amount, accountId, categoryId, beneficiaryPersonId, payerPersonId, description, notes);

        account.Debit(amount);

        await _transactionRepository.AddAsync(expense, ct);
        await _transactionRepository.SaveChangesAsync(ct);

        if (budget is not null)
        {
            var wasOverBudget = spentBefore > budget.Amount;
            var newSpent = spentBefore + amount;
            var isOverBudget = newSpent > budget.Amount;

            if (!wasOverBudget && isOverBudget)
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
                if (category is not null)
                    await _localNotifier.NotifyBudgetExceededAsync(category, budget.Amount, newSpent - budget.Amount, ct);
            }
        }
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
