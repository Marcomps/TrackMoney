using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Net worth evolution history for a single currency at a time (README §24) — a plain chronological
/// list, no charting, mirroring <see cref="InvestmentFundDetailViewModel"/>'s valuation-history
/// precedent. Currencies are never blended: the currency Picker switches which currency's history is
/// shown, one at a time (CLAUDE.md hard constraint).
/// </summary>
public sealed partial class NetWorthViewModel : ObservableObject
{
    private readonly INetWorthSnapshotRepository _snapshotRepository;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private CurrencyCode selectedCurrency;

    public ObservableCollection<CurrencyCode> AvailableCurrencies { get; } = [];

    public ObservableCollection<NetWorthSnapshotListItem> Rows { get; } = [];

    public bool IsEmpty => HasLoaded && Rows.Count == 0;

    public NetWorthViewModel(INetWorthSnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository;
    }

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
            // Simplest correct source for "which currencies have history" — the distinct currencies
            // already present across every recorded NetWorthSnapshot row, not a separate lookup.
            var allSnapshots = await _snapshotRepository.GetAllAsync();
            var currencies = allSnapshots.Select(s => s.Currency).Distinct().OrderBy(c => c.ToString()).ToList();

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
                // second chance to resync now that the list actually has a matching item. Found live:
                // the Picker rendered permanently blank despite SelectedCurrency and the loaded rows
                // both being correct. Force the resync explicitly.
                OnPropertyChanged(nameof(SelectedCurrency));
                await LoadRowsForSelectedCurrencyAsync();
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

    partial void OnSelectedCurrencyChanged(CurrencyCode value) => LoadRowsForSelectedCurrencyCommand.Execute(null);

    [RelayCommand]
    private async Task LoadRowsForSelectedCurrencyAsync()
    {
        var snapshots = await _snapshotRepository.GetForCurrencyAsync(SelectedCurrency);

        Rows.Clear();
        foreach (var snapshot in snapshots)
            Rows.Add(NetWorthSnapshotListItem.FromDomain(snapshot));

        OnPropertyChanged(nameof(IsEmpty));
    }
}
