using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// README §20's Snowball ("Bola de Nieve") debt payoff planner — purely advisory: no command on this
/// screen ever posts a CreditCardPayment/LoanPayment or calls CreditAccount.RegisterPayment, and
/// nothing computed here is persisted. Debts are partitioned by currency before ranking (never
/// compared/ranked across currencies), mirroring DashboardViewModel's per-currency grouping. A card
/// with no recorded CreditCardStatement yet is excluded from the ranked plan entirely (an assumed $0
/// minimum would misrepresent it as an attractive target), and surfaced separately with a link to
/// record a statement.
/// </summary>
public sealed partial class SnowballPlanViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICreditCardStatementRepository _statementRepository;
    private readonly ISnowballPlanner _snowballPlanner;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<SnowballCurrencyGroupItem> Groups { get; } = [];

    public ObservableCollection<ExcludedCardListItem> ExcludedCards { get; } = [];

    public bool IsEmpty => HasLoaded && Groups.Count == 0 && ExcludedCards.Count == 0;

    public bool HasExcludedCards => ExcludedCards.Count > 0;

    public SnowballPlanViewModel(
        ICreditAccountRepository creditAccountRepository,
        ICreditCardStatementRepository statementRepository,
        ISnowballPlanner snowballPlanner)
    {
        _creditAccountRepository = creditAccountRepository;
        _statementRepository = statementRepository;
        _snowballPlanner = snowballPlanner;
    }

    [RelayCommand]
    private async Task LoadSnowballPlanAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var creditAccounts = await _creditAccountRepository.GetActiveAsync();

            var debtsByCurrency = new Dictionary<CurrencyCode, List<SnowballDebtInput>>();
            var excludedCards = new List<ExcludedCardListItem>();

            foreach (var creditAccount in creditAccounts)
            {
                switch (creditAccount)
                {
                    case Loan loan:
                        AddDebt(debtsByCurrency, loan.Currency, new SnowballDebtInput(
                            loan.Id, loan.Name, loan.AmountOwed, loan.RequiredPayment, loan.CreatedAtUtc));
                        break;

                    case CreditCard card:
                        var latestStatement = await _statementRepository.GetLatestForCardAsync(card.Id);
                        if (latestStatement is null)
                        {
                            excludedCards.Add(new ExcludedCardListItem(card.Id, card.Name, card.AmountOwed));
                            break;
                        }

                        AddDebt(debtsByCurrency, card.Currency, new SnowballDebtInput(
                            card.Id, card.Name, card.AmountOwed, latestStatement.MinimumPayment, card.CreatedAtUtc));
                        break;
                }
            }

            Groups.Clear();
            foreach (var (currency, debts) in debtsByCurrency)
                Groups.Add(new SnowballCurrencyGroupItem(_snowballPlanner, currency, debts));

            ExcludedCards.Clear();
            foreach (var excludedCard in excludedCards)
                ExcludedCards.Add(excludedCard);

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasExcludedCards));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void AddDebt(Dictionary<CurrencyCode, List<SnowballDebtInput>> debtsByCurrency, CurrencyCode currency, SnowballDebtInput debt)
    {
        if (!debtsByCurrency.TryGetValue(currency, out var debts))
        {
            debts = [];
            debtsByCurrency[currency] = debts;
        }

        debts.Add(debt);
    }

    [RelayCommand]
    private static async Task RecordStatementForExcludedCardAsync(Guid creditAccountId) =>
        await Shell.Current.GoToAsync($"{nameof(RecordCreditCardStatementPage)}?creditAccountId={creditAccountId}");
}
