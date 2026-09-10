using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// A single investment fund's current figures plus its valuation history (README §23). Unlike
/// <see cref="CreditCardDetailViewModel"/>'s statement recording, there is no "cycle closed" gate here —
/// a valuation can be recorded at any time, so RecordValuationCommand is always enabled.
/// </summary>
[QueryProperty(nameof(InvestmentFundId), "investmentFundId")]
public sealed partial class InvestmentFundDetailViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _financialAccountRepository;
    private readonly IInvestmentValuationRepository _valuationRepository;

    [ObservableProperty]
    private Guid investmentFundId;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string institution = string.Empty;

    [ObservableProperty]
    private DateOnly investmentDate;

    [ObservableProperty]
    private decimal balance;

    [ObservableProperty]
    private decimal contributions;

    [ObservableProperty]
    private decimal withdrawals;

    [ObservableProperty]
    private decimal fees;

    [ObservableProperty]
    private decimal gain;

    [ObservableProperty]
    private decimal? returnPercentage;

    public ObservableCollection<InvestmentValuationListItem> Valuations { get; } = [];

    public bool IsEmpty => HasLoaded && Valuations.Count == 0;

    public InvestmentFundDetailViewModel(
        IFinancialAccountRepository financialAccountRepository,
        IInvestmentValuationRepository valuationRepository)
    {
        _financialAccountRepository = financialAccountRepository;
        _valuationRepository = valuationRepository;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var account = await _financialAccountRepository.GetByIdAsync(InvestmentFundId);
            if (account is not InvestmentFund fund)
                return;

            Name = fund.Name;
            Institution = fund.Institution;
            InvestmentDate = fund.InvestmentDate;
            Balance = fund.Balance;
            Contributions = fund.Contributions;
            Withdrawals = fund.Withdrawals;
            Fees = fund.Fees;
            Gain = fund.Gain;
            ReturnPercentage = fund.ReturnPercentage;

            var valuations = await _valuationRepository.GetForFundAsync(InvestmentFundId);

            Valuations.Clear();
            foreach (var valuation in valuations)
                Valuations.Add(InvestmentValuationListItem.FromDomain(valuation));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RecordValuationAsync() =>
        await Shell.Current.GoToAsync($"{nameof(RecordInvestmentValuationPage)}?investmentFundId={InvestmentFundId}");

    [RelayCommand]
    private async Task OpenValuationAsync(Guid valuationId) =>
        await Shell.Current.GoToAsync($"{nameof(RecordInvestmentValuationPage)}?investmentFundId={InvestmentFundId}&valuationId={valuationId}");
}
