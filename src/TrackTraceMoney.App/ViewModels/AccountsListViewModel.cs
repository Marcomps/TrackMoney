using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AccountsListViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _accountRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<AccountListItem> Accounts { get; } = [];

    public bool IsEmpty => HasLoaded && Accounts.Count == 0;

    public AccountsListViewModel(IFinancialAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var accounts = await _accountRepository.GetActiveAsync();

            Accounts.Clear();
            foreach (var account in accounts)
                Accounts.Add(AccountListItem.FromDomain(account));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddAccountAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddAccountPage));
    }
}
