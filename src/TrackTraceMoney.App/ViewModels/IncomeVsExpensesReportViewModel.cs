using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// "Income vs. expenses" report (README §40 slice 1) — same shape as <c>DashboardViewModel</c>'s Tile 2
/// (<see cref="ISpendingCalculator"/> + <see cref="IIncomeCalculator"/>), except for an arbitrary
/// month/year instead of always "this month". No currency picker: every currency present stacks as its
/// own group on one page (never blended — CLAUDE.md).
/// </summary>
public sealed partial class IncomeVsExpensesReportViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly IIncomeCalculator _incomeCalculator;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private DateTime selectedMonth = DateTime.Today;

    public ObservableCollection<IncomeExpenseReportGroup> Groups { get; } = [];

    public bool IsEmpty => HasLoaded && Groups.Count == 0;

    public IncomeVsExpensesReportViewModel(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        ISpendingCalculator spendingCalculator,
        IIncomeCalculator incomeCalculator)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _spendingCalculator = spendingCalculator;
        _incomeCalculator = incomeCalculator;
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
            var transactions = await _transactionRepository.GetByDateRangeAsync(startOfMonth, endOfMonth);

            var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);
            var spendingSummary = _spendingCalculator.Calculate(transactions, accountCurrencies);
            var incomeSummary = _incomeCalculator.Calculate(transactions, accountCurrencies);

            var currencies = spendingSummary.TotalSpentByCurrency.Keys
                .Union(incomeSummary.TotalIncomeByCurrency.Keys)
                .OrderBy(c => c.ToString());

            Groups.Clear();
            foreach (var currency in currencies)
            {
                var income = incomeSummary.TotalIncomeByCurrency.GetValueOrDefault(currency);
                var expenses = spendingSummary.TotalSpentByCurrency.GetValueOrDefault(currency);
                var available = income - expenses;
                var maxAmount = Math.Max(income, expenses);

                var bars = new[]
                {
                    new IncomeExpenseBarItem(AppResources.Dashboard_IncomeLabel, income, ReportBarWidth.Compute(income, maxAmount)),
                    new IncomeExpenseBarItem(AppResources.Dashboard_ExpensesLabel, expenses, ReportBarWidth.Compute(expenses, maxAmount))
                };

                Groups.Add(new IncomeExpenseReportGroup(currency, income, expenses, available, bars));
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
