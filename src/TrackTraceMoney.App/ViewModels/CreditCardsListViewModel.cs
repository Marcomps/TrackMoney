using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class CreditCardsListViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICreditCardStatementRepository _statementRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICreditCardHealthEvaluator _healthEvaluator;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<CreditCardListItem> CreditCards { get; } = [];

    public bool IsEmpty => HasLoaded && CreditCards.Count == 0;

    public CreditCardsListViewModel(
        ICreditAccountRepository creditAccountRepository,
        ICreditCardStatementRepository statementRepository,
        ITransactionRepository transactionRepository,
        ICreditCardHealthEvaluator healthEvaluator)
    {
        _creditAccountRepository = creditAccountRepository;
        _statementRepository = statementRepository;
        _transactionRepository = transactionRepository;
        _healthEvaluator = healthEvaluator;
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
            var today = DateOnly.FromDateTime(DateTime.Today);

            CreditCards.Clear();
            foreach (var creditAccount in creditAccounts)
            {
                if (creditAccount is not CreditCard card)
                    continue;

                var latestStatement = await _statementRepository.GetLatestForCardAsync(card.Id);
                var paymentsMadeThisCycle = latestStatement is null
                    ? 0m
                    : (await _transactionRepository.GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(
                        latestStatement.CycleEndDate.AddDays(1), today, card.Id)).Sum(t => t.Amount);
                var assessment = _healthEvaluator.Evaluate(card, latestStatement, paymentsMadeThisCycle, today);

                CreditCards.Add(CreditCardListItem.FromDomain(card, assessment.Status));
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

    [RelayCommand]
    private static async Task OpenCreditCardAsync(Guid creditAccountId) =>
        await Shell.Current.GoToAsync($"{nameof(CreditCardDetailPage)}?creditAccountId={creditAccountId}");

    [RelayCommand]
    private static async Task ViewLoansAsync()
    {
        await Shell.Current.GoToAsync(nameof(LoansListPage));
    }

    [RelayCommand]
    private static async Task ViewSnowballPlanAsync()
    {
        await Shell.Current.GoToAsync(nameof(SnowballPlanPage));
    }
}
