using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// A single card's current billing cycle plus its statement history (README §15). The current cycle's
/// start/end dates are always recomputed from the card's configuration and the latest recorded
/// statement — never stored — so this stays correct even if a statement is edited or the card's
/// cut-off day were to change in a later slice.
/// </summary>
[QueryProperty(nameof(CreditAccountId), "creditAccountId")]
public sealed partial class CreditCardDetailViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICreditCardStatementRepository _statementRepository;
    private readonly ITransactionRepository _transactionRepository;

    [ObservableProperty]
    private Guid creditAccountId;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private string cardName = string.Empty;

    [ObservableProperty]
    private DateOnly cycleStartDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CycleNotClosedMessage))]
    private DateOnly cycleEndDate;

    [ObservableProperty]
    private decimal purchasesSoFarThisCycle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CycleIsNotClosed))]
    private bool canRecordStatement;

    public ObservableCollection<CreditCardStatementListItem> Statements { get; } = [];

    public bool IsEmpty => HasLoaded && Statements.Count == 0;

    public bool CycleIsNotClosed => !CanRecordStatement;

    public string CycleNotClosedMessage =>
        string.Format(CultureInfo.CurrentCulture, AppResources.CreditCardDetail_CycleNotClosedMessage, CycleEndDate);

    public CreditCardDetailViewModel(
        ICreditAccountRepository creditAccountRepository,
        ICreditCardStatementRepository statementRepository,
        ITransactionRepository transactionRepository)
    {
        _creditAccountRepository = creditAccountRepository;
        _statementRepository = statementRepository;
        _transactionRepository = transactionRepository;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var creditAccount = await _creditAccountRepository.GetByIdAsync(CreditAccountId);
            if (creditAccount is not CreditCard card)
                return;

            CardName = card.Name;

            var today = DateOnly.FromDateTime(DateTime.Today);

            var latestStatement = await _statementRepository.GetLatestForCardAsync(CreditAccountId);
            var cycleStart = latestStatement is null
                ? DateOnly.FromDateTime(card.CreatedAtUtc.DateTime)
                : latestStatement.CycleEndDate.AddDays(1);
            var cycleEnd = card.GetCutOffDateOnOrAfter(cycleStart);

            CycleStartDate = cycleStart;
            CycleEndDate = cycleEnd;
            CanRecordStatement = cycleEnd <= today;

            var purchasesEnd = cycleEnd < today ? cycleEnd : today;
            var purchases = await _transactionRepository.GetByDateRangeAndSpendAccountAsync(cycleStart, purchasesEnd, CreditAccountId);
            PurchasesSoFarThisCycle = purchases.Sum(t => t.Amount);

            var statements = await _statementRepository.GetForCardAsync(CreditAccountId);

            Statements.Clear();
            foreach (var statement in statements)
            {
                var statementPurchases = await _transactionRepository.GetByDateRangeAndSpendAccountAsync(
                    statement.CycleStartDate, statement.CycleEndDate, CreditAccountId);
                var dueDate = card.GetPaymentDueDateForCycleEndingOn(statement.CycleEndDate);

                Statements.Add(CreditCardStatementListItem.FromDomain(statement, dueDate, statementPurchases.Sum(t => t.Amount)));
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
    private async Task RecordStatementAsync() =>
        await Shell.Current.GoToAsync($"{nameof(RecordCreditCardStatementPage)}?creditAccountId={CreditAccountId}");

    [RelayCommand]
    private async Task OpenStatementAsync(Guid statementId) =>
        await Shell.Current.GoToAsync($"{nameof(RecordCreditCardStatementPage)}?creditAccountId={CreditAccountId}&statementId={statementId}");
}
