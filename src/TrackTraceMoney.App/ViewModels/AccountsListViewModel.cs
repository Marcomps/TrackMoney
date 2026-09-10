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

            // TermDeposit and InvestmentFund are FinancialAccount subtypes (share this same repository/
            // table per README §22/§23) but are deliberately excluded here: neither is part of
            // AccountListItem's Cash/Bank/Savings switch (that would mean either folding them into
            // AccountKind, which this slice explicitly avoids, or AccountListItem.FromDomain throwing
            // NotSupportedException for them) and each has its own separate list screen
            // (TermDepositsListPage/InvestmentFundsListPage) reached from the ViewTermDepositsCommand/
            // ViewInvestmentFundsCommand below.
            Accounts.Clear();
            foreach (var account in accounts)
            {
                if (account is TermDeposit or InvestmentFund)
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

    [RelayCommand]
    private static async Task ViewInvestmentFundsAsync()
    {
        await Shell.Current.GoToAsync(nameof(InvestmentFundsListPage));
    }
}
