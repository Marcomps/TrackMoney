using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.CreditAccounts;
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
    private static (
        TransactionEntryService Service,
        InMemoryAccountRepository Accounts,
        InMemoryCreditAccountRepository CreditAccounts,
        InMemoryTransactionRepository Transactions,
        InMemoryBudgetRepository Budgets,
        InMemoryCategoryRepository Categories,
        FakeLocalNotifier Notifier) CreateSut()
    {
        var accounts = new InMemoryAccountRepository();
        var creditAccounts = new InMemoryCreditAccountRepository();
        var transactions = new InMemoryTransactionRepository();
        var budgets = new InMemoryBudgetRepository();
        var categories = new InMemoryCategoryRepository();
        var notifier = new FakeLocalNotifier();
        var service = new TransactionEntryService(transactions, accounts, creditAccounts, budgets, categories, new SpendingCalculator(), notifier);
        return (service, accounts, creditAccounts, transactions, budgets, categories, notifier);
    }

    [Fact]
    public async Task RecordExpenseAsync_DebitsAccountExactlyOnce_AndCountsAsSpend()
    {
        var (service, accounts, _, transactions, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 500m));
        var savings = accounts.Add(new BankAccount("Savings", CurrencyCode.USD, openingBalance: 0m));
        var categoryId = Guid.NewGuid();

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 40m, checking.Id, categoryId, null, null, "Dinner", null);
        await service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 100m, checking.Id, savings.Id, "Move to savings", null);

        var spendTotal = transactions.All.Where(t => t.CountsAsExpense).Sum(t => t.Amount);
        Assert.Equal(40m, spendTotal);
    }

    [Fact]
    public async Task RecordExpenseAsync_CrossingBudgetLimit_TriggersExactlyOneNotification()
    {
        // README §34/§37: the notification is a reactive trigger fired on the crossing transition —
        // an expense that stays at/below the budget must not notify at all.
        var (service, accounts, _, transactions, budgets, categories, notifier) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        await service.RecordExpenseAsync(today, 80m, account.Id, category.Id, null, null, "Lunch", null);
        Assert.Empty(notifier.Calls);

        await service.RecordExpenseAsync(today, 50m, account.Id, category.Id, null, null, "Dinner", null);

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(category.Id, call.Category.Id);
        Assert.Equal(100m, call.BudgetAmount);
        Assert.Equal(30m, call.AmountOver);
    }

    [Fact]
    public async Task RecordExpenseAsync_WhenAlreadyOverBudget_DoesNotNotifyAgain()
    {
        // Only the crossing transition notifies — a second expense in an already-over-budget category
        // must not fire a second notification.
        var (service, accounts, _, transactions, budgets, categories, notifier) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 1000m));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        await service.RecordExpenseAsync(today, 150m, account.Id, category.Id, null, null, "Big dinner", null);
        Assert.Single(notifier.Calls);

        await service.RecordExpenseAsync(today, 20m, account.Id, category.Id, null, null, "Another expense", null);

        Assert.Single(notifier.Calls);
    }

    [Fact]
    public async Task RecordExpenseAsync_DoesNotBlendSpendAcrossDifferentCurrencyAccounts_WhenEvaluatingBudget()
    {
        // QA-confirmed bug: a $90 MXN expense must never be summed together with USD spend when
        // evaluating a budget denominated in USD — only same-currency spend may ever cross a budget's
        // limit (CLAUDE.md: currency is explicit per account/transaction, never blended).
        var (service, accounts, _, transactions, budgets, categories, notifier) = CreateSut();
        var usdAccount = accounts.Add(new CashAccount("USD Wallet", CurrencyCode.USD, openingBalance: 1000m));
        var mxnAccount = accounts.Add(new CashAccount("MXN Wallet", CurrencyCode.MXN, openingBalance: 5000m));
        var category = categories.Add(Category.CreateUserDefined("Food"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        // A large MXN expense in the same category. With the bug (no Currency on Budget/Transaction,
        // spend summed as a currency-blind decimal), this would silently count toward the USD budget.
        await service.RecordExpenseAsync(today, 90m, mxnAccount.Id, category.Id, null, null, "Groceries MXN", null);
        Assert.Empty(notifier.Calls);

        // A USD expense that, on its own, stays comfortably under the $100 USD budget. Blended with
        // the $90 MXN expense above, the (incorrect) total would be 140 > 100 and would wrongly fire
        // the over-budget notification.
        await service.RecordExpenseAsync(today, 50m, usdAccount.Id, category.Id, null, null, "Groceries USD", null);

        Assert.Empty(notifier.Calls);
    }

    [Fact]
    public async Task RecordExpenseAsync_WithNoBudgetForCategoryAndMonth_NeverAttemptsNotification()
    {
        var (service, accounts, _, transactions, budgets, categories, notifier) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var categoryId = Guid.NewGuid();

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 999m, account.Id, categoryId, null, null, "Big spend", null);

        Assert.Empty(notifier.Calls);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_IncreasesCardDebt_AndCountsAsSpend()
    {
        // CLAUDE.md's #1 correctness risk: the purchase is the expense and must increase card debt.
        var (service, _, creditAccounts, transactions, _, _, _) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var categoryId = Guid.NewGuid();

        await service.RecordCreditCardPurchaseAsync(DateOnly.FromDateTime(DateTime.Today), 60m, card.Id, categoryId, null, null, "Groceries", null);

        Assert.Equal(60m, card.AmountOwed);
        var recorded = Assert.Single(transactions.All);
        Assert.True(recorded.CountsAsExpense);
        Assert.Equal(categoryId, recorded.SpendCategoryId);
        Assert.IsType<CreditCardPurchase>(recorded);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_DoesNotDebitAnyFinancialAccount()
    {
        // A card purchase must never touch a FinancialAccount's balance — only the card's debt moves.
        // The later payment (a future CreditCardPayment slice) is what debits a bank account.
        var (service, accounts, creditAccounts, transactions, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 500m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var categoryId = Guid.NewGuid();

        await service.RecordCreditCardPurchaseAsync(DateOnly.FromDateTime(DateTime.Today), 60m, card.Id, categoryId, null, null, "Groceries", null);

        Assert.Equal(500m, checking.Balance);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_CrossingBudgetLimit_TriggersExactlyOneNotification()
    {
        var (service, _, creditAccounts, transactions, budgets, categories, notifier) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        await service.RecordCreditCardPurchaseAsync(today, 80m, card.Id, category.Id, null, null, "Lunch", null);
        Assert.Empty(notifier.Calls);

        await service.RecordCreditCardPurchaseAsync(today, 50m, card.Id, category.Id, null, null, "Dinner", null);

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(category.Id, call.Category.Id);
        Assert.Equal(100m, call.BudgetAmount);
        Assert.Equal(30m, call.AmountOver);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_UnknownCreditAccount_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, _, _, transactions, _, _, _) = CreateSut();
        var categoryId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPurchaseAsync(DateOnly.FromDateTime(DateTime.Today), 60m, Guid.NewGuid(), categoryId, null, null, "Groceries", null));

        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_AndCashExpense_TogetherCrossBudget_ThatNeitherAloneWouldCross()
    {
        // Regression guard for the GetByDateRangeAndCategoryAsync fix (Infrastructure section 3):
        // production code previously only queried Expense, so a card purchase preceding a cash Expense
        // in the same category/month would be invisible to the budget-crossing check's "spentBefore"
        // calculation. Neither $60 nor $50 alone crosses the $100 budget, but together they must.
        var (service, accounts, creditAccounts, transactions, budgets, categories, notifier) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 500m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        await service.RecordCreditCardPurchaseAsync(today, 60m, card.Id, category.Id, null, null, "Card lunch", null);
        Assert.Empty(notifier.Calls);

        await service.RecordExpenseAsync(today, 50m, checking.Id, category.Id, null, null, "Cash dinner", null);

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(category.Id, call.Category.Id);
        Assert.Equal(100m, call.BudgetAmount);
        Assert.Equal(10m, call.AmountOver);

        var spendTotal = transactions.All.Where(t => t.CountsAsExpense).Sum(t => t.Amount);
        Assert.Equal(110m, spendTotal);
    }

    [Fact]
    public async Task RecordCreditCardPaymentAsync_DebitsSourceAndReducesDebt_ExactlyOnce()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        card.RegisterCharge(500m);

        await service.RecordCreditCardPaymentAsync(DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, card.Id, "Card payment", null);

        Assert.Equal(700m, checking.Balance);
        Assert.Equal(200m, card.AmountOwed);
        var recorded = Assert.Single(transactions.All);
        Assert.IsType<CreditCardPayment>(recorded);
    }

    [Fact]
    public async Task RecordCreditCardPaymentAsync_NeverCountsAsSpend_AndNeverTriggersBudgetNotification()
    {
        // THE critical test for this slice: the purchase already counted as spend; the payment must
        // not count again, and must never touch budget/notification machinery at all (CLAUDE.md's #1
        // correctness risk, inverse direction — do not wire ISpendingCalculator/ILocalNotifier here).
        var (service, accounts, creditAccounts, transactions, budgets, categories, notifier) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        // Push spend right up to the budget limit via a purchase in that category.
        await service.RecordCreditCardPurchaseAsync(today, 100m, card.Id, category.Id, null, null, "At the limit", null);
        var callsBeforePayment = notifier.Calls.Count;

        // Now pay down the card's debt — unrelated to the category, no category at all on this type.
        await service.RecordCreditCardPaymentAsync(today, 60m, checking.Id, card.Id, "Card payment", null);

        Assert.Equal(callsBeforePayment, notifier.Calls.Count);
        var recorded = transactions.All.OfType<CreditCardPayment>().Single();
        Assert.False(recorded.CountsAsExpense);
    }

    [Fact]
    public async Task RecordCreditCardPaymentAsync_Overpayment_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        card.RegisterCharge(200m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPaymentAsync(DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, card.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(200m, card.AmountOwed);
    }

    [Fact]
    public async Task RecordCreditCardPaymentAsync_AcrossDifferentCurrencies_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Tarjeta MXN", CurrencyCode.MXN, "Bank", creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        card.RegisterCharge(500m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPaymentAsync(DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, card.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(500m, card.AmountOwed);
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

        public Budget Add(Budget budget)
        {
            _budgets[budget.Id] = budget;
            return budget;
        }

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

        public Category Add(Category category)
        {
            _categories[category.Id] = category;
            return category;
        }

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

    /// <summary>Records every call so tests can assert on the crossing-transition-only firing behavior.</summary>
    private sealed class FakeLocalNotifier : ILocalNotifier
    {
        public List<(Category Category, decimal BudgetAmount, decimal AmountOver)> Calls { get; } = [];

        public Task NotifyBudgetExceededAsync(Category category, decimal budgetAmount, decimal amountOver, CancellationToken ct = default)
        {
            Calls.Add((category, budgetAmount, amountOver));
            return Task.CompletedTask;
        }
    }
}
