using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class LoansListViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly IFinancialInstitutionRepository _institutionRepository;
    private readonly ICreditAccountLifecycleService _creditAccountLifecycleService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    /// <summary>Non-optional per the edit/delete slice spec §1.3/§3.3 -- same rationale as AccountsListViewModel.</summary>
    [ObservableProperty]
    private bool showInactive;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<LoanListItem> Loans { get; } = [];

    public bool IsEmpty => HasLoaded && Loans.Count == 0;

    public LoansListViewModel(
        ICreditAccountRepository creditAccountRepository,
        IFinancialInstitutionRepository institutionRepository,
        ICreditAccountLifecycleService creditAccountLifecycleService)
    {
        _creditAccountRepository = creditAccountRepository;
        _institutionRepository = institutionRepository;
        _creditAccountLifecycleService = creditAccountLifecycleService;
    }

    partial void OnShowInactiveChanged(bool value) => LoadLoansCommand.Execute(null);

    [RelayCommand]
    private async Task LoadLoansAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var creditAccounts = ShowInactive
                ? await _creditAccountRepository.GetAllAsync()
                : await _creditAccountRepository.GetActiveAsync();
            var institutionNames = (await _institutionRepository.GetAllAsync()).ToDictionary(i => i.Id, i => i.Name);

            Loans.Clear();
            foreach (var account in creditAccounts.OrderByDescending(a => a.IsActive).ThenBy(a => a.Name))
                if (account is Loan loan)
                {
                    var institutionName = loan.InstitutionId is { } institutionId && institutionNames.TryGetValue(institutionId, out var name)
                        ? name
                        : "?";
                    Loans.Add(LoanListItem.FromDomain(loan, institutionName));
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
    private static async Task AddLoanAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddLoanPage));
    }

    [RelayCommand]
    private static async Task EditLoanAsync(Guid loanId) =>
        await Shell.Current.GoToAsync($"{nameof(AddLoanPage)}?loanId={loanId}");

    /// <summary>Same "check-then-confirm" pattern as <c>AccountsListViewModel.DeleteAccountAsync</c>.</summary>
    [RelayCommand]
    private async Task DeleteLoanAsync(Guid loanId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        bool canDelete;
        try
        {
            canDelete = await _creditAccountLifecycleService.CanHardDeleteAsync(loanId);
        }
        finally
        {
            IsBusy = false;
        }

        if (!canDelete)
        {
            ErrorMessage = AppResources.LoansList_CannotDeleteMessage;
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.LoansList_DeleteConfirmTitle,
            AppResources.LoansList_DeleteConfirmMessage,
            AppResources.LoansList_DeleteConfirmAccept,
            AppResources.LoansList_DeleteConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _creditAccountLifecycleService.DeleteAsync(loanId);
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = AppResources.LoansList_CannotDeleteMessage;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadLoansAsync();
    }

    [RelayCommand]
    private async Task DeactivateLoanAsync(Guid loanId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.LoansList_DeactivateConfirmTitle,
            AppResources.LoansList_DeactivateConfirmMessage,
            AppResources.LoansList_DeactivateConfirmAccept,
            AppResources.LoansList_DeactivateConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            await _creditAccountLifecycleService.DeactivateAsync(loanId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.LoansList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadLoansAsync();
    }

    [RelayCommand]
    private async Task ReactivateLoanAsync(Guid loanId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await _creditAccountLifecycleService.ReactivateAsync(loanId);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.LoansList_Error;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadLoansAsync();
    }
}
