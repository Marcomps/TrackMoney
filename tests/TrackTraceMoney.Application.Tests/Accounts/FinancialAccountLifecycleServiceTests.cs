using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Accounts;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Domain.RecurringIncomes;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.Accounts;

/// <summary>
/// Covers <see cref="FinancialAccountLifecycleService"/> against every referencing scenario listed in
/// the edit/delete slice spec §1.2/§1.1: transaction references, recurring-expense references,
/// recurring-income references (each independently blocks hard delete), the re-check-on-delete
/// invariant (never trusts a caller's cached "yes"), and the narrower currency-edit guard (only
/// transaction references matter there, per §1.1's "one check, two call sites" reuse).
/// </summary>
public sealed class FinancialAccountLifecycleServiceTests
{
    private static (
        FinancialAccountLifecycleService Service,
        InMemoryFinancialAccountRepository Accounts,
        StubTransactionRepository Transactions,
        StubRecurringExpenseRepository RecurringExpenses,
        StubRecurringIncomeRepository RecurringIncomes) CreateSut()
    {
        var accounts = new InMemoryFinancialAccountRepository();
        var transactions = new StubTransactionRepository();
        var recurringExpenses = new StubRecurringExpenseRepository();
        var recurringIncomes = new StubRecurringIncomeRepository();
        var service = new FinancialAccountLifecycleService(accounts, transactions, recurringExpenses, recurringIncomes);
        return (service, accounts, transactions, recurringExpenses, recurringIncomes);
    }

    [Fact]
    public async Task CanHardDeleteAsync_NoReferences_ReturnsTrue()
    {
        var (service, accounts, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));

        Assert.True(await service.CanHardDeleteAsync(account.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_TransactionReferencesIt_ReturnsFalse()
    {
        var (service, accounts, transactions, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));
        transactions.ReferencedFinancialAccountIds.Add(account.Id);

        Assert.False(await service.CanHardDeleteAsync(account.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_RecurringExpenseReferencesIt_ReturnsFalse()
    {
        var (service, accounts, _, recurringExpenses, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));
        recurringExpenses.ReferencedAccountIds.Add(account.Id);

        Assert.False(await service.CanHardDeleteAsync(account.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_RecurringIncomeReferencesIt_ReturnsFalse()
    {
        var (service, accounts, _, _, recurringIncomes) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));
        recurringIncomes.ReferencedAccountIds.Add(account.Id);

        Assert.False(await service.CanHardDeleteAsync(account.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenAllowed_RemovesAccount()
    {
        var (service, accounts, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));

        await service.DeleteAsync(account.Id);

        Assert.Null(await accounts.GetByIdAsync(account.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenBlockedByTransactionReference_ThrowsAndDoesNotRemove()
    {
        // Proves DeleteAsync re-checks CanHardDeleteAsync itself rather than trusting a caller's
        // cached "yes" -- no prior CanHardDeleteAsync call is made by this test at all.
        var (service, accounts, transactions, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));
        transactions.ReferencedFinancialAccountIds.Add(account.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(account.Id));

        Assert.NotNull(await accounts.GetByIdAsync(account.Id));
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse()
    {
        var (service, accounts, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));

        await service.DeactivateAsync(account.Id);

        Assert.False(account.IsActive);
    }

    [Fact]
    public async Task ReactivateAsync_SetsIsActiveTrue()
    {
        var (service, accounts, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));
        account.Deactivate();

        await service.ReactivateAsync(account.Id);

        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task CanChangeCurrencyAsync_NoTransactionReferences_ReturnsTrue()
    {
        var (service, accounts, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));

        Assert.True(await service.CanChangeCurrencyAsync(account.Id));
    }

    [Fact]
    public async Task CanChangeCurrencyAsync_TransactionReferencesIt_ReturnsFalse()
    {
        var (service, accounts, transactions, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));
        transactions.ReferencedFinancialAccountIds.Add(account.Id);

        Assert.False(await service.CanChangeCurrencyAsync(account.Id));
    }

    [Fact]
    public async Task CanChangeCurrencyAsync_OnlyRecurringExpenseReferencesIt_StillReturnsTrue()
    {
        // The currency guard deliberately reuses only the transaction-reference check (spec §1.1's
        // "one check, two call sites"), NOT the full three-part deletion guard -- a recurring
        // definition alone has posted no money yet, so it carries no currency-denominated balance.
        var (service, accounts, _, recurringExpenses, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD));
        recurringExpenses.ReferencedAccountIds.Add(account.Id);

        Assert.True(await service.CanChangeCurrencyAsync(account.Id));
    }

    private sealed class InMemoryFinancialAccountRepository : IFinancialAccountRepository
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

    /// <summary>
    /// Configurable stand-in for <see cref="ITransactionRepository"/> — tests populate
    /// <see cref="ReferencedFinancialAccountIds"/>/<see cref="ReferencedCreditAccountIds"/> directly
    /// rather than constructing real transactions, since only the two Has* existence checks are
    /// exercised by <see cref="FinancialAccountLifecycleService"/>.
    /// </summary>
    private sealed class StubTransactionRepository : ITransactionRepository
    {
        public HashSet<Guid> ReferencedFinancialAccountIds { get; } = [];

        public HashSet<Guid> ReferencedCreditAccountIds { get; } = [];

        public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Transaction?>(null);

        public Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);

        public Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);

        public Task<IReadOnlyList<Transaction>> GetByDateRangeAndCategoryAsync(DateOnly from, DateOnly to, Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);

        public Task<IReadOnlyList<Transaction>> GetByDateRangeAndSpendAccountAsync(DateOnly from, DateOnly to, Guid spendAccountId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);

        public Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(
            DateOnly from, DateOnly to, Guid creditAccountId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditCardPayment>>([]);

        public Task<IReadOnlyList<CreditCardPayment>> GetCreditCardPaymentsUpToDateForCreditAccountsAsync(
            DateOnly to, IEnumerable<Guid> creditAccountIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditCardPayment>>([]);

        public Task<IReadOnlyList<Transaction>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Transaction>>([]);

        public Task AddAsync(Transaction entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(Transaction entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> HasAnyTransactionReferencingFinancialAccountAsync(Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedFinancialAccountIds.Contains(accountId));

        public Task<bool> HasAnyTransactionReferencingCreditAccountAsync(Guid creditAccountId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedCreditAccountIds.Contains(creditAccountId));
    }

    private sealed class StubRecurringExpenseRepository : IRecurringExpenseRepository
    {
        public HashSet<Guid> ReferencedAccountIds { get; } = [];

        public Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RecurringExpense?>(null);

        public Task<IReadOnlyList<RecurringExpense>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringExpense>>([]);

        public Task<IReadOnlyList<RecurringExpense>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringExpense>>([]);

        public Task<bool> HasAnyReferencingAccountAsync(Guid accountOrCreditAccountId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedAccountIds.Contains(accountOrCreditAccountId));

        public Task AddAsync(RecurringExpense entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(RecurringExpense entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubRecurringIncomeRepository : IRecurringIncomeRepository
    {
        public HashSet<Guid> ReferencedAccountIds { get; } = [];

        public Task<RecurringIncome?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RecurringIncome?>(null);

        public Task<IReadOnlyList<RecurringIncome>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringIncome>>([]);

        public Task<IReadOnlyList<RecurringIncome>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringIncome>>([]);

        public Task<bool> HasAnyReferencingAccountAsync(Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedAccountIds.Contains(accountId));

        public Task AddAsync(RecurringIncome entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(RecurringIncome entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
