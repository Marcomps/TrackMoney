using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class CreditCardsListViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<CreditCardListItem> CreditCards { get; } = [];

    public bool IsEmpty => HasLoaded && CreditCards.Count == 0;

    public CreditCardsListViewModel(ICreditAccountRepository creditAccountRepository)
    {
        _creditAccountRepository = creditAccountRepository;
    }

    [RelayCommand]
    private async Task LoadCreditCardsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var creditAccounts = await _creditAccountRepository.GetActiveAsync();

            CreditCards.Clear();
            foreach (var creditAccount in creditAccounts)
            {
                if (creditAccount is CreditCard card)
                    CreditCards.Add(CreditCardListItem.FromDomain(card));
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
    private static async Task AddCreditCardAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddCreditCardPage));
    }
}
