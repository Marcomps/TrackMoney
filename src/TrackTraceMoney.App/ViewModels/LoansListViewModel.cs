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
    private readonly IFinancialInstitutionRepository _institutionRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<LoanListItem> Loans { get; } = [];

    public bool IsEmpty => HasLoaded && Loans.Count == 0;

    public LoansListViewModel(ICreditAccountRepository creditAccountRepository, IFinancialInstitutionRepository institutionRepository)
    {
        _creditAccountRepository = creditAccountRepository;
        _institutionRepository = institutionRepository;
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
            var institutionNames = (await _institutionRepository.GetAllAsync()).ToDictionary(i => i.Id, i => i.Name);

            Loans.Clear();
            foreach (var account in creditAccounts)
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
}
