using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Application.Reporting;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// "Budget vs. spending" report (README §40 slice 1) — reuses <see cref="IBudgetEvaluator.Evaluate"/>
/// exactly like <c>DashboardViewModel</c>'s Tile 3, except it renders EVERY <see cref="BudgetStatus"/>
/// (comfortably-under budgets included), not just over-budget ones — the first screen in this app where
/// every budget is visible side by side. Zero new calculation logic. No currency picker: every currency
/// present stacks as its own group on one page (never blended — CLAUDE.md).
/// </summary>
public sealed partial class BudgetVsSpendingReportViewModel : ObservableObject
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly IBudgetEvaluator _budgetEvaluator;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private DateTime selectedMonth = DateTime.Today;

    public ObservableCollection<BudgetSpendReportGroup> Groups { get; } = [];

    public bool IsEmpty => HasLoaded && Groups.Count == 0;

    public BudgetVsSpendingReportViewModel(
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        ISpendingCalculator spendingCalculator,
        IBudgetEvaluator budgetEvaluator)
    {
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _spendingCalculator = spendingCalculator;
        _budgetEvaluator = budgetEvaluator;
    }

    partial void OnSelectedMonthChanged(DateTime value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var year = SelectedMonth.Year;
            var month = SelectedMonth.Month;
            var startOfMonth = new DateOnly(year, month, 1);
            var endOfMonth = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

            var budgets = await _budgetRepository.GetForMonthAsync(year, month);
            var categories = await _categoryRepository.GetAllAsync();
            var accounts = await _accountRepository.GetAllAsync();
            var creditAccounts = await _creditAccountRepository.GetAllAsync();
            var transactions = await _transactionRepository.GetByDateRangeAsync(startOfMonth, endOfMonth);

            var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);
            var spendingSummary = _spendingCalculator.Calculate(transactions, accountCurrencies);
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);

            // BudgetEvaluator.Evaluate returns one status per input budget, in the same order — zipped
            // by position, same idiom as DashboardViewModel's Tile 3 (a category can legitimately have
            // more than one budget in the same month, one per currency).
            var statuses = _budgetEvaluator.Evaluate(budgets, spendingSummary);

            var items = budgets.Zip(statuses, (budget, status) =>
            {
                var categoryName = categoryNames.TryGetValue(budget.CategoryId, out var name) ? name : "?";
                return BudgetSpendReportItem.FromDomain(budget, status, categoryName);
            });

            Groups.Clear();
            foreach (var currencyGroup in items.GroupBy(item => item.ListItem.Currency).OrderBy(g => g.Key.ToString()))
                Groups.Add(new BudgetSpendReportGroup(currencyGroup.Key, currencyGroup.OrderByDescending(i => i.ListItem.PercentUsed)));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }
    }
}
