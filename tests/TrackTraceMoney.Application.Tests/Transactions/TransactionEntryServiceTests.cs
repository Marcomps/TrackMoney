using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.MedicalExpenses;
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
        FakeLocalNotifier Notifier,
        InMemoryMedicalExpenseDetailRepository MedicalExpenseDetails) CreateSut()
    {
        var accounts = new InMemoryAccountRepository();
        var creditAccounts = new InMemoryCreditAccountRepository();
        var transactions = new InMemoryTransactionRepository();
        var budgets = new InMemoryBudgetRepository();
        var categories = new InMemoryCategoryRepository();
        var notifier = new FakeLocalNotifier();
        var medicalExpenseDetails = new InMemoryMedicalExpenseDetailRepository();
        var service = new TransactionEntryService(transactions, accounts, creditAccounts, budgets, categories, new SpendingCalculator(), notifier, medicalExpenseDetails);
        return (service, accounts, creditAccounts, transactions, budgets, categories, notifier, medicalExpenseDetails);
    }

    [Fact]
    public async Task RecordExpenseAsync_DebitsAccountExactlyOnce_AndCountsAsSpend()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
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
        var (service, accounts, _, transactions, budgets, categories, notifier, _) = CreateSut();
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
        var (service, accounts, _, transactions, budgets, categories, notifier, _) = CreateSut();
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
        var (service, accounts, _, transactions, budgets, categories, notifier, _) = CreateSut();
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
        var (service, accounts, _, transactions, budgets, categories, notifier, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var categoryId = Guid.NewGuid();

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 999m, account.Id, categoryId, null, null, "Big spend", null);

        Assert.Empty(notifier.Calls);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_IncreasesCardDebt_AndCountsAsSpend()
    {
        // CLAUDE.md's #1 correctness risk: the purchase is the expense and must increase card debt.
        var (service, _, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
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
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 500m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var categoryId = Guid.NewGuid();

        await service.RecordCreditCardPurchaseAsync(DateOnly.FromDateTime(DateTime.Today), 60m, card.Id, categoryId, null, null, "Groceries", null);

        Assert.Equal(500m, checking.Balance);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_CrossingBudgetLimit_TriggersExactlyOneNotification()
    {
        var (service, _, creditAccounts, transactions, budgets, categories, notifier, _) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
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
        var (service, _, _, transactions, _, _, _, _) = CreateSut();
        var categoryId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPurchaseAsync(DateOnly.FromDateTime(DateTime.Today), 60m, Guid.NewGuid(), categoryId, null, null, "Groceries", null));

        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_CreditAccountIsALoan_ThrowsAndDoesNotRecordTransaction()
    {
        // Bug fix regression guard: a Loan must never be accepted by the card-only purchase flow —
        // RegisterCharge exists on the shared CreditAccount base, so without this guard a Loan would
        // silently accept a charge via the wrong code path (never calling Loan.AdvanceSchedule),
        // leaving NextPaymentDate/RequiredPayment stale forever.
        var (service, _, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.USD, currentBalance: 500m));
        var categoryId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPurchaseAsync(DateOnly.FromDateTime(DateTime.Today), 60m, loan.Id, categoryId, null, null, "Groceries", null));

        Assert.Empty(transactions.All);
        Assert.Equal(500m, loan.AmountOwed);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_AndCashExpense_TogetherCrossBudget_ThatNeitherAloneWouldCross()
    {
        // Regression guard for the GetByDateRangeAndCategoryAsync fix (Infrastructure section 3):
        // production code previously only queried Expense, so a card purchase preceding a cash Expense
        // in the same category/month would be invisible to the budget-crossing check's "spentBefore"
        // calculation. Neither $60 nor $50 alone crosses the $100 budget, but together they must.
        var (service, accounts, creditAccounts, transactions, budgets, categories, notifier, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 500m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
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
    public async Task RecordMedicalExpenseAsync_NoInsurance_CreatesDetailWithAllFieldsNull()
    {
        var (service, accounts, _, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var categoryId = Guid.NewGuid();
        var medicalInfo = new MedicalInsuranceInput(null, null, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, account.Id, categoryId, null, null, "Doctor visit", null, medicalInfo);

        Assert.Equal(420m, account.Balance);
        var recorded = Assert.Single(transactions.All);
        Assert.True(recorded.CountsAsExpense);

        var detail = Assert.Single(medicalExpenseDetails.All);
        Assert.Equal(recorded.Id, detail.TransactionId);
        Assert.Equal(MedicalReimbursementStatus.None, detail.Status);
        Assert.Null(detail.InsuranceProvider);
        Assert.Null(detail.GrossAmount);
        Assert.Null(detail.InsuranceCoveredAmount);
    }

    [Fact]
    public async Task RecordMedicalExpenseAsync_PendingReimbursement_GrossAmountEqualsAmountPaid()
    {
        var (service, accounts, _, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var categoryId = Guid.NewGuid();
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, account.Id, categoryId, null, null, "Doctor visit", null, medicalInfo);

        Assert.Equal(420m, account.Balance);
        var recorded = Assert.Single(transactions.All);

        var detail = Assert.Single(medicalExpenseDetails.All);
        Assert.Equal(recorded.Id, detail.TransactionId);
        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);
        Assert.Equal("Acme Insurance", detail.InsuranceProvider);
        Assert.Equal(80m, detail.GrossAmount);
        Assert.Equal(30m, detail.InsuranceCoveredAmount);
    }

    [Fact]
    public async Task RecordMedicalExpenseAsync_InsurancePaidProviderDirectly_GrossAmountIncludesInsurancePortion()
    {
        var (service, accounts, _, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var categoryId = Guid.NewGuid();
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: true);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, account.Id, categoryId, null, null, "Doctor visit", null, medicalInfo);

        Assert.Equal(420m, account.Balance);
        var recorded = Assert.Single(transactions.All);

        var detail = Assert.Single(medicalExpenseDetails.All);
        Assert.Equal(recorded.Id, detail.TransactionId);
        Assert.Equal(MedicalReimbursementStatus.PaidDirectly, detail.Status);
        Assert.Equal(110m, detail.GrossAmount);
        Assert.Equal(30m, detail.InsuranceCoveredAmount);
    }

    [Fact]
    public async Task RecordMedicalExpenseAsync_CrossingBudgetLimit_TriggersExactlyOneNotification()
    {
        // Same crossing-transition notification as plain RecordExpenseAsync — a medical expense must
        // still count as spend and notify identically (README §34/§37; CLAUDE.md's #1 risk).
        var (service, accounts, _, transactions, budgets, categories, notifier, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));
        var medicalInfo = new MedicalInsuranceInput(null, null, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(today, 80m, account.Id, category.Id, null, null, "Checkup", null, medicalInfo);
        Assert.Empty(notifier.Calls);

        await service.RecordMedicalExpenseAsync(today, 50m, account.Id, category.Id, null, null, "Follow-up", null, medicalInfo);

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(category.Id, call.Category.Id);
        Assert.Equal(100m, call.BudgetAmount);
        Assert.Equal(30m, call.AmountOver);
    }

    [Fact]
    public async Task RecordExpenseAsync_PlainExpense_CreatesZeroMedicalExpenseDetailRows()
    {
        // Regression guard: a plain (non-medical) expense must never create a MedicalExpenseDetail row.
        var (service, accounts, _, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var categoryId = Guid.NewGuid();

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 40m, account.Id, categoryId, null, null, "Groceries", null);

        Assert.Empty(medicalExpenseDetails.All);
    }

    [Fact]
    public async Task RecordMedicalCreditCardPurchaseAsync_PendingReimbursement_IncreasesCardDebt_AndCreatesDetail()
    {
        var (service, _, creditAccounts, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var categoryId = Guid.NewGuid();
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 20m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalCreditCardPurchaseAsync(
            DateOnly.FromDateTime(DateTime.Today), 60m, card.Id, categoryId, null, null, "Pharmacy", null, medicalInfo);

        Assert.Equal(60m, card.AmountOwed);
        var recorded = Assert.Single(transactions.All);
        Assert.True(recorded.CountsAsExpense);
        Assert.IsType<CreditCardPurchase>(recorded);

        var detail = Assert.Single(medicalExpenseDetails.All);
        Assert.Equal(recorded.Id, detail.TransactionId);
        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);
        Assert.Equal(60m, detail.GrossAmount);
    }

    [Fact]
    public async Task RecordMedicalCreditCardPurchaseAsync_InsurancePaidProviderDirectly_GrossAmountIncludesInsurancePortion()
    {
        var (service, _, creditAccounts, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var categoryId = Guid.NewGuid();
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 20m, InsurancePaidProviderDirectly: true);

        await service.RecordMedicalCreditCardPurchaseAsync(
            DateOnly.FromDateTime(DateTime.Today), 60m, card.Id, categoryId, null, null, "Pharmacy", null, medicalInfo);

        Assert.Equal(60m, card.AmountOwed);
        var detail = Assert.Single(medicalExpenseDetails.All);
        Assert.Equal(MedicalReimbursementStatus.PaidDirectly, detail.Status);
        Assert.Equal(80m, detail.GrossAmount);
    }

    [Fact]
    public async Task RecordMedicalCreditCardPurchaseAsync_CrossingBudgetLimit_TriggersExactlyOneNotification()
    {
        var (service, _, creditAccounts, transactions, budgets, categories, notifier, _) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));
        var medicalInfo = new MedicalInsuranceInput(null, null, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalCreditCardPurchaseAsync(today, 80m, card.Id, category.Id, null, null, "Checkup", null, medicalInfo);
        Assert.Empty(notifier.Calls);

        await service.RecordMedicalCreditCardPurchaseAsync(today, 50m, card.Id, category.Id, null, null, "Follow-up", null, medicalInfo);

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(category.Id, call.Category.Id);
        Assert.Equal(100m, call.BudgetAmount);
        Assert.Equal(30m, call.AmountOver);
    }

    [Fact]
    public async Task RecordMedicalCreditCardPurchaseAsync_CreditAccountIsALoan_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, _, creditAccounts, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.USD, currentBalance: 500m));
        var categoryId = Guid.NewGuid();
        var medicalInfo = new MedicalInsuranceInput(null, null, InsurancePaidProviderDirectly: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalCreditCardPurchaseAsync(
                DateOnly.FromDateTime(DateTime.Today), 60m, loan.Id, categoryId, null, null, "Pharmacy", null, medicalInfo));

        Assert.Empty(transactions.All);
        Assert.Empty(medicalExpenseDetails.All);
        Assert.Equal(500m, loan.AmountOwed);
    }

    [Fact]
    public async Task RecordCreditCardPurchaseAsync_PlainPurchase_CreatesZeroMedicalExpenseDetailRows()
    {
        // Regression guard: a plain (non-medical) card purchase must never create a MedicalExpenseDetail row.
        var (service, _, creditAccounts, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var categoryId = Guid.NewGuid();

        await service.RecordCreditCardPurchaseAsync(DateOnly.FromDateTime(DateTime.Today), 60m, card.Id, categoryId, null, null, "Groceries", null);

        Assert.Empty(medicalExpenseDetails.All);
    }

    [Fact]
    public async Task RecordCreditCardPaymentAsync_DebitsSourceAndReducesDebt_ExactlyOnce()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
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
        var (service, accounts, creditAccounts, transactions, budgets, categories, notifier, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
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
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
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
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Tarjeta MXN", CurrencyCode.MXN, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        card.RegisterCharge(500m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPaymentAsync(DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, card.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(500m, card.AmountOwed);
    }

    [Fact]
    public async Task RecordCreditCardPaymentAsync_CreditAccountIsALoan_ThrowsAndDoesNotRecordTransaction()
    {
        // Bug fix regression guard: a Loan must never be accepted by the card-only payment flow —
        // RegisterPayment exists on the shared CreditAccount base, so without this guard a Loan would
        // silently accept a payment via the wrong code path (never calling Loan.AdvanceSchedule),
        // leaving NextPaymentDate/RequiredPayment stale forever.
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.USD, currentBalance: 500m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPaymentAsync(DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, loan.Id, "Card payment", null));

        Assert.Empty(transactions.All);
        Assert.Equal(500m, loan.AmountOwed);
    }

    [Fact]
    public async Task RecordLoanPaymentAsync_DebitsSourceAndReducesDebt_AndAdvancesSchedule_ExactlyOnce()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.USD, currentBalance: 500m));

        var nextPaymentDate = new DateOnly(2026, 10, 10);

        await service.RecordLoanPaymentAsync(
            DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, loan.Id, nextPaymentDate, 150m, "Loan payment", null);

        Assert.Equal(700m, checking.Balance);
        Assert.Equal(200m, loan.AmountOwed);
        Assert.Equal(nextPaymentDate, loan.NextPaymentDate);
        Assert.Equal(150m, loan.RequiredPayment);
        var recorded = Assert.Single(transactions.All);
        Assert.IsType<LoanPayment>(recorded);
    }

    [Fact]
    public async Task RecordLoanPaymentAsync_NeverCountsAsSpend_AndNeverTriggersBudgetNotification()
    {
        // Mirrors RecordCreditCardPaymentAsync's equivalent test: push a category's budget right up to
        // its limit, then record a loan payment (unrelated — loan payments have no category at all) and
        // assert zero additional notifications and CountsAsExpense == false on the persisted transaction.
        var (service, accounts, creditAccounts, transactions, budgets, categories, notifier, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.USD, currentBalance: 500m));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        await service.RecordExpenseAsync(today, 100m, checking.Id, category.Id, null, null, "At the limit", null);
        var callsBeforePayment = notifier.Calls.Count;

        await service.RecordLoanPaymentAsync(today, 60m, checking.Id, loan.Id, today.AddMonths(1), 60m, "Loan payment", null);

        Assert.Equal(callsBeforePayment, notifier.Calls.Count);
        var recorded = transactions.All.OfType<LoanPayment>().Single();
        Assert.False(recorded.CountsAsExpense);
    }

    [Fact]
    public async Task RecordLoanPaymentAsync_AcrossDifferentCurrencies_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.MXN, currentBalance: 500m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordLoanPaymentAsync(
                DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, loan.Id, DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), 150m, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(500m, loan.AmountOwed);
    }

    [Fact]
    public async Task RecordLoanPaymentAsync_Overpayment_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.USD, currentBalance: 200m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordLoanPaymentAsync(
                DateOnly.FromDateTime(DateTime.Today), 300m, checking.Id, loan.Id, DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), 150m, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(200m, loan.AmountOwed);
    }

    [Fact]
    public async Task RecordLoanPaymentAsync_WrongCreditAccountType_Throws()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordLoanPaymentAsync(
                DateOnly.FromDateTime(DateTime.Today), 100m, checking.Id, card.Id, DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), 150m, null, null));

        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordInvestmentContributionAsync_DebitsSourceAndCreditsFund_AndUpdatesContributionsTotal_ExactlyOnce()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD, contributions: 2000m, openingBalance: 2084.50m));

        await service.RecordInvestmentContributionAsync(
            DateOnly.FromDateTime(DateTime.Today), 500m, checking.Id, fund.Id, "Monthly contribution", null);

        Assert.Equal(500m, checking.Balance);
        Assert.Equal(2584.50m, fund.Balance);
        Assert.Equal(2500m, fund.Contributions);
        var recorded = Assert.Single(transactions.All);
        Assert.IsType<InvestmentContribution>(recorded);
        Assert.False(recorded.CountsAsExpense);
        Assert.False(recorded.CountsAsIncome);
    }

    [Fact]
    public async Task RecordInvestmentContributionAsync_DestinationIsNotAnInvestmentFund_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var savings = accounts.Add(new BankAccount("Savings", CurrencyCode.USD, openingBalance: 0m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordInvestmentContributionAsync(DateOnly.FromDateTime(DateTime.Today), 500m, checking.Id, savings.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(0m, savings.Balance);
    }

    [Fact]
    public async Task RecordInvestmentContributionAsync_SameAccountForSourceAndFund_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordInvestmentContributionAsync(DateOnly.FromDateTime(DateTime.Today), 500m, fund.Id, fund.Id, null, null));

        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordInvestmentContributionAsync_AcrossDifferentCurrencies_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.MXN));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordInvestmentContributionAsync(DateOnly.FromDateTime(DateTime.Today), 500m, checking.Id, fund.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(2084.50m, fund.Balance);
    }

    [Fact]
    public async Task RecordInvestmentContributionAsync_NeverTriggersBudgetNotification()
    {
        var (service, accounts, _, transactions, budgets, categories, notifier, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        await service.RecordExpenseAsync(today, 100m, checking.Id, category.Id, null, null, "At the limit", null);
        var callsBeforeContribution = notifier.Calls.Count;

        await service.RecordInvestmentContributionAsync(today, 500m, checking.Id, fund.Id, null, null);

        Assert.Equal(callsBeforeContribution, notifier.Calls.Count);
    }

    [Fact]
    public async Task RecordInvestmentWithdrawalAsync_DebitsFundAndCreditsDestination_AndUpdatesWithdrawalsTotal_ExactlyOnce()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD, contributions: 2000m, openingBalance: 2084.50m));
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));

        await service.RecordInvestmentWithdrawalAsync(
            DateOnly.FromDateTime(DateTime.Today), 84.50m, fund.Id, checking.Id, "Cash out", null);

        Assert.Equal(2000m, fund.Balance);
        Assert.Equal(84.50m, fund.Withdrawals);
        Assert.Equal(84.50m, checking.Balance);
        var recorded = Assert.Single(transactions.All);
        Assert.IsType<InvestmentWithdrawal>(recorded);
        Assert.False(recorded.CountsAsExpense);
        Assert.False(recorded.CountsAsIncome);
    }

    [Fact]
    public async Task RecordInvestmentWithdrawalAsync_AllowsFundBalanceToGoNegative_NoOverdraftGuard()
    {
        // Decision under test: unlike CreditAccount.RegisterPayment's overpayment guard, an
        // InvestmentFund withdrawal must NOT be blocked from taking the fund's Balance negative — this
        // proves the no-guard decision was implemented deliberately, not accidentally guarded against.
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD, contributions: 100m, openingBalance: 100m));
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));

        await service.RecordInvestmentWithdrawalAsync(DateOnly.FromDateTime(DateTime.Today), 250m, fund.Id, checking.Id, null, null);

        Assert.Equal(-150m, fund.Balance);
        Assert.Equal(250m, fund.Withdrawals);
        Assert.Equal(250m, checking.Balance);
        Assert.Single(transactions.All);
    }

    [Fact]
    public async Task RecordInvestmentWithdrawalAsync_SourceIsNotAnInvestmentFund_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));
        var savings = accounts.Add(new BankAccount("Savings", CurrencyCode.USD, openingBalance: 0m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordInvestmentWithdrawalAsync(DateOnly.FromDateTime(DateTime.Today), 500m, checking.Id, savings.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
        Assert.Equal(0m, savings.Balance);
    }

    [Fact]
    public async Task RecordInvestmentWithdrawalAsync_SameAccountForFundAndDestination_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordInvestmentWithdrawalAsync(DateOnly.FromDateTime(DateTime.Today), 500m, fund.Id, fund.Id, null, null));

        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordInvestmentWithdrawalAsync_AcrossDifferentCurrencies_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD));
        var checking = accounts.Add(new BankAccount("Checking MXN", CurrencyCode.MXN, openingBalance: 0m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordInvestmentWithdrawalAsync(DateOnly.FromDateTime(DateTime.Today), 500m, fund.Id, checking.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(2084.50m, fund.Balance);
        Assert.Equal(0m, checking.Balance);
    }

    [Fact]
    public async Task RecordInvestmentWithdrawalAsync_NeverTriggersBudgetNotification()
    {
        var (service, accounts, _, transactions, budgets, categories, notifier, _) = CreateSut();
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD));
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 500m));
        var category = categories.Add(Category.CreateUserDefined("Dining"));
        var today = DateOnly.FromDateTime(DateTime.Today);
        budgets.Add(new Budget(category.Id, 100m, today.Year, today.Month, CurrencyCode.USD));

        await service.RecordExpenseAsync(today, 100m, checking.Id, category.Id, null, null, "At the limit", null);
        var callsBeforeWithdrawal = notifier.Calls.Count;

        await service.RecordInvestmentWithdrawalAsync(today, 50m, fund.Id, checking.Id, null, null);

        Assert.Equal(callsBeforeWithdrawal, notifier.Calls.Count);
    }

    [Fact]
    public async Task RecordInterestIncomeAsync_CreditsBalanceAndInterestReceived_AndCountsAsIncome_ExactlyOnce()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var termDeposit = (TermDeposit)accounts.Add(CreateTermDeposit(CurrencyCode.USD, openingBalance: 10000m));

        await service.RecordInterestIncomeAsync(
            DateOnly.FromDateTime(DateTime.Today), 41.67m, termDeposit.Id, "Monthly interest", null);

        Assert.Equal(10041.67m, termDeposit.Balance);
        Assert.Equal(41.67m, termDeposit.InterestReceived);
        var recorded = Assert.Single(transactions.All);
        Assert.IsType<InterestIncome>(recorded);
        Assert.True(recorded.CountsAsIncome);
        Assert.False(recorded.CountsAsExpense);
    }

    [Fact]
    public async Task RecordInterestIncomeAsync_AccountIsNotATermDeposit_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 1000m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordInterestIncomeAsync(DateOnly.FromDateTime(DateTime.Today), 41.67m, checking.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(1000m, checking.Balance);
    }

    [Fact]
    public async Task RecordInterestIncomeAsync_UnknownAccount_ThrowsAndDoesNotRecordTransaction()
    {
        var (service, _, _, transactions, _, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordInterestIncomeAsync(DateOnly.FromDateTime(DateTime.Today), 41.67m, Guid.NewGuid(), null, null));

        Assert.Empty(transactions.All);
    }

    // --- Checkpoint review HIGH #1: TermDeposit must be rejected as the source of the four flows below,
    // but must remain usable as Transfer's source (the deliberate, unguarded exception — see
    // RecordTransferAsync_TermDepositSource_StillMovesFunds below and TransferDestinationAccounts's doc
    // comment in AddTransactionViewModel).

    [Fact]
    public async Task RecordExpenseAsync_SourceIsATermDeposit_ThrowsAndDoesNotMutateBalance()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var termDeposit = (TermDeposit)accounts.Add(CreateTermDeposit(CurrencyCode.USD, openingBalance: 10000m));
        var categoryId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 100m, termDeposit.Id, categoryId, null, null, "Groceries", null));

        Assert.Equal(10000m, termDeposit.Balance);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordMedicalExpenseAsync_SourceIsATermDeposit_ThrowsAndDoesNotMutateBalance()
    {
        var (service, accounts, _, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var termDeposit = (TermDeposit)accounts.Add(CreateTermDeposit(CurrencyCode.USD, openingBalance: 10000m));
        var categoryId = Guid.NewGuid();
        var medicalInfo = new MedicalInsuranceInput(null, null, InsurancePaidProviderDirectly: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 100m, termDeposit.Id, categoryId, null, null, "Doctor visit", null, medicalInfo));

        Assert.Equal(10000m, termDeposit.Balance);
        Assert.Empty(transactions.All);
        Assert.Empty(medicalExpenseDetails.All);
    }

    [Fact]
    public async Task RecordCreditCardPaymentAsync_SourceIsATermDeposit_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var termDeposit = (TermDeposit)accounts.Add(CreateTermDeposit(CurrencyCode.USD, openingBalance: 10000m));
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        card.RegisterCharge(200m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordCreditCardPaymentAsync(DateOnly.FromDateTime(DateTime.Today), 100m, termDeposit.Id, card.Id, "Card payment", null));

        Assert.Equal(10000m, termDeposit.Balance);
        Assert.Equal(200m, card.AmountOwed);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordLoanPaymentAsync_SourceIsATermDeposit_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, creditAccounts, transactions, _, _, _, _) = CreateSut();
        var termDeposit = (TermDeposit)accounts.Add(CreateTermDeposit(CurrencyCode.USD, openingBalance: 10000m));
        var loan = (Loan)creditAccounts.Add(CreateLoan(CurrencyCode.USD, currentBalance: 500m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordLoanPaymentAsync(
                DateOnly.FromDateTime(DateTime.Today), 100m, termDeposit.Id, loan.Id, DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), 150m, null, null));

        Assert.Equal(10000m, termDeposit.Balance);
        Assert.Equal(500m, loan.AmountOwed);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordInvestmentContributionAsync_SourceIsATermDeposit_ThrowsAndDoesNotMutateEitherSide()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var termDeposit = (TermDeposit)accounts.Add(CreateTermDeposit(CurrencyCode.USD, openingBalance: 10000m));
        var fund = (InvestmentFund)accounts.Add(CreateInvestmentFund(CurrencyCode.USD));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordInvestmentContributionAsync(DateOnly.FromDateTime(DateTime.Today), 500m, termDeposit.Id, fund.Id, null, null));

        Assert.Equal(10000m, termDeposit.Balance);
        Assert.Equal(2084.50m, fund.Balance);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task RecordTransferAsync_TermDepositSource_StillMovesFunds_DeliberateException()
    {
        // The one deliberate exception to the TermDeposit-source guard above (slice 4 decision, see
        // TransferDestinationAccounts's doc comment in AddTransactionViewModel): moving a matured
        // deposit's proceeds out has no dedicated "close/redeem" flow yet, so Transfer's source side
        // must keep accepting a TermDeposit. This must keep passing after the HIGH #1 fix.
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var termDeposit = (TermDeposit)accounts.Add(CreateTermDeposit(CurrencyCode.USD, openingBalance: 10000m));
        var checking = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));

        await service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 10000m, termDeposit.Id, checking.Id, "Redeem matured deposit", null);

        Assert.Equal(0m, termDeposit.Balance);
        Assert.Equal(10000m, checking.Balance);
        var recorded = Assert.Single(transactions.All);
        Assert.IsType<Transfer>(recorded);
    }

    [Fact]
    public async Task RecordMedicalReimbursementAsync_HappyPath_PersistsReimbursement_CreditsAccount_AndMarksDetailReimbursedWithActualAmount()
    {
        var (service, accounts, _, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var bank = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, wallet.Id, category.Id, null, null, "Doctor visit", null, medicalInfo);
        var originalExpense = Assert.Single(transactions.All);
        var detail = Assert.Single(medicalExpenseDetails.All);
        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);

        // Actual amount received differs from the original $30 estimate — proves the real amount, not
        // the estimate, is what gets stored.
        await service.RecordMedicalReimbursementAsync(
            DateOnly.FromDateTime(DateTime.Today), 25m, originalExpense.Id, bank.Id, "Reimbursement", null);

        Assert.Equal(25m, bank.Balance);
        var reimbursement = Assert.Single(transactions.All.OfType<Reimbursement>());
        Assert.Equal(originalExpense.Id, reimbursement.LinkedTransactionId);
        Assert.Equal(bank.Id, reimbursement.DestinationAccountId);
        Assert.True(reimbursement.CountsAsIncome);
        Assert.False(reimbursement.CountsAsExpense);

        Assert.Equal(MedicalReimbursementStatus.Reimbursed, detail.Status);
        Assert.Equal(25m, detail.InsuranceCoveredAmount);

        // The original expense must never be mutated (CLAUDE.md's reimbursement rule).
        Assert.Equal(420m, wallet.Balance);
        Assert.Equal(80m, originalExpense.Amount);
    }

    [Fact]
    public async Task RecordMedicalReimbursementAsync_UnknownLinkedTransaction_Throws()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var bank = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalReimbursementAsync(
                DateOnly.FromDateTime(DateTime.Today), 25m, Guid.NewGuid(), bank.Id, null, null));

        Assert.Empty(transactions.All);
        Assert.Equal(0m, bank.Balance);
    }

    [Fact]
    public async Task RecordMedicalReimbursementAsync_LinkedTransactionHasNoMedicalExpenseDetail_Throws()
    {
        var (service, accounts, _, transactions, _, categories, _, _) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var bank = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));
        var category = categories.Add(Category.CreateUserDefined("Dining"));

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 40m, wallet.Id, category.Id, null, null, "Groceries", null);
        var plainExpense = Assert.Single(transactions.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalReimbursementAsync(
                DateOnly.FromDateTime(DateTime.Today), 25m, plainExpense.Id, bank.Id, null, null));

        Assert.Equal(0m, bank.Balance);
        Assert.Single(transactions.All);
    }

    [Theory]
    [InlineData(MedicalReimbursementStatus.None)]
    [InlineData(MedicalReimbursementStatus.PaidDirectly)]
    [InlineData(MedicalReimbursementStatus.Reimbursed)]
    [InlineData(MedicalReimbursementStatus.Rejected)]
    public async Task RecordMedicalReimbursementAsync_LinkedDetailNotPending_Throws(MedicalReimbursementStatus status)
    {
        var (service, accounts, _, transactions, _, _, _, medicalExpenseDetails) = CreateSut();
        var bank = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));

        // A plain (non-service) transaction, added directly so RecordMedicalReimbursementAsync's own
        // "linked transaction exists" lookup succeeds and the test isolates the status guard below.
        var linkedTransaction = new Expense(DateOnly.FromDateTime(DateTime.Today), 80m, Guid.NewGuid(), Guid.NewGuid(), null, null, "Doctor visit", null);
        await transactions.AddAsync(linkedTransaction);

        var (grossAmount, insuranceCoveredAmount, initialStatus) = status switch
        {
            MedicalReimbursementStatus.None => ((decimal?)null, (decimal?)null, MedicalReimbursementStatus.None),
            MedicalReimbursementStatus.PaidDirectly => (110m, 30m, MedicalReimbursementStatus.PaidDirectly),
            _ => (80m, 30m, MedicalReimbursementStatus.Pending)
        };

        var detail = new MedicalExpenseDetail(linkedTransaction.Id, "Acme Insurance", grossAmount, insuranceCoveredAmount, initialStatus);
        if (status == MedicalReimbursementStatus.Reimbursed)
            detail.MarkReimbursed(30m);
        else if (status == MedicalReimbursementStatus.Rejected)
            detail.MarkRejected();

        await medicalExpenseDetails.AddAsync(detail);

        var transactionCountBefore = transactions.All.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalReimbursementAsync(
                DateOnly.FromDateTime(DateTime.Today), 25m, detail.TransactionId, bank.Id, null, null));

        Assert.Equal(0m, bank.Balance);
        Assert.Equal(transactionCountBefore, transactions.All.Count);
        Assert.Equal(status, detail.Status);
    }

    [Fact]
    public async Task RecordMedicalReimbursementAsync_UnknownDestinationAccount_Throws()
    {
        var (service, accounts, _, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, wallet.Id, category.Id, null, null, "Doctor visit", null, medicalInfo);
        var originalExpense = Assert.Single(transactions.All);
        var detail = Assert.Single(medicalExpenseDetails.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalReimbursementAsync(
                DateOnly.FromDateTime(DateTime.Today), 25m, originalExpense.Id, Guid.NewGuid(), null, null));

        Assert.Single(transactions.All);
        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);
    }

    // --- Checkpoint review HIGH #3: RecordMedicalReimbursementAsync must reject a destination account
    // whose currency differs from the original expense's currency, for both supported linked-transaction
    // kinds (a plain Expense debiting a FinancialAccount, and a CreditCardPurchase charging a CreditAccount).

    [Fact]
    public async Task RecordMedicalReimbursementAsync_LinkedExpenseCurrencyDiffersFromDestination_ThrowsAndDoesNotMutateAnything()
    {
        var (service, accounts, _, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var mxnBank = accounts.Add(new BankAccount("Cuenta MXN", CurrencyCode.MXN, openingBalance: 0m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, wallet.Id, category.Id, null, null, "Doctor visit", null, medicalInfo);
        var originalExpense = Assert.Single(transactions.All);
        var detail = Assert.Single(medicalExpenseDetails.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalReimbursementAsync(
                DateOnly.FromDateTime(DateTime.Today), 25m, originalExpense.Id, mxnBank.Id, null, null));

        Assert.Equal(0m, mxnBank.Balance);
        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);
        Assert.Single(transactions.All);
    }

    [Fact]
    public async Task RecordMedicalReimbursementAsync_LinkedCreditCardPurchaseCurrencyDiffersFromDestination_ThrowsAndDoesNotMutateAnything()
    {
        var (service, accounts, creditAccounts, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var card = creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m, statementCutOffDay: 1, paymentDueDay: 15));
        var mxnBank = accounts.Add(new BankAccount("Cuenta MXN", CurrencyCode.MXN, openingBalance: 0m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 20m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalCreditCardPurchaseAsync(
            DateOnly.FromDateTime(DateTime.Today), 60m, card.Id, category.Id, null, null, "Pharmacy", null, medicalInfo);
        var originalPurchase = Assert.Single(transactions.All);
        var detail = Assert.Single(medicalExpenseDetails.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMedicalReimbursementAsync(
                DateOnly.FromDateTime(DateTime.Today), 15m, originalPurchase.Id, mxnBank.Id, null, null));

        Assert.Equal(0m, mxnBank.Balance);
        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);
        Assert.Single(transactions.All);
    }

    [Fact]
    public async Task RecordMedicalReimbursementAsync_PartialReimbursement_LessThanOriginalEstimate_Succeeds()
    {
        // Proves a partial reimbursement (actual < the original InsuranceCoveredAmount estimate) is not
        // blocked — MarkReimbursed only rejects amounts greater than GrossAmount, never less.
        var (service, accounts, _, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var bank = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, wallet.Id, category.Id, null, null, "Doctor visit", null, medicalInfo);
        var originalExpense = Assert.Single(transactions.All);
        var detail = Assert.Single(medicalExpenseDetails.All);

        await service.RecordMedicalReimbursementAsync(
            DateOnly.FromDateTime(DateTime.Today), 10m, originalExpense.Id, bank.Id, null, null);

        Assert.Equal(10m, bank.Balance);
        Assert.Equal(MedicalReimbursementStatus.Reimbursed, detail.Status);
        Assert.Equal(10m, detail.InsuranceCoveredAmount);
    }

    [Fact]
    public async Task RecordMedicalReimbursementAsync_AmountGreaterThanGrossAmount_ThrowsAndLeavesAccountBalanceUnchanged()
    {
        // Proves the mutation-ordering fix: MarkReimbursed's own guard must run BEFORE Credit, so a
        // thrown exception never leaves the destination account's Balance mutated.
        var (service, accounts, _, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var bank = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, wallet.Id, category.Id, null, null, "Doctor visit", null, medicalInfo);
        var originalExpense = Assert.Single(transactions.All);
        var detail = Assert.Single(medicalExpenseDetails.All);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.RecordMedicalReimbursementAsync(
                DateOnly.FromDateTime(DateTime.Today), 999m, originalExpense.Id, bank.Id, null, null));

        Assert.Equal(0m, bank.Balance);
        Assert.Equal(MedicalReimbursementStatus.Pending, detail.Status);
        Assert.Single(transactions.All);
    }

    [Fact]
    public async Task RejectMedicalReimbursementAsync_HappyPath_FlipsStatusToRejected_AndCreatesZeroTransactions()
    {
        var (service, accounts, _, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var category = categories.Add(Category.CreateUserDefined("Health"));
        var medicalInfo = new MedicalInsuranceInput("Acme Insurance", 30m, InsurancePaidProviderDirectly: false);

        await service.RecordMedicalExpenseAsync(
            DateOnly.FromDateTime(DateTime.Today), 80m, wallet.Id, category.Id, null, null, "Doctor visit", null, medicalInfo);
        var originalExpense = Assert.Single(transactions.All);
        var detail = Assert.Single(medicalExpenseDetails.All);
        var transactionCountBefore = transactions.All.Count;

        await service.RejectMedicalReimbursementAsync(originalExpense.Id);

        Assert.Equal(MedicalReimbursementStatus.Rejected, detail.Status);
        Assert.Equal(transactionCountBefore, transactions.All.Count);
    }

    [Fact]
    public async Task RejectMedicalReimbursementAsync_NoLinkedDetail_Throws()
    {
        var (service, _, _, _, _, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectMedicalReimbursementAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RejectMedicalReimbursementAsync_DetailNotPending_Throws()
    {
        var (service, accounts, _, transactions, _, categories, _, medicalExpenseDetails) = CreateSut();
        var wallet = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var category = categories.Add(Category.CreateUserDefined("Dining"));

        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 40m, wallet.Id, category.Id, null, null, "Groceries", null);
        var plainExpense = Assert.Single(transactions.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectMedicalReimbursementAsync(plainExpense.Id));
    }

    // --- ReverseExpenseAsync/ReverseIncomeAsync/ReverseTransferAsync (edit/delete slice spec §4.1) ---

    [Fact]
    public async Task ReverseExpenseAsync_CreditsAccountByExactAmount_AndRemovesTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 30m, account.Id, categoryId, null, null, "Groceries", null);
        var expense = Assert.Single(transactions.All);
        Assert.Equal(70m, account.Balance);

        await service.ReverseExpenseAsync(expense.Id);

        Assert.Equal(100m, account.Balance);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task ReverseExpenseAsync_UnknownTransaction_Throws()
    {
        var (service, _, _, _, _, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseExpenseAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReverseExpenseAsync_TransactionIsNotAnExpense_Throws()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var source = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 200m));
        var destination = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));
        await service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 50m, source.Id, destination.Id, null, null);
        var transfer = Assert.Single(transactions.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseExpenseAsync(transfer.Id));
    }

    [Fact]
    public async Task ReverseIncomeAsync_DebitsAccountByExactAmount_AndRemovesTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        await service.RecordIncomeAsync(DateOnly.FromDateTime(DateTime.Today), 50m, account.Id, categoryId, null, "Salary", null);
        var income = Assert.Single(transactions.All);
        Assert.Equal(150m, account.Balance);

        await service.ReverseIncomeAsync(income.Id);

        Assert.Equal(100m, account.Balance);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task ReverseIncomeAsync_UnknownTransaction_Throws()
    {
        var (service, _, _, _, _, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseIncomeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReverseIncomeAsync_TransactionIsNotAnIncome_Throws()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 30m, account.Id, categoryId, null, null, "Groceries", null);
        var expense = Assert.Single(transactions.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseIncomeAsync(expense.Id));
    }

    [Fact]
    public async Task ReverseTransferAsync_MovesFundsBackExactlyOnceEachSide_AndRemovesTransaction()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var source = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 200m));
        var destination = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));
        await service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 75m, source.Id, destination.Id, "Move to bank", null);
        var transfer = Assert.Single(transactions.All);
        Assert.Equal(125m, source.Balance);
        Assert.Equal(75m, destination.Balance);

        await service.ReverseTransferAsync(transfer.Id);

        Assert.Equal(200m, source.Balance);
        Assert.Equal(0m, destination.Balance);
        Assert.Empty(transactions.All);
    }

    [Fact]
    public async Task ReverseTransferAsync_RefetchesBothAccountsFresh_RatherThanTrustingStaleState()
    {
        // Mirrors how RecordTransferAsync's own tests prove the same fetch-fresh discipline: mutate
        // each account's Balance directly (as if another operation had touched it between recording and
        // reversing) and confirm the reversal's Credit/Debit lands on top of that intervening mutation
        // rather than on some stale snapshot captured earlier.
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var source = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 200m));
        var destination = accounts.Add(new BankAccount("Checking", CurrencyCode.USD, openingBalance: 0m));
        await service.RecordTransferAsync(DateOnly.FromDateTime(DateTime.Today), 75m, source.Id, destination.Id, "Move to bank", null);
        var transfer = Assert.Single(transactions.All);

        // Intervening mutation on both accounts, simulating another operation racing in between.
        source.Debit(10m);
        destination.Credit(20m);

        await service.ReverseTransferAsync(transfer.Id);

        // 125 (post-transfer) - 10 (intervening debit) + 75 (reversal credit) = 190.
        Assert.Equal(190m, source.Balance);
        // 75 (post-transfer) + 20 (intervening credit) - 75 (reversal debit) = 20.
        Assert.Equal(20m, destination.Balance);
    }

    [Fact]
    public async Task ReverseTransferAsync_UnknownTransaction_Throws()
    {
        var (service, _, _, _, _, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseTransferAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReverseTransferAsync_TransactionIsNotATransfer_Throws()
    {
        var (service, accounts, _, transactions, _, _, _, _) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 100m));
        var categoryId = Guid.NewGuid();
        await service.RecordExpenseAsync(DateOnly.FromDateTime(DateTime.Today), 30m, account.Id, categoryId, null, null, "Groceries", null);
        var expense = Assert.Single(transactions.All);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseTransferAsync(expense.Id));
    }

    private static TermDeposit CreateTermDeposit(CurrencyCode currency, decimal openingBalance = 10000m) =>
        new(
            "12-Month CD",
            currency,
            Guid.NewGuid(),
            initialPrincipal: 10000m,
            openingBalance,
            rate: 0.05m,
            TermDepositRateType.Nominal,
            startDate: new DateOnly(2026, 1, 1),
            maturityDate: new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.Monthly,
            isCompounding: true,
            autoRenewal: false);

    private static InvestmentFund CreateInvestmentFund(
        CurrencyCode currency,
        decimal contributions = 2000m,
        decimal openingBalance = 2084.50m) =>
        new(
            "Growth Fund",
            currency,
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            contributions,
            openingBalance);

    private static Loan CreateLoan(CurrencyCode currency, decimal currentBalance) =>
        new(
            "Car Loan",
            currency,
            Guid.NewGuid(),
            LoanKind.AutoLoan,
            originalAmount: 1000m,
            currentBalance: currentBalance,
            interestRate: 10m,
            rateType: LoanRateType.Fixed,
            monthlyInstallment: 100m,
            nextPaymentDate: DateOnly.FromDateTime(DateTime.Today),
            requiredPayment: 100m);

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

    private sealed class InMemoryMedicalExpenseDetailRepository : IMedicalExpenseDetailRepository
    {
        private readonly Dictionary<Guid, MedicalExpenseDetail> _details = new();

        public IReadOnlyList<MedicalExpenseDetail> All => _details.Values.ToList();

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

    /// <summary>Records every call so tests can assert on the crossing-transition-only firing behavior.</summary>
    private sealed class FakeLocalNotifier : ILocalNotifier
    {
        public List<(Category Category, decimal BudgetAmount, decimal AmountOver)> Calls { get; } = [];

        public Task NotifyBudgetExceededAsync(Category category, decimal budgetAmount, decimal amountOver, CancellationToken ct = default)
        {
            Calls.Add((category, budgetAmount, amountOver));
            return Task.CompletedTask;
        }

        public Task NotifyTermDepositRenewedAsync(string institution, decimal renewedAmount, CurrencyCode currency, DateOnly newMaturityDate, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
