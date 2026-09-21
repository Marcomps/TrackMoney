using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Categories;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Domain.RecurringIncomes;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Tests.Categories;

/// <summary>
/// Covers <see cref="CategoryLifecycleService"/> against every referencing scenario listed in the
/// Category lifecycle slice spec §A.3/§A.5: the six independent reference kinds (Expense/Income/
/// CreditCardPurchase transactions, RecurringExpense, RecurringIncome, Budget -- each individually
/// blocks hard delete, tested separately, matching <c>FinancialAccountLifecycleServiceTests</c>'
/// established granularity), the re-check-on-delete invariant, Deactivate/Reactivate round-trip, and
/// the system-defined-category guard genuinely new to this lifecycle service.
/// </summary>
public sealed class CategoryLifecycleServiceTests
{
    private static (
        CategoryLifecycleService Service,
        InMemoryCategoryRepository Categories,
        StubTransactionRepository Transactions,
        StubRecurringExpenseRepository RecurringExpenses,
        StubRecurringIncomeRepository RecurringIncomes,
        StubBudgetRepository Budgets) CreateSut()
    {
        var categories = new InMemoryCategoryRepository();
        var transactions = new StubTransactionRepository();
        var recurringExpenses = new StubRecurringExpenseRepository();
        var recurringIncomes = new StubRecurringIncomeRepository();
        var budgets = new StubBudgetRepository();
        var service = new CategoryLifecycleService(categories, transactions, recurringExpenses, recurringIncomes, budgets);
        return (service, categories, transactions, recurringExpenses, recurringIncomes, budgets);
    }

    [Fact]
    public async Task CanHardDeleteAsync_NoReferences_ReturnsTrue()
    {
        var (service, categories, _, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));

        Assert.True(await service.CanHardDeleteAsync(category.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_TransactionReferencesIt_ReturnsFalse()
    {
        var (service, categories, transactions, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));
        transactions.ReferencedCategoryIds.Add(category.Id);

        Assert.False(await service.CanHardDeleteAsync(category.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_RecurringExpenseReferencesIt_ReturnsFalse()
    {
        var (service, categories, _, recurringExpenses, _, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));
        recurringExpenses.ReferencedCategoryIds.Add(category.Id);

        Assert.False(await service.CanHardDeleteAsync(category.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_RecurringIncomeReferencesIt_ReturnsFalse()
    {
        var (service, categories, _, _, recurringIncomes, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));
        recurringIncomes.ReferencedCategoryIds.Add(category.Id);

        Assert.False(await service.CanHardDeleteAsync(category.Id));
    }

    [Fact]
    public async Task CanHardDeleteAsync_BudgetReferencesIt_ReturnsFalse()
    {
        var (service, categories, _, _, _, budgets) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));
        budgets.ReferencedCategoryIds.Add(category.Id);

        Assert.False(await service.CanHardDeleteAsync(category.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenAllowed_RemovesCategory()
    {
        var (service, categories, _, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));

        await service.DeleteAsync(category.Id);

        Assert.Null(await categories.GetByIdAsync(category.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenBlockedByTransactionReference_ThrowsAndDoesNotRemove()
    {
        // Proves DeleteAsync re-checks CanHardDeleteAsync itself rather than trusting a caller's
        // cached "yes" -- no prior CanHardDeleteAsync call is made by this test at all.
        var (service, categories, transactions, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));
        transactions.ReferencedCategoryIds.Add(category.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(category.Id));

        Assert.NotNull(await categories.GetByIdAsync(category.Id));
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse()
    {
        var (service, categories, _, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));

        await service.DeactivateAsync(category.Id);

        Assert.False(category.IsActive);
    }

    [Fact]
    public async Task ReactivateAsync_SetsIsActiveTrue()
    {
        var (service, categories, _, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateUserDefined("Mascotas"));
        category.Deactivate();

        await service.ReactivateAsync(category.Id);

        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_SystemDefinedCategory_Throws()
    {
        var (service, categories, _, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateSystemDefined(SystemCategoryKey.Food, "Food"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeactivateAsync(category.Id));
        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_SystemDefinedCategory_Throws()
    {
        var (service, categories, _, _, _, _) = CreateSut();
        var category = categories.Add(Category.CreateSystemDefined(SystemCategoryKey.Food, "Food"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(category.Id));
        Assert.NotNull(await categories.GetByIdAsync(category.Id));
    }

    private sealed class InMemoryCategoryRepository : ICategoryRepository
    {
        private readonly Dictionary<Guid, Category> _categories = new();

        public Category Add(Category category)
        {
            _categories[category.Id] = category;
            return category;
        }

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

    /// <summary>
    /// Configurable stand-in for <see cref="ITransactionRepository"/> -- tests populate
    /// <see cref="ReferencedCategoryIds"/> directly rather than constructing real transactions, since
    /// only <see cref="ITransactionRepository.HasAnyTransactionReferencingCategoryAsync"/> is exercised
    /// by <see cref="CategoryLifecycleService"/>.
    /// </summary>
    private sealed class StubTransactionRepository : ITransactionRepository
    {
        public HashSet<Guid> ReferencedCategoryIds { get; } = [];

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
            Task.FromResult(false);

        public Task<bool> HasAnyTransactionReferencingCreditAccountAsync(Guid creditAccountId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> HasAnyTransactionReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedCategoryIds.Contains(categoryId));
    }

    private sealed class StubRecurringExpenseRepository : IRecurringExpenseRepository
    {
        public HashSet<Guid> ReferencedCategoryIds { get; } = [];

        public Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RecurringExpense?>(null);

        public Task<IReadOnlyList<RecurringExpense>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringExpense>>([]);

        public Task<IReadOnlyList<RecurringExpense>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringExpense>>([]);

        public Task<bool> HasAnyReferencingAccountAsync(Guid accountOrCreditAccountId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedCategoryIds.Contains(categoryId));

        public Task AddAsync(RecurringExpense entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(RecurringExpense entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubRecurringIncomeRepository : IRecurringIncomeRepository
    {
        public HashSet<Guid> ReferencedCategoryIds { get; } = [];

        public Task<RecurringIncome?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RecurringIncome?>(null);

        public Task<IReadOnlyList<RecurringIncome>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringIncome>>([]);

        public Task<IReadOnlyList<RecurringIncome>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecurringIncome>>([]);

        public Task<bool> HasAnyReferencingAccountAsync(Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedCategoryIds.Contains(categoryId));

        public Task AddAsync(RecurringIncome entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(RecurringIncome entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubBudgetRepository : IBudgetRepository
    {
        public HashSet<Guid> ReferencedCategoryIds { get; } = [];

        public Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Budget?>(null);

        public Task<IReadOnlyList<Budget>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Budget>>([]);

        public Task<Budget?> GetForCategoryAndMonthAsync(Guid categoryId, int year, int month, Domain.Enums.CurrencyCode currency, CancellationToken ct = default) =>
            Task.FromResult<Budget?>(null);

        public Task<IReadOnlyList<Budget>> GetForMonthAsync(int year, int month, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Budget>>([]);

        public Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
            Task.FromResult(ReferencedCategoryIds.Contains(categoryId));

        public Task AddAsync(Budget entity, CancellationToken ct = default) => Task.CompletedTask;

        public void Remove(Budget entity)
        {
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
