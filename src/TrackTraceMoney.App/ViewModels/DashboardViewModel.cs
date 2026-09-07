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
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly IIncomeCalculator _incomeCalculator;
    private readonly IBudgetEvaluator _budgetEvaluator;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private decimal income;

    [ObservableProperty]
    private decimal expenses;

    [ObservableProperty]
    private decimal available;

    [ObservableProperty]
    private string healthEmoji = string.Empty;

    [ObservableProperty]
    private string healthLabel = string.Empty;

    public ObservableCollection<CurrencyBalance> Balances { get; } = [];

    public ObservableCollection<BudgetListItem> OverBudgetCategories { get; } = [];

    public bool HasOverBudgetCategories => OverBudgetCategories.Count > 0;

    public bool ShowOverBudgetEmpty => HasLoaded && !HasOverBudgetCategories;

    public DashboardViewModel(
        IFinancialAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        ISpendingCalculator spendingCalculator,
        IIncomeCalculator incomeCalculator,
        IBudgetEvaluator budgetEvaluator)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
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

            // Tile 1: available balance per currency — never summed across currencies.
            Balances.Clear();
            foreach (var group in accounts.GroupBy(a => a.Currency))
                Balances.Add(new CurrencyBalance(group.Key, group.Sum(a => a.Balance)));

            // Tile 2: income / expenses / available this month.
            var spendingSummary = _spendingCalculator.Calculate(transactions);
            var incomeSummary = _incomeCalculator.Calculate(transactions);
            Income = incomeSummary.TotalIncome;
            Expenses = spendingSummary.TotalSpent;
            Available = Income - Expenses;

            // Tile 3: over-budget categories this month.
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            var statuses = _budgetEvaluator.Evaluate(budgets, spendingSummary).ToDictionary(s => s.CategoryId);

            OverBudgetCategories.Clear();
            foreach (var budget in budgets)
            {
                if (!statuses.TryGetValue(budget.CategoryId, out var status) || !status.IsOverBudget)
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

            HasLoaded = true;
            OnPropertyChanged(nameof(HasOverBudgetCategories));
            OnPropertyChanged(nameof(ShowOverBudgetEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
