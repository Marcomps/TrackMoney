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

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<InvestmentFundListItem> InvestmentFunds { get; } = [];

    public bool IsEmpty => HasLoaded && InvestmentFunds.Count == 0;

    public InvestmentFundsListViewModel(IFinancialAccountRepository financialAccountRepository)
    {
        _financialAccountRepository = financialAccountRepository;
    }

    [RelayCommand]
    private async Task LoadInvestmentFundsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var accounts = await _financialAccountRepository.GetActiveAsync();

            InvestmentFunds.Clear();
            foreach (var account in accounts)
                if (account is InvestmentFund fund)
                    InvestmentFunds.Add(InvestmentFundListItem.FromDomain(fund));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
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
