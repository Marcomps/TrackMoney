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

            var cards = creditAccounts.OfType<CreditCard>().ToList();
            var cardIds = cards.Select(c => c.Id).ToList();

            // Batched instead of two per-card queries inside the loop below (finding 10 of the Phase 2
            // checkpoint review): one query for every card's latest statement, and one query for every
            // card's payments up to today. Each card's own lower bound (its latest statement's
            // CycleEndDate) still differs, so that half of the filter is applied per card below, against
            // the already small, id- and upper-bound-filtered payments list.
            var latestStatementsByCard = await _statementRepository.GetLatestForCardsAsync(cardIds);
            var paymentsByCard = (await _transactionRepository.GetCreditCardPaymentsUpToDateForCreditAccountsAsync(today, cardIds))
                .GroupBy(p => p.CreditAccountId)
                .ToDictionary(g => g.Key, g => g.ToList());

            CreditCards.Clear();
            foreach (var card in cards)
            {
                latestStatementsByCard.TryGetValue(card.Id, out var latestStatement);
                decimal paymentsMadeThisCycle = 0m;
                if (latestStatement is not null && paymentsByCard.TryGetValue(card.Id, out var payments))
                    paymentsMadeThisCycle = payments.Where(p => p.Date > latestStatement.CycleEndDate).Sum(p => p.Amount);

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
