using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class LoansListViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<LoanListItem> Loans { get; } = [];

    public bool IsEmpty => HasLoaded && Loans.Count == 0;

    public LoansListViewModel(ICreditAccountRepository creditAccountRepository)
    {
        _creditAccountRepository = creditAccountRepository;
    }

    [RelayCommand]
    private async Task LoadLoansAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var creditAccounts = await _creditAccountRepository.GetActiveAsync();

            Loans.Clear();
            foreach (var account in creditAccounts)
                if (account is Loan loan)
                    Loans.Add(LoanListItem.FromDomain(loan));

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
}
