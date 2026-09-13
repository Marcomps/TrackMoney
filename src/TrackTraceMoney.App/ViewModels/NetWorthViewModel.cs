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
        if (IsBusy)
            return;

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
                await LoadRowsForSelectedCurrencyAsync();

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
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
