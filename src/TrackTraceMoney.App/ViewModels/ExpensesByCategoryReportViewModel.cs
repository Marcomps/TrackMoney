using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// "Expenses by category" report (README §40 slice 1) — reuses <see cref="ISpendingCalculator"/> exactly
/// like <c>DashboardViewModel</c>'s Tile 3, except for an arbitrary month/year instead of always "this
/// month". No currency picker: every currency present stacks as its own bar-chart group on one page (the
/// slice spec's §5 single-period-snapshot pattern), never blended (CLAUDE.md).
/// </summary>
public sealed partial class ExpensesByCategoryReportViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISpendingCalculator _spendingCalculator;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private DateTime selectedMonth = DateTime.Today;

    public ObservableCollection<CategorySpendReportGroup> Groups { get; } = [];

    public bool IsEmpty => HasLoaded && Groups.Count == 0;

    public ExpensesByCategoryReportViewModel(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        ICategoryRepository categoryRepository,
        ISpendingCalculator spendingCalculator)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _categoryRepository = categoryRepository;
        _spendingCalculator = spendingCalculator;
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
            var startOfMonth = new DateOnly(SelectedMonth.Year, SelectedMonth.Month, 1);
            var endOfMonth = new DateOnly(SelectedMonth.Year, SelectedMonth.Month, DateTime.DaysInMonth(SelectedMonth.Year, SelectedMonth.Month));

            var accounts = await _accountRepository.GetAllAsync();
            var creditAccounts = await _creditAccountRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();
            var transactions = await _transactionRepository.GetByDateRangeAsync(startOfMonth, endOfMonth);

            var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);
            var spendingSummary = _spendingCalculator.Calculate(transactions, accountCurrencies);
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);

            Groups.Clear();
            foreach (var currencyGroup in spendingSummary.SpentByCategoryAndCurrency
                         .GroupBy(kvp => kvp.Key.Currency)
                         .OrderBy(g => g.Key.ToString()))
            {
                var maxAmount = currencyGroup.Max(kvp => kvp.Value);
                var total = currencyGroup.Sum(kvp => kvp.Value);

                var rows = currencyGroup
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => new CategorySpendBarItem(
                        categoryNames.TryGetValue(kvp.Key.CategoryId, out var name) ? name : "?",
                        kvp.Value,
                        kvp.Key.Currency,
                        ReportBarWidth.Compute(kvp.Value, maxAmount)));

                Groups.Add(new CategorySpendReportGroup(currencyGroup.Key, total, rows));
            }

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
