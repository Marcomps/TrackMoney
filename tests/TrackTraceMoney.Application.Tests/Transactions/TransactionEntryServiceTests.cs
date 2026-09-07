using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.Transactions;

/// <summary>
/// Covers the #1 correctness risk in this domain (README §9/§16, CLAUDE.md "Non-obvious domain
/// rules"): balances must be mutated exactly once per account per transaction, transfers must never
/// count as spend, and cross-currency transfers must be rejected rather than silently mis-booked.
/// </summary>
public sealed class TransactionEntryServiceTests
{
    private static (TransactionEntryService Service, InMemoryAccountRepository Accounts, InMemoryTransactionRepository Transactions) CreateSut()
    {
        var accounts = new InMemoryAccountRepository();
        var transactions = new InMemoryTransactionRepository();
        var service = new TransactionEntryService(transactions, accounts);
        return (service, accounts, transactions);
    }

    [Fact]
    public async Task RecordExpenseAsync_DebitsAccountExactlyOnce_AndCountsAsSpend()
    {
        var (service, accounts, transactions) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 30m, account.Id, categoryId, null, null, "Groceries", null);

        Assert.Equal(70m, account.Balance);
        var recorded = Assert.Single(transactions.All);
        Assert.True(recorded.CountsAsExpense);
        Assert.Equal(categoryId, recorded.SpendCategoryId);
    }

    [Fact]
    public async Task RecordIncomeAsync_CreditsAccountExactlyOnce_AndDoesNotCountAsSpend()
    {
        var (service, accounts, transactions) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();

        await service.RecordIncomeAsync(DateOnly.FromDateTime(DateTime.Today), 50m, account.Id, categoryId, null, "Salary", null);

        Assert.Equal(150m, account.Balance);
        var recorded = Assert.Single(transactions.All);
        Assert.False(recorded.CountsAsExpense);
    }

    [Fact]
    public async Task RecordTransferAsync_MovesFundsExactlyOnceEachSide_AndNeverCountsAsSpend()
    {
        var (service, accounts, transactions) = CreateSut();
        var source = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 200m));
        var destination = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));

        await service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 75m, source.Id, destination.Id, "Move to bank", null);

        Assert.Equal(125m, source.Balance);
        Assert.Equal(75m, destination.Balance);

        var recorded = Assert.Single(transactions.All);
        Assert.False(recorded.CountsAsExpense, "A transfer must never count as spend (README §9.1 / CLAUDE.md).");
    }

    [Fact]
    public async Task RecordTransferAsync_AcrossDifferentCurrencies_ThrowsInsteadOfSilentlyMisbookingAmounts()
    {
        // README §6/§10: currency is explicit per account; nothing may assume a single global
        // currency. Without conversion support, applying the same numeric amount to both a USD and
        // a MXN account would silently corrupt both balances by a large real-world factor.
        var (service, accounts, transactions) = CreateSut();
        var usdSource = accounts.Add(new CashAccount("USD Wallet", CurrencyCode.USD, openingBalance: 100m));
        var mxnDestination = accounts.Add(new BankAccount("MXN Checking", CurrencyCode.MXN, openingBalance: 0m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 50m, usdSource.Id, mxnDestination.Id, null, null));

        // Balances must be untouched — no partial mutation on the rejected path.
        Assert.Equal(100m, usdSource.Balance);
        Assert.Equal(0m, mxnDestination.Balance);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordExpenseAsync_ThenSeparatePayment_DoesNotDoubleCountAsSpend_WhenOnlyExpenseIsBooked()
    {
        // Regression guard for the #1 risk called out in CLAUDE.md: booking an expense against an
        // account and then moving money via a Transfer (standing in for a later "payment" leg, e.g.
        // paying off a card in a later phase) must leave exactly one CountsAsExpense=true record.
        var (service, accounts, transactions) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 500m));
        var savings = accounts.Add(new BankAccount("Savings", CurrencyCode.USD, openingBalance: 0m));
        var categoryId = Guid.NewGuid();

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 40m, checking.Id, categoryId, null, null, "Dinner", null);
        await service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 100m, checking.Id, savings.Id, "Move to savings", null);

        var spendTotal = transactions.All.Where(t => t.CountsAsExpense).Sum(t => t.Amount);
        Assert.Equal(40m, spendTotal);
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

        public Task AddAsync(Transaction entity, CancellationToken ct = default)
        {
            _transactions.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(Transaction entity) => _transactions.Remove(entity);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
