using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Accounts;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AccountsListViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly IFinancialAccountLifecycleService _accountLifecycleService;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    /// <summary>
    /// Non-optional per the edit/delete slice spec §1.3: without this toggle, Deactivate is a one-way
    /// trap -- this ViewModel would otherwise only ever call <c>GetActiveAsync</c>, so a deactivated
    /// account would vanish from the user's own view with no UI path back.
    /// </summary>
    [ObservableProperty]
    private bool showInactive;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<AccountListItem> Accounts { get; } = [];

    public bool IsEmpty => HasLoaded && Accounts.Count == 0;

    public AccountsListViewModel(IFinancialAccountRepository accountRepository, IFinancialAccountLifecycleService accountLifecycleService)
    {
        _accountRepository = accountRepository;
        _accountLifecycleService = accountLifecycleService;
    }

    partial void OnShowInactiveChanged(bool value) => LoadAccountsCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var accounts = ShowInactive
                ? await _accountRepository.GetAllAsync()
                : await _accountRepository.GetActiveAsync();

            // TermDeposit and InvestmentFund are FinancialAccount subtypes (share this same repository/
            // table per README §22/§23) but are deliberately excluded here: neither is part of
            // AccountListItem's Cash/Bank/Savings switch (that would mean either folding them into
            // AccountKind, which this slice explicitly avoids, or AccountListItem.FromDomain throwing
            // NotSupportedException for them); each is shown in its own section of the Accounts tab,
            // backed by TermDepositsListViewModel/InvestmentFundsListViewModel (see AccountsListPage).
            Accounts.Clear();
            foreach (var account in accounts.OrderByDescending(a => a.IsActive).ThenBy(a => a.Name))
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
            _isLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddAccountAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddAccountPage));
    }

    [RelayCommand]
    private static async Task EditAccountAsync(Guid accountId) =>
        await Shell.Current.GoToAsync($"{nameof(AddAccountPage)}?accountId={accountId}");

    /// <summary>
    /// Checks <c>CanHardDeleteAsync</c> BEFORE showing the destructive confirm dialog -- if it's false,
    /// shows an explanatory message instead of a confirm dialog the user would only have rejected
    /// after the fact (edit/delete slice spec §1.2/§0: not just a disabled button with no explanation).
    /// The confirm-then-delete call re-checks the guard itself (never trusting this cached "yes") --
    /// see <see cref="FinancialAccountLifecycleService.DeleteAsync"/>.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAccountAsync(Guid accountId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        bool canDelete;
        try
        {
            canDelete = await _accountLifecycleService.CanHardDeleteAsync(accountId);
        }
        finally
        {
            IsBusy = false;
        }

        if (!canDelete)
        {
            ErrorMessage = AppResources.AccountsList_CannotDeleteMessage;
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.AccountsList_DeleteConfirmTitle,
            AppResources.AccountsList_DeleteConfirmMessage,
            AppResources.AccountsList_DeleteConfirmAccept,
            AppResources.AccountsList_DeleteConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _accountLifecycleService.DeleteAsync(accountId);
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = AppResources.AccountsList_CannotDeleteMessage;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAccountsAsync();
    }

    [RelayCommand]
    private async Task DeactivateAccountAsync(Guid accountId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.AccountsList_DeactivateConfirmTitle,
            AppResources.AccountsList_DeactivateConfirmMessage,
            AppResources.AccountsList_DeactivateConfirmAccept,
            AppResources.AccountsList_DeactivateConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _accountLifecycleService.DeactivateAsync(accountId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.AccountsList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAccountsAsync();
    }

    [RelayCommand]
    private async Task ReactivateAccountAsync(Guid accountId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await _accountLifecycleService.ReactivateAsync(accountId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.AccountsList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAccountsAsync();
    }
}
