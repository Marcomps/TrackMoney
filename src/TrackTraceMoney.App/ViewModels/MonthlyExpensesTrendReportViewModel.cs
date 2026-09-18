using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// "Monthly expenses trend" report (README §40 slice 1) — a multi-period series, unlike the other 3
/// slice-1 reports' single-period snapshots, so it mirrors <see cref="NetWorthViewModel"/>'s
/// currency-picker pattern (the slice spec's §5: stacking two currencies' bars on one trend axis would
/// conflate unrelated scales). Fetches a bounded one-year lookback window
/// (<see cref="HistoryViewModel"/>'s already-exercised default range), not the full transaction history.
/// </summary>
public sealed partial class MonthlyExpensesTrendReportViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IMonthlySpendingTrendCalculator _trendCalculator;

    private MonthlySpendingTrendSummary _summary = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private CurrencyCode selectedCurrency;

    public ObservableCollection<CurrencyCode> AvailableCurrencies { get; } = [];

    public ObservableCollection<MonthlyTrendBarItem> Rows { get; } = [];

    public bool IsEmpty => HasLoaded && Rows.Count == 0;

    public MonthlyExpensesTrendReportViewModel(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        IMonthlySpendingTrendCalculator trendCalculator)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _trendCalculator = trendCalculator;
    }

    partial void OnSelectedCurrencyChanged(CurrencyCode value) => LoadRowsForSelectedCurrencyCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var from = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));
            var to = DateOnly.FromDateTime(DateTime.Today);

            var accounts = await _accountRepository.GetAllAsync();
            var creditAccounts = await _creditAccountRepository.GetAllAsync();
            var transactions = await _transactionRepository.GetByDateRangeAsync(from, to);

            var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);
            _summary = _trendCalculator.Calculate(transactions, accountCurrencies);

            var currencies = _summary.SpentByCurrencyAndMonth.Keys.Select(k => k.Currency).Distinct().OrderBy(c => c.ToString()).ToList();

            AvailableCurrencies.Clear();
            foreach (var currency in currencies)
                AvailableCurrencies.Add(currency);

            if (AvailableCurrencies.Count > 0 && !AvailableCurrencies.Contains(SelectedCurrency))
                // Assigning here fires OnSelectedCurrencyChanged below, which loads the rows for it —
                // avoids loading rows twice on the common "first load, nothing selected yet" path.
                SelectedCurrency = AvailableCurrencies[0];
            else
            {
                // SelectedCurrency already equals AvailableCurrencies[0] here in the common case --
                // both are CurrencyCode's default value (USD) on first load. The generated property
                // setter's equality check means the assignment above would be a no-op and never raise
                // PropertyChanged, so the Picker's SelectedItem binding -- synced once when
                // AvailableCurrencies was still empty, at BindingContext-set time -- never gets a
                // second chance to resync now that the list actually has a matching item. Found live
                // (same bug, same fix, as NetWorthViewModel.LoadAsync): the Picker rendered
                // permanently blank despite SelectedCurrency and the loaded rows both being correct.
                // Force the resync explicitly.
                OnPropertyChanged(nameof(SelectedCurrency));
                await LoadRowsForSelectedCurrencyAsync();
            }

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadRowsForSelectedCurrencyAsync()
    {
        // No repository call here (unlike NetWorthViewModel's per-currency fetch) -- the whole year's
        // summary was already computed once in LoadAsync above; switching currencies just re-slices it.
        await Task.CompletedTask;

        var monthEntries = _summary.SpentByCurrencyAndMonth
            .Where(kvp => kvp.Key.Currency == SelectedCurrency)
            .OrderBy(kvp => kvp.Key.Year)
            .ThenBy(kvp => kvp.Key.Month)
            .ToList();

        var maxAmount = monthEntries.Count > 0 ? monthEntries.Max(e => e.Value) : 0m;

        Rows.Clear();
        foreach (var (key, amount) in monthEntries)
        {
            var monthLabel = new DateTime(key.Year, key.Month, 1).ToString("Y", CultureInfo.CurrentCulture);
            Rows.Add(new MonthlyTrendBarItem(monthLabel, amount, ReportBarWidth.Compute(amount, maxAmount)));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
