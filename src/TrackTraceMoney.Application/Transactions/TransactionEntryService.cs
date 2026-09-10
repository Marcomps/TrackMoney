using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Transactions;

public sealed class TransactionEntryService : ITransactionEntryService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly ILocalNotifier _localNotifier;

    public TransactionEntryService(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        ISpendingCalculator spendingCalculator,
        ILocalNotifier localNotifier)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
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
            var creditAccounts = await _creditAccountRepository.GetAllAsync(ct);
            var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);
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

    public async Task RecordCreditCardPurchaseAsync(
        DateOnly date,
        decimal amount,
        Guid creditAccountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        if (creditAccount is not CreditCard)
            throw new InvalidOperationException($"Credit account '{creditAccountId}' is not a credit card.");

        // Same budget-crossing check as RecordExpenseAsync — see its comment above. Kept duplicated
        // here rather than factored into a second service, since the crossing logic must stay in sync
        // for every spend-counting transaction kind (README §34/§37; CLAUDE.md's #1 risk).
        var budget = await _budgetRepository.GetForCategoryAndMonthAsync(categoryId, date.Year, date.Month, creditAccount.Currency, ct);
        decimal spentBefore = 0m;
        if (budget is not null)
        {
            var startOfMonth = new DateOnly(date.Year, date.Month, 1);
            var categoryTransactions = await _transactionRepository.GetByDateRangeAndCategoryAsync(startOfMonth, date, categoryId, ct);
            var accounts = await _accountRepository.GetAllAsync(ct);
            var creditAccounts = await _creditAccountRepository.GetAllAsync(ct);
            var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);
            var summary = _spendingCalculator.Calculate(categoryTransactions, accountCurrencies);
            spentBefore = summary.GetSpentForCategory(categoryId, budget.Currency);
        }

        var purchase = new CreditCardPurchase(date, amount, creditAccountId, categoryId, beneficiaryPersonId, payerPersonId, description, notes);

        creditAccount.RegisterCharge(amount);

        await _transactionRepository.AddAsync(purchase, ct);
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

    public async Task RecordCreditCardPaymentAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid creditAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var sourceAccount = await _accountRepository.GetByIdAsync(sourceAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{sourceAccountId}' was not found.");

        var creditAccount = await _creditAccountRepository.GetByIdAsync(creditAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{creditAccountId}' was not found.");

        if (creditAccount is not CreditCard)
            throw new InvalidOperationException($"Credit account '{creditAccountId}' is not a credit card.");

        // README §6/§10: same cross-currency rejection as RecordTransferAsync — no conversion feature
        // in scope, so a numeric amount can't be safely applied to accounts in different currencies.
        if (sourceAccount.Currency != creditAccount.Currency)
            throw new InvalidOperationException(
                $"Cannot pay a card with an account in a different currency ({sourceAccount.Currency} -> {creditAccount.Currency}) without currency conversion support.");

        var payment = new CreditCardPayment(date, amount, sourceAccountId, creditAccountId, description, notes);

        // Credit side first: RegisterPayment's overpayment guard is the stricter/newer invariant — if it
        // throws, the source account must not have been mutated yet.
        creditAccount.RegisterPayment(amount);
        sourceAccount.Debit(amount);

        await _transactionRepository.AddAsync(payment, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }

    public async Task RecordLoanPaymentAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid loanAccountId,
        DateOnly nextPaymentDate,
        decimal requiredPayment,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        var sourceAccount = await _accountRepository.GetByIdAsync(sourceAccountId, ct)
            ?? throw new InvalidOperationException($"Account '{sourceAccountId}' was not found.");

        var creditAccount = await _creditAccountRepository.GetByIdAsync(loanAccountId, ct)
            ?? throw new InvalidOperationException($"Credit account '{loanAccountId}' was not found.");

        if (creditAccount is not Loan loan)
            throw new InvalidOperationException($"Credit account '{loanAccountId}' is not a loan.");

        if (sourceAccount.Currency != loan.Currency)
            throw new InvalidOperationException(
                $"Cannot pay a loan with an account in a different currency ({sourceAccount.Currency} -> {loan.Currency}) without currency conversion support.");

        var payment = new LoanPayment(date, amount, sourceAccountId, loanAccountId, description, notes);

        // Credit side first: RegisterPayment's overpayment guard is the stricter/newer invariant — if it
        // throws, the source account must not have been mutated yet. Same ordering as CreditCardPayment.
        loan.RegisterPayment(amount);
        loan.AdvanceSchedule(nextPaymentDate, requiredPayment);
        sourceAccount.Debit(amount);

        await _transactionRepository.AddAsync(payment, ct);
        await _transactionRepository.SaveChangesAsync(ct);
    }
}
