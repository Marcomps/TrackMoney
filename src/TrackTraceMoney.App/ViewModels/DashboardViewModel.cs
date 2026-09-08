using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Application.Reporting;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly IIncomeCalculator _incomeCalculator;
    private readonly IBudgetEvaluator _budgetEvaluator;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private string healthEmoji = string.Empty;

    [ObservableProperty]
    private string healthLabel = string.Empty;

    public ObservableCollection<CurrencyBalance> Balances { get; } = [];

    public ObservableCollection<CurrencyIncomeExpense> IncomeExpenseSummaries { get; } = [];

    public ObservableCollection<BudgetListItem> OverBudgetCategories { get; } = [];

    public ObservableCollection<RecurringExpenseListItem> UpcomingPayments { get; } = [];

    public bool HasOverBudgetCategories => OverBudgetCategories.Count > 0;

    public bool ShowOverBudgetEmpty => HasLoaded && !HasOverBudgetCategories;

    public bool HasUpcomingPayments => UpcomingPayments.Count > 0;

    public bool ShowUpcomingPaymentsEmpty => HasLoaded && !HasUpcomingPayments;

    public DashboardViewModel(
        IFinancialAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IRecurringExpenseRepository recurringExpenseRepository,
        ISpendingCalculator spendingCalculator,
        IIncomeCalculator incomeCalculator,
        IBudgetEvaluator budgetEvaluator)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
        _spendingCalculator = spendingCalculator;
        _incomeCalculator = incomeCalculator;
        _budgetEvaluator = budgetEvaluator;
    }

    [RelayCommand]
    private async Task LoadDashboardAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);

            var accounts = await _accountRepository.GetActiveAsync();
            var transactions = await _transactionRepository.GetByDateRangeAsync(startOfMonth, today);
            var budgets = await _budgetRepository.GetForMonthAsync(today.Year, today.Month);
            var categories = await _categoryRepository.GetAllAsync();
            var recurringExpenses = await _recurringExpenseRepository.GetActiveAsync();

            // Tile 1: available balance per currency — never summed across currencies.
            Balances.Clear();
            foreach (var group in accounts.GroupBy(a => a.Currency))
                Balances.Add(new CurrencyBalance(group.Key, group.Sum(a => a.Balance)));

            // Tile 2: income / expenses / available this month — per currency, never blended
            // (mirrors Tile 1's accounts.GroupBy(a => a.Currency) above).
            var accountCurrencies = accounts.ToDictionary(a => a.Id, a => a.Currency);
            var spendingSummary = _spendingCalculator.Calculate(transactions, accountCurrencies);
            var incomeSummary = _incomeCalculator.Calculate(transactions, accountCurrencies);

            IncomeExpenseSummaries.Clear();
            var currencies = spendingSummary.TotalSpentByCurrency.Keys.Union(incomeSummary.TotalIncomeByCurrency.Keys);
            foreach (var currency in currencies)
            {
                var currencyIncome = incomeSummary.TotalIncomeByCurrency.GetValueOrDefault(currency);
                var currencyExpenses = spendingSummary.TotalSpentByCurrency.GetValueOrDefault(currency);
                IncomeExpenseSummaries.Add(new CurrencyIncomeExpense(currency, currencyIncome, currencyExpenses));
            }

            // Tile 3: over-budget categories this month.
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            // BudgetEvaluator.Evaluate returns one status per input budget, in the same order — zipped
            // by position rather than a CategoryId-keyed dictionary, because a category can now
            // legitimately have more than one budget in the same month (one per currency).
            var statuses = _budgetEvaluator.Evaluate(budgets, spendingSummary);

            OverBudgetCategories.Clear();
            foreach (var (budget, status) in budgets.Zip(statuses))
            {
                if (!status.IsOverBudget)
                    continue;

                var categoryName = categoryNames.TryGetValue(budget.CategoryId, out var name) ? name : "?";
                OverBudgetCategories.Add(BudgetListItem.FromDomain(budget, status, categoryName));
            }

            // Tile 4: financial health indicator v0.
            var hasNegativeBalance = accounts.Any(a => a.Balance < 0);
            var health = FinancialHealthEvaluator.Evaluate(hasNegativeBalance, OverBudgetCategories.Count > 0);
            HealthEmoji = FinancialHealthEvaluator.GetEmoji(health);
            HealthLabel = health switch
            {
                FinancialHealthStatus.Critical => AppResources.Dashboard_Health_Critical,
                FinancialHealthStatus.AtRisk => AppResources.Dashboard_Health_AtRisk,
                _ => AppResources.Dashboard_Health_Healthy
            };

            // Tile 5: upcoming payments — recurring expenses due now or due within the next 7 days
            // (README §30 question 4, "What do I need to pay?"). Read-only here: confirming or
            // deactivating an occurrence stays exclusively on the Recurring Expenses screen.
            var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);
            var dueSoonCutoff = today.AddDays(7);

            UpcomingPayments.Clear();
            foreach (var recurringExpense in recurringExpenses
                         .Where(r => r.IsDue(dueSoonCutoff))
                         .OrderBy(r => r.NextOccurrenceDate))
            {
                var categoryName = categoryNames.TryGetValue(recurringExpense.CategoryId, out var name) ? name : "?";
                var accountName = accountNames.TryGetValue(recurringExpense.AccountId, out var accName) ? accName : "?";
                UpcomingPayments.Add(RecurringExpenseListItem.FromDomain(recurringExpense, categoryName, accountName));
            }

            HasLoaded = true;
            OnPropertyChanged(nameof(HasOverBudgetCategories));
            OnPropertyChanged(nameof(ShowOverBudgetEmpty));
            OnPropertyChanged(nameof(HasUpcomingPayments));
            OnPropertyChanged(nameof(ShowUpcomingPaymentsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task GoToRecurringExpensesAsync()
    {
        // The Recurring Expenses tab's ShellContent registers "RecurringExpensesList" as its own
        // route (see AppShell.xaml) rather than the page type name — it's reached by switching tabs
        // (an absolute "//" route), unlike the "AddXxxPage" pages pushed via Routing.RegisterRoute.
        await Shell.Current.GoToAsync("//RecurringExpensesList");
    }
}
