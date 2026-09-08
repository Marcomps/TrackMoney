using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.RecurringExpenses;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.RecurringExpenses;

/// <summary>
/// Covers the atomicity bug flagged in code review/QA: confirming a due occurrence posts an
/// <c>Expense</c> (via <c>ITransactionEntryService</c>, its own <c>SaveChangesAsync</c>) and then
/// marks the recurring expense confirmed (a second, separate <c>SaveChangesAsync</c>). Both must
/// commit or roll back together — otherwise a failure between the two leaves the Expense already
/// posted while the recurring expense still reports as due, letting a retry double-post the same
/// real-world payment.
/// </summary>
public sealed class RecurringExpenseServiceTests
{
    private static (
        RecurringExpenseService Service,
        InMemoryRecurringExpenseRepository RecurringExpenses,
        InMemoryAccountRepository Accounts,
        InMemoryTransactionRepository Transactions,
        FakeUnitOfWork UnitOfWork) CreateSut(bool throwOnRecurringExpenseSave = false)
    {
        var accounts = new InMemoryAccountRepository();
        var transactions = new InMemoryTransactionRepository();
        var budgets = new InMemoryBudgetRepository();
        var categories = new InMemoryCategoryRepository();
        var notifier = new FakeLocalNotifier();
        var transactionEntryService = new TransactionEntryService(transactions, accounts, budgets, categories, new SpendingCalculator(), notifier);

        var recurringExpenses = new InMemoryRecurringExpenseRepository { ThrowOnSaveChanges = throwOnRecurringExpenseSave };
        var unitOfWork = new FakeUnitOfWork();

        var service = new RecurringExpenseService(recurringExpenses, transactionEntryService, unitOfWork);

        return (service, recurringExpenses, accounts, transactions, unitOfWork);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_HappyPath_RecordsExactlyOneExpense_AndAdvancesLastConfirmedDate()
    {
        var (service, recurringExpenses, accounts, transactions, unitOfWork) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        var recurringExpense = recurringExpenses.Add(new RecurringExpense(
            "Netflix", 15m, categoryId, account.Id, RecurringExpenseFrequency.Monthly, startDate, endDate: null));

        await service.ConfirmOccurrenceAsync(recurringExpense.Id);

        var recorded = Assert.Single(transactions.All);
        Assert.True(recorded.CountsAsExpense);
        Assert.Equal(15m, recorded.Amount);
        Assert.Equal(85m, account.Balance);

        Assert.Equal(startDate, recurringExpense.LastConfirmedDate);

        Assert.True(unitOfWork.LastTransaction!.Committed);
        Assert.False(unitOfWork.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task ConfirmOccurrenceAsync_WhenMarkingConfirmedFails_RollsBackInsteadOfCommitting()
    {
        // Simulates the second SaveChangesAsync (recurringExpenseRepository) failing after the first
        // one (inside RecordExpenseAsync) already ran against the shared in-memory store — the exact
        // shape of the bug report's failure scenario (RecordExpenseAsync's own save succeeds, then
        // the outer MarkConfirmed save fails). With a real EF Core DbContext sharing one connection
        // (this app's actual DI shape — see LocalBackupService's remarks), rolling back the ambient
        // transaction here would also physically undo the Expense insert at the database level; that
        // part isn't observable through these in-memory Application-layer fakes, so this test proves
        // the piece that *is* observable at this layer: the service must roll back rather than commit,
        // and must propagate the failure rather than swallowing it, whenever the second save fails.
        var (service, recurringExpenses, accounts, transactions, unitOfWork) = CreateSut(throwOnRecurringExpenseSave: true);
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        var recurringExpense = recurringExpenses.Add(new RecurringExpense(
            "Netflix", 15m, categoryId, account.Id, RecurringExpenseFrequency.Monthly, startDate, endDate: null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmOccurrenceAsync(recurringExpense.Id));

        Assert.False(unitOfWork.LastTransaction!.Committed);
        Assert.True(unitOfWork.LastTransaction!.RolledBack);
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

    private sealed class InMemoryRecurringExpenseRepository : IRecurringExpenseRepository
    {
        private readonly Dictionary<Guid, RecurringExpense> _recurringExpenses = new();

        public bool ThrowOnSaveChanges { get; set; }

        public RecurringExpense Add(RecurringExpense recurringExpense)
        {
            _recurringExpenses[recurringExpense.Id] = recurringExpense;
            return recurringExpense;
        }

        public Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_recurringExpenses.GetValueOrDefault(id));

        public Task<IReadOnlyList<RecurringExpense>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringExpense>>(_recurringExpenses.Values.ToList());

        public Task<IReadOnlyList<RecurringExpense>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringExpense>>(_recurringExpenses.Values.Where(r => r.IsActive).ToList());

        public Task AddAsync(RecurringExpense entity, CancellationToken ct = default)
        {
            _recurringExpenses[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(RecurringExpense entity) => _recurringExpenses.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            if (ThrowOnSaveChanges)
                throw new InvalidOperationException("Simulated failure: recurring expense SaveChangesAsync failed (e.g. a locked SQLite file).");

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

        public Task AddAsync(Transaction entity, CancellationToken ct = default)
        {
            _transactions.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(Transaction entity) => _transactions.Remove(entity);

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

        public Task AddAsync(Category entity, CancellationToken ct = default)
        {
            _categories[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(Category entity) => _categories.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLocalNotifier : ILocalNotifier
    {
        public Task NotifyBudgetExceededAsync(Category category, decimal budgetAmount, decimal amountOver, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
