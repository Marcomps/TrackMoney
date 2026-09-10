using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;

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

            // TermDeposit is a FinancialAccount subtype (shares this same repository/table per README
            // §22) but is deliberately excluded here: it isn't part of AccountListItem's Cash/Bank/Savings
            // switch (that would mean either folding it into AccountKind, which this slice explicitly
            // avoids, or AccountListItem.FromDomain throwing NotSupportedException for it) and it has its
            // own separate list screen (TermDepositsListPage) reached from ViewTermDepositsCommand below.
            Accounts.Clear();
            foreach (var account in accounts)
            {
                if (account is TermDeposit)
                    continue;

                Accounts.Add(AccountListItem.FromDomain(account));
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
    private static async Task AddAccountAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddAccountPage));
    }

    [RelayCommand]
    private static async Task ViewTermDepositsAsync()
    {
        await Shell.Current.GoToAsync(nameof(TermDepositsListPage));
    }
}
