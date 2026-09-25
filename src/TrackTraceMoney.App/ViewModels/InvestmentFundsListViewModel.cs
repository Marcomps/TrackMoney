using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class InvestmentFundsListViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _financialAccountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<InvestmentFundListItem> InvestmentFunds { get; } = [];

    public bool IsEmpty => HasLoaded && InvestmentFunds.Count == 0;

    public InvestmentFundsListViewModel(IFinancialAccountRepository financialAccountRepository, IFinancialInstitutionRepository institutionRepository)
    {
        _financialAccountRepository = financialAccountRepository;
        _institutionRepository = institutionRepository;
    }

    [RelayCommand]
    private async Task LoadInvestmentFundsAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var accounts = await _financialAccountRepository.GetActiveAsync();
            var institutionNames = (await _institutionRepository.GetAllAsync()).ToDictionary(i => i.Id, i => i.Name);

            InvestmentFunds.Clear();
            foreach (var account in accounts)
                if (account is InvestmentFund fund)
                {
                    var institutionName = fund.InstitutionId is { } institutionId && institutionNames.TryGetValue(institutionId, out var name)
                        ? name
                        : "?";
                    InvestmentFunds.Add(InvestmentFundListItem.FromDomain(fund, institutionName));
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

    [RelayCommand]
    private static async Task AddInvestmentFundAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddInvestmentFundPage));
    }

    [RelayCommand]
    private static async Task OpenInvestmentFundAsync(Guid investmentFundId) =>
        await Shell.Current.GoToAsync($"{nameof(InvestmentFundDetailPage)}?investmentFundId={investmentFundId}");
}
