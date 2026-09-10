using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class TermDepositsListViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _financialAccountRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<TermDepositListItem> TermDeposits { get; } = [];

    public bool IsEmpty => HasLoaded && TermDeposits.Count == 0;

    public TermDepositsListViewModel(IFinancialAccountRepository financialAccountRepository)
    {
        _financialAccountRepository = financialAccountRepository;
    }

    [RelayCommand]
    private async Task LoadTermDepositsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var accounts = await _financialAccountRepository.GetActiveAsync();

            TermDeposits.Clear();
            foreach (var account in accounts)
                if (account is TermDeposit termDeposit)
                    TermDeposits.Add(TermDepositListItem.FromDomain(termDeposit));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddTermDepositAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddTermDepositPage));
    }
}
