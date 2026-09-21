using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.CreditAccounts;

/// <summary>
/// Covers <see cref="CreditAccountLifecycleService"/> against every referencing scenario the edit/
/// delete slice spec §2.2/§3.2 lists, and its polymorphic handling of <see cref="CreditCard"/> vs.
/// <see cref="Loan"/> (a card additionally checks statements; a loan deliberately never does, since it
/// has no <see cref="CreditCardStatement"/> concept).
/// </summary>
public sealed class CreditAccountLifecycleServiceTests
{
    private static (
        CreditAccountLifecycleService Service,
        InMemoryCreditAccountRepository CreditAccounts,
        StubTransactionRepository Transactions,
        StubRecurringExpenseRepository RecurringExpenses,
        StubCreditCardStatementRepository Statements) CreateSut()
    {
        var creditAccounts = new InMemoryCreditAccountRepository();
        var transactions = new StubTransactionRepository();
        var recurringExpenses = new StubRecurringExpenseRepository();
        var statements = new StubCreditCardStatementRepository();
        var service = new CreditAccountLifecycleService(creditAccounts, transactions, recurringExpenses, statements);
        return (service, creditAccounts, transactions, recurringExpenses, statements);
    }

    private static CreditCard CreateCard() =>
        new("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15);

    private static Loan CreateLoan() =>
        new(
            "Car Loan",
            CurrencyCode.USD,
            Guid.NewGuid(),
            LoanKind.AutoLoan,
            originalAmount: 5000m,
            currentBalance: 3250m,
            interestRate: 0.1m,
            rateType: LoanRateType.Fixed,
            monthlyInstallment: 150m,
            nextPaymentDate: new DateOnly(2026, 10, 1),
            requiredPayment: 150m);

    [Fact]
    public async Task CanHardDeleteAsync_CreditCard_NoReferences_ReturnsTrue()
    {
        var (service, creditAccounts, _, _, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());

        Assert.True(await service.CanHardDeleteAsync(card.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_CreditCard_TransactionReferencesIt_ReturnsFalse()
    {
        var (service, creditAccounts, transactions, _, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());
        transactions.ReferencedCreditAccountIds.Add(card.Id);

        Assert.False(await service.CanHardDeleteAsync(card.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_CreditCard_RecurringExpenseReferencesIt_ReturnsFalse()
    {
        var (service, creditAccounts, _, recurringExpenses, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());
        recurringExpenses.ReferencedAccountIds.Add(card.Id);

        Assert.False(await service.CanHardDeleteAsync(card.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_CreditCard_HasStatements_ReturnsFalse()
    {
        var (service, creditAccounts, _, _, statements) = CreateSut();
        var card = creditAccounts.Add(CreateCard());
        statements.StatementCountsByCard[card.Id] = 1;

        Assert.False(await service.CanHardDeleteAsync(card.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_Loan_NoReferences_ReturnsTrue()
    {
        var (service, creditAccounts, _, _, _) = CreateSut();
        var loan = creditAccounts.Add(CreateLoan());

        Assert.True(await service.CanHardDeleteAsync(loan.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_Loan_TransactionReferencesIt_ReturnsFalse()
    {
        var (service, creditAccounts, transactions, _, _) = CreateSut();
        var loan = creditAccounts.Add(CreateLoan());
        transactions.ReferencedCreditAccountIds.Add(loan.Id);

        Assert.False(await service.CanHardDeleteAsync(loan.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_Loan_RecurringExpenseReferencesIt_ReturnsFalse()
    {
        var (service, creditAccounts, _, recurringExpenses, _) = CreateSut();
        var loan = creditAccounts.Add(CreateLoan());
        recurringExpenses.ReferencedAccountIds.Add(loan.Id);

        Assert.False(await service.CanHardDeleteAsync(loan.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_Loan_IgnoresStatementCheckEntirely()
    {
        // Proves the statement check is skipped for a Loan, not just coincidentally empty -- a "phantom"
        // statement row is seeded against the loan's id (loans never really have statements in
        // production) and CanHardDeleteAsync must still return true, proving the CreditCard-only branch
        // never runs for a Loan.
        var (service, creditAccounts, _, _, statements) = CreateSut();
        var loan = creditAccounts.Add(CreateLoan());
        statements.StatementCountsByCard[loan.Id] = 1;

        Assert.True(await service.CanHardDeleteAsync(loan.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_UnknownCreditAccount_Throws()
    {
        var (service, _, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CanHardDeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_WhenAllowed_RemovesCreditAccount()
    {
        var (service, creditAccounts, _, _, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());

        await service.DeleteAsync(card.Id);

        Assert.Null(await creditAccounts.GetByIdAsync(card.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenBlockedByStatement_ThrowsAndDoesNotRemove()
    {
        // Proves DeleteAsync re-checks CanHardDeleteAsync itself -- no prior CanHardDeleteAsync call is
        // made by this test at all.
        var (service, creditAccounts, _, _, statements) = CreateSut();
        var card = creditAccounts.Add(CreateCard());
        statements.StatementCountsByCard[card.Id] = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(card.Id));

        Assert.NotNull(await creditAccounts.GetByIdAsync(card.Id));
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse()
    {
        var (service, creditAccounts, _, _, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());

        await service.DeactivateAsync(card.Id);

        Assert.False(card.IsActive);
    }

    [Fact]
    public async Task ReactivateAsync_SetsIsActiveTrue()
    {
        var (service, creditAccounts, _, _, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());
        card.Deactivate();

        await service.ReactivateAsync(card.Id);

        Assert.True(card.IsActive);
    }

    [Fact]
    public async Task CanChangeCurrencyAsync_NoTransactionReferences_ReturnsTrue()
    {
        var (service, creditAccounts, _, _, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());

        Assert.True(await service.CanChangeCurrencyAsync(card.Id));
    }

    [Fact]
    public async Task CanChangeCurrencyAsync_TransactionReferencesIt_ReturnsFalse()
    {
        var (service, creditAccounts, transactions, _, _) = CreateSut();
        var card = creditAccounts.Add(CreateCard());
        transactions.ReferencedCreditAccountIds.Add(card.Id);

        Assert.False(await service.CanChangeCurrencyAsync(card.Id));
    }

    private sealed class InMemoryCreditAccountRepository : ICreditAccountRepository
    {
        private readonly Dictionary<Guid, CreditAccount> _creditAccounts = new();

        public CreditAccount Add(CreditAccount creditAccount)
        {
            _creditAccounts[creditAccount.Id] = creditAccount;
            return creditAccount;
        }

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

    /// <summary>
    /// Configurable stand-in for <see cref="ITransactionRepository"/> — tests populate
    /// <see cref="ReferencedCreditAccountIds"/> directly rather than constructing real transactions,
    /// since only <see cref="ITransactionRepository.HasAnyTransactionReferencingCreditAccountAsync"/> is
    /// exercised by <see cref="CreditAccountLifecycleService"/>.
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

        public Task<bool> HasAnyTransactionReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(false);
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

        public Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task AddAsync(RecurringExpense entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(RecurringExpense entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Configurable stand-in for <see cref="ICreditCardStatementRepository"/> — tests populate
    /// <see cref="StatementCountsByCard"/> directly rather than constructing real statements, since only
    /// <see cref="ICreditCardStatementRepository.GetForCardAsync"/>'s emptiness is checked by
    /// <see cref="CreditAccountLifecycleService"/>.
    /// </summary>
    private sealed class StubCreditCardStatementRepository : ICreditCardStatementRepository
    {
        public Dictionary<Guid, int> StatementCountsByCard { get; } = [];

        public Task<CreditCardStatement?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<CreditCardStatement?>(null);

        public Task<IReadOnlyList<CreditCardStatement>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditCardStatement>>([]);

        public Task<CreditCardStatement?> GetLatestForCardAsync(Guid creditAccountId, CancellationToken ct = default) =>
            Task.FromResult<CreditCardStatement?>(null);

        public Task<IReadOnlyList<CreditCardStatement>> GetForCardAsync(Guid creditAccountId, CancellationToken ct = default)
        {
            // Only Count matters to CreditAccountLifecycleService -- placeholder elements, never
            // dereferenced by the code under test.
            var count = StatementCountsByCard.GetValueOrDefault(creditAccountId);
            var placeholder = Enumerable.Repeat<CreditCardStatement>(null!, count).ToList();
            return Task.FromResult<IReadOnlyList<CreditCardStatement>>(placeholder);
        }

        public Task<IReadOnlyDictionary<Guid, CreditCardStatement>> GetLatestForCardsAsync(IEnumerable<Guid> creditAccountIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, CreditCardStatement>>(new Dictionary<Guid, CreditCardStatement>());

        public Task AddAsync(CreditCardStatement entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(CreditCardStatement entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
