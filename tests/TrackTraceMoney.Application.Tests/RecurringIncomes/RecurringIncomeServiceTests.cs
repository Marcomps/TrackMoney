using TrackTraceMoney.Application.Tests.TestDoubles;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.RecurringIncomes;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.MedicalExpenses;
using TrackTraceMoney.Domain.RecurringIncomes;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.RecurringIncomes;

/// <summary>
/// Mirrors <c>RecurringExpenseServiceTests</c>' atomicity coverage: confirming a due occurrence
/// posts an <c>Income</c> (via <c>ITransactionEntryService</c>, its own <c>SaveChangesAsync</c>)
/// and then marks the recurring income confirmed (a second, separate <c>SaveChangesAsync</c>).
/// Both must commit or roll back together — otherwise a failure between the two leaves the
/// Income already posted while the recurring income still reports as due, letting a retry
/// double-post the same real-world deposit.
/// </summary>
public sealed class RecurringIncomeServiceTests
{
    private static (
        RecurringIncomeService Service,
        InMemoryRecurringIncomeRepository RecurringIncomes,
        InMemoryAccountRepository Accounts,
        InMemoryTransactionRepository Transactions,
        FakeUnitOfWork UnitOfWork) CreateSut(bool throwOnRecurringIncomeSave = false)
    {
        var accounts = new InMemoryAccountRepository();
        var creditAccounts = new InMemoryCreditAccountRepository();
        var transactions = new InMemoryTransactionRepository();
        var budgets = new InMemoryBudgetRepository();
        var categories = new InMemoryCategoryRepository();
        var notifier = new FakeLocalNotifier();
        var medicalExpenseDetails = new InMemoryMedicalExpenseDetailRepository();
        var transactionEntryService = new TransactionEntryService(transactions, accounts, creditAccounts, budgets, categories, new SpendingCalculator(), notifier, medicalExpenseDetails, new InMemoryCreditCardStatementRepository());

        var recurringIncomes = new InMemoryRecurringIncomeRepository { ThrowOnSaveChanges = throwOnRecurringIncomeSave };
        var unitOfWork = new FakeUnitOfWork();

        var service = new RecurringIncomeService(recurringIncomes, transactionEntryService, unitOfWork);

        return (service, recurringIncomes, accounts, transactions, unitOfWork);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_HappyPath_RecordsExactlyOneIncome_CreditsAccount_AndAdvancesLastConfirmedDate()
    {
        var (service, recurringIncomes, accounts, transactions, unitOfWork) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        var recurringIncome = recurringIncomes.Add(new RecurringIncome(
            "Salary", 2000m, categoryId, account.Id, RecurringIncomeFrequency.Monthly, startDate, endDate: null));

        await service.ConfirmOccurrenceAsync(recurringIncome.Id, startDate);

        var recorded = Assert.Single(transactions.All);
        var income = Assert.IsType<Income>(recorded);
        Assert.True(income.CountsAsIncome);
        Assert.Equal(2000m, income.Amount);
        Assert.Null(income.PersonId);
        Assert.Equal(2100m, account.Balance);

        Assert.Equal(startDate, recurringIncome.LastConfirmedDate);

        Assert.True(unitOfWork.LastTransaction!.Committed);
        Assert.False(unitOfWork.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_WhenMarkingConfirmedFails_RollsBackInsteadOfCommitting()
    {
        // Simulates the second SaveChangesAsync (recurringIncomeRepository) failing after the first
        // one (inside RecordIncomeAsync) already ran against the shared in-memory store -- see
        // RecurringExpenseServiceTests' equivalent test for the full reasoning behind what is/isn't
        // observable through these in-memory Application-layer fakes.
        var (service, recurringIncomes, accounts, transactions, unitOfWork) = CreateSut(throwOnRecurringIncomeSave: true);
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        var recurringIncome = recurringIncomes.Add(new RecurringIncome(
            "Salary", 2000m, categoryId, account.Id, RecurringIncomeFrequency.Monthly, startDate, endDate: null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmOccurrenceAsync(recurringIncome.Id, startDate));

        Assert.False(unitOfWork.LastTransaction!.Committed);
        Assert.True(unitOfWork.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_AfterUpdateAmount_PostsNewAmount_NotOriginalAmount()
    {
        // Proves RecurringIncome.UpdateAmount's prospective-only contract end to end: confirming
        // after a "raise" posts the CURRENT amount, not the one the definition was created with.
        var (service, recurringIncomes, accounts, transactions, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 0m));
        var categoryId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        var recurringIncome = recurringIncomes.Add(new RecurringIncome(
            "Salary", 2000m, categoryId, account.Id, RecurringIncomeFrequency.Monthly, startDate, endDate: null));

        recurringIncome.UpdateAmount(2500m);

        await service.ConfirmOccurrenceAsync(recurringIncome.Id, startDate);

        var recorded = Assert.Single(transactions.All);
        Assert.Equal(2500m, recorded.Amount);
        Assert.Equal(2500m, account.Balance);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_EarlyWithinWindow_PostsIncomeDatedToday_AndAdvancesFromScheduledDate()
    {
        // Payday is Sunday the 30th; payroll landed Friday the 28th.
        var (service, recurringIncomes, accounts, transactions, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Agricola", CurrencyCode.USD, openingBalance: 181.01m));
        var scheduled = new DateOnly(2026, 9, 30);
        var today = new DateOnly(2026, 9, 28);
        var recurringIncome = recurringIncomes.Add(new RecurringIncome(
            "Salario", 654.52m, Guid.NewGuid(), account.Id, RecurringIncomeFrequency.SemiMonthly, scheduled, endDate: null));

        await service.ConfirmOccurrenceAsync(recurringIncome.Id, today);

        var recorded = Assert.Single(transactions.All);
        Assert.Equal(today, recorded.Date);
        Assert.Equal(835.53m, account.Balance);
        Assert.Equal(scheduled, recurringIncome.LastConfirmedDate);
        Assert.Equal(new DateOnly(2026, 10, 15), recurringIncome.NextOccurrenceDate);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_BeyondEarlyWindow_Throws_AndPostsNothing()
    {
        var (service, recurringIncomes, accounts, transactions, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Agricola", CurrencyCode.USD, openingBalance: 0m));
        var recurringIncome = recurringIncomes.Add(new RecurringIncome(
            "Salario", 654.52m, Guid.NewGuid(), account.Id, RecurringIncomeFrequency.SemiMonthly,
            new DateOnly(2026, 10, 15), endDate: null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ConfirmOccurrenceAsync(recurringIncome.Id, new DateOnly(2026, 9, 23)));

        Assert.Empty(transactions.All);
        Assert.Equal(0m, account.Balance);
        Assert.Null(recurringIncome.LastConfirmedDate);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_Late_KeepsScheduledDate()
    {
        var (service, recurringIncomes, accounts, transactions, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Agricola", CurrencyCode.USD, openingBalance: 0m));
        var scheduled = new DateOnly(2026, 9, 15);
        var recurringIncome = recurringIncomes.Add(new RecurringIncome(
            "Salario", 654.52m, Guid.NewGuid(), account.Id, RecurringIncomeFrequency.SemiMonthly, scheduled, endDate: null));

        await service.ConfirmOccurrenceAsync(recurringIncome.Id, new DateOnly(2026, 9, 23));

        Assert.Equal(scheduled, Assert.Single(transactions.All).Date);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public FakeTransaction? LastTransaction { get; private set; }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            LastTransaction = new FakeTransaction();
            return Task.FromResult<IUnitOfWorkTransaction>(LastTransaction);
        }

        public sealed class FakeTransaction : IUnitOfWorkTransaction
        {
            public bool Committed { get; private set; }

            public bool RolledBack { get; private set; }

            public Task CommitAsync(CancellationToken ct = default)
            {
                Committed = true;
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken ct = default)
            {
                RolledBack = true;
                return Task.CompletedTask;
            }

            public IDisposable EnterAmbientScope() => NoopScope.Instance;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            private sealed class NoopScope : IDisposable
            {
                public static readonly NoopScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }

    private sealed class InMemoryRecurringIncomeRepository : IRecurringIncomeRepository
    {
        private readonly Dictionary<Guid, RecurringIncome> _recurringIncomes = new();

        public bool ThrowOnSaveChanges { get; set; }

        public RecurringIncome Add(RecurringIncome recurringIncome)
        {
            _recurringIncomes[recurringIncome.Id] = recurringIncome;
            return recurringIncome;
        }

        public Task<RecurringIncome?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_recurringIncomes.GetValueOrDefault(id));

        public Task<IReadOnlyList<RecurringIncome>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringIncome>>(_recurringIncomes.Values.ToList());

        public Task<IReadOnlyList<RecurringIncome>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringIncome>>(_recurringIncomes.Values.Where(r => r.IsActive).ToList());

        public Task<bool> HasAnyReferencingAccountAsync(Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(_recurringIncomes.Values.Any(r => r.DestinationAccountId == accountId));

        public Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(_recurringIncomes.Values.Any(r => r.CategoryId == categoryId));

        public Task AddAsync(RecurringIncome entity, CancellationToken ct = default)
        {
            _recurringIncomes[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(RecurringIncome entity) => _recurringIncomes.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            if (ThrowOnSaveChanges)
                throw new InvalidOperationException("Simulated failure: recurring income SaveChangesAsync failed (e.g. a locked SQLite file).");

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAccountRepository : IFinancialAccountRepository
    {
        private readonly Dictionary<Guid, FinancialAccount> _accounts = new();

        public FinancialAccount Add(FinancialAccount account)
        {
            _accounts[account.Id] = account;
            return account;
        }

        public Task<FinancialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_accounts.GetValueOrDefault(id));

        public Task<IReadOnlyList<FinancialAccount>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FinancialAccount>>(_accounts.Values.ToList());

        public Task<IReadOnlyList<FinancialAccount>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FinancialAccount>>(_accounts.Values.Where(a => a.IsActive).ToList());

        public Task AddAsync(FinancialAccount entity, CancellationToken ct = default)
        {
            _accounts[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(FinancialAccount entity) => _accounts.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryTransactionRepository : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = [];

        public IReadOnlyList<Transaction> All => _transactions;

        public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_transactions.FirstOrDefault(t => t.Id == id));

        public Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>(_transactions.ToList());

        public Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>(_transactions.Where(t => t.Date >= from && t.Date <= to).ToList());

        public Task<IReadOnlyList<Transaction>> GetByDateRangeAndCategoryAsync(DateOnly from, DateOnly to, Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>(_transactions
                .Where(t => t.Date >= from && t.Date <= to && t.SpendCategoryId == categoryId)
                .ToList());

        public Task<IReadOnlyList<Transaction>> GetByDateRangeAndSpendAccountAsync(DateOnly from, DateOnly to, Guid spendAccountId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>(_transactions
                .Where(t => t.Date >= from && t.Date <= to && t.SpendAccountId == spendAccountId)
                .ToList());

        public Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(
            DateOnly from, DateOnly to, Guid creditAccountId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditCardPayment>>(_transactions
                .OfType<CreditCardPayment>()
                .Where(p => p.Date >= from && p.Date <= to && p.CreditAccountId == creditAccountId)
                .ToList());

        public Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsUpToDateForCreditAccountsAsync(
            DateOnly to, IEnumerable<Guid> creditAccountIds, CancellationToken ct = default)
        {
            var ids = creditAccountIds.ToList();
            return Task.FromResult<IReadOnlyList<CreditCardPayment>>(_transactions
                .OfType<CreditCardPayment>()
                .Where(p => p.Date <= to && ids.Contains(p.CreditAccountId))
                .ToList());
        }

        public Task<IReadOnlyList<Transaction>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var idSet = ids.ToList();
            return Task.FromResult<IReadOnlyList<Transaction>>(_transactions.Where(t => idSet.Contains(t.Id)).ToList());
        }

        public Task AddAsync(Transaction entity, CancellationToken ct = default)
        {
            _transactions.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(Transaction entity) => _transactions.Remove(entity);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> HasAnyTransactionReferencingFinancialAccountAsync(Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(_transactions.Any(t => t switch
            {
                Expense e => e.AccountId == accountId,
                Income i => i.DestinationAccountId == accountId,
                Transfer tr => tr.SourceAccountId == accountId || tr.DestinationAccountId == accountId,
                CreditCardPayment p => p.SourceAccountId == accountId,
                LoanPayment lp => lp.SourceAccountId == accountId,
                InvestmentContribution ic => ic.SourceAccountId == accountId || ic.DestinationAccountId == accountId,
                InvestmentWithdrawal iw => iw.SourceAccountId == accountId || iw.DestinationAccountId == accountId,
                InterestIncome ii => ii.DestinationAccountId == accountId,
                Reimbursement r => r.DestinationAccountId == accountId,
                _ => false,
            }));

        public Task<bool> HasAnyTransactionReferencingCreditAccountAsync(Guid creditAccountId, CancellationToken ct = default) =>
            Task.FromResult(_transactions.Any(t => t switch
            {
                CreditCardPurchase p => p.CreditAccountId == creditAccountId,
                CreditCardPayment p => p.CreditAccountId == creditAccountId,
                LoanPayment lp => lp.CreditAccountId == creditAccountId,
                _ => false,
            }));

        public Task<bool> HasAnyTransactionReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(_transactions.Any(t => t switch
            {
                Expense e => e.CategoryId == categoryId,
                Income i => i.CategoryId == categoryId,
                CreditCardPurchase p => p.CategoryId == categoryId,
                _ => false,
            }));
    }

    private sealed class InMemoryCreditAccountRepository : ICreditAccountRepository
    {
        private readonly Dictionary<Guid, CreditAccount> _creditAccounts = new();

        public Task<CreditAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_creditAccounts.GetValueOrDefault(id));

        public Task<IReadOnlyList<CreditAccount>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditAccount>>(_creditAccounts.Values.ToList());

        public Task<IReadOnlyList<CreditAccount>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditAccount>>(_creditAccounts.Values.Where(a => a.IsActive).ToList());

        public Task AddAsync(CreditAccount entity, CancellationToken ct = default)
        {
            _creditAccounts[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(CreditAccount entity) => _creditAccounts.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryBudgetRepository : IBudgetRepository
    {
        private readonly Dictionary<Guid, Budget> _budgets = new();

        public Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_budgets.GetValueOrDefault(id));

        public Task<IReadOnlyList<Budget>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Budget>>(_budgets.Values.ToList());

        public Task<Budget?> GetForCategoryAndMonthAsync(Guid categoryId, int year, int month, CurrencyCode currency, CancellationToken ct = default) =>
            Task.FromResult(_budgets.Values.FirstOrDefault(b => b.CategoryId == categoryId && b.Year == year && b.Month == month && b.Currency == currency));

        public Task<IReadOnlyList<Budget>> GetForMonthAsync(int year, int month, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Budget>>(_budgets.Values.Where(b => b.Year == year && b.Month == month).ToList());

        public Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(_budgets.Values.Any(b => b.CategoryId == categoryId));

        public Task AddAsync(Budget entity, CancellationToken ct = default)
        {
            _budgets[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(Budget entity) => _budgets.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryCategoryRepository : ICategoryRepository
    {
        private readonly Dictionary<Guid, Category> _categories = new();

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_categories.GetValueOrDefault(id));

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Category>>(_categories.Values.ToList());

        public Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Category>>(_categories.Values.Where(c => c.IsActive).ToList());

        public Task AddAsync(Category entity, CancellationToken ct = default)
        {
            _categories[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(Category entity) => _categories.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryMedicalExpenseDetailRepository : IMedicalExpenseDetailRepository
    {
        private readonly Dictionary<Guid, MedicalExpenseDetail> _details = new();

        public Task<MedicalExpenseDetail?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_details.GetValueOrDefault(id));

        public Task<IReadOnlyList<MedicalExpenseDetail>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MedicalExpenseDetail>>(_details.Values.ToList());

        public Task<MedicalExpenseDetail?> GetForTransactionAsync(Guid transactionId, CancellationToken ct = default) =>
            Task.FromResult(_details.Values.FirstOrDefault(d => d.TransactionId == transactionId));

        public Task<IReadOnlyDictionary<Guid, MedicalExpenseDetail>> GetForTransactionsAsync(IEnumerable<Guid> transactionIds, CancellationToken ct = default)
        {
            var ids = transactionIds.ToList();
            IReadOnlyDictionary<Guid, MedicalExpenseDetail> result = _details.Values
                .Where(d => ids.Contains(d.TransactionId))
                .ToDictionary(d => d.TransactionId);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<MedicalExpenseDetail>> GetPendingAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MedicalExpenseDetail>>(_details.Values
                .Where(d => d.Status == MedicalReimbursementStatus.Pending)
                .ToList());

        public Task AddAsync(MedicalExpenseDetail entity, CancellationToken ct = default)
        {
            _details[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(MedicalExpenseDetail entity) => _details.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLocalNotifier : ILocalNotifier
    {
        public Task NotifyBudgetExceededAsync(Category category, decimal budgetAmount, decimal amountOver, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task NotifyTermDepositRenewedAsync(string institution, decimal renewedAmount, CurrencyCode currency, DateOnly newMaturityDate, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
