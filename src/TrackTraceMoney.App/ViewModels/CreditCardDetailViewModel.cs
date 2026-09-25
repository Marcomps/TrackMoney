using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// A single card's current billing cycle plus its statement history (README §15). The current cycle's
/// start/end dates are always recomputed from the card's configuration and the latest recorded
/// statement — never stored — so this stays correct even if a statement is edited or the card's
/// cut-off day were to change in a later slice.
/// </summary>
[QueryProperty(nameof(CreditAccountIdText), "creditAccountId")]
public sealed partial class CreditCardDetailViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICreditCardStatementRepository _statementRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICreditCardPurchasedVsPaidCalculator _purchasedVsPaidCalculator;
    private readonly ICreditCardHealthEvaluator _healthEvaluator;
    private readonly ICreditAccountLifecycleService _creditAccountLifecycleService;

    private CurrencyCode cardCurrency;

    [ObservableProperty]
    private Guid creditAccountId;

    /// <summary>
    /// The actual <c>[QueryProperty]</c> target. MAUI Shell's query-string navigation always hands a
    /// <c>string</c> to the receiving property and internally does a plain <c>Convert.ChangeType</c> --
    /// which throws <see cref="InvalidCastException"/> for a non-nullable <see cref="Guid"/> target
    /// (<see cref="Guid"/> has no <see cref="IConvertible"/> implementation), crashing the app on every
    /// single navigation into this page. Bound here as a string and parsed defensively instead, same
    /// idiom this codebase already uses for its optional Guid query properties (e.g.
    /// <c>RecordCreditCardStatementViewModel.StatementId</c>) -- the only difference is this one is
    /// always expected to be present, so an unparseable value is a genuine error, not "create mode".
    /// </summary>
    [ObservableProperty]
    private string? creditAccountIdText;

    partial void OnCreditAccountIdTextChanged(string? value)
    {
        if (Guid.TryParse(value, out var parsed))
            CreditAccountId = parsed;
    }

    private bool _isLoading;

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

    [ObservableProperty]
    private PurchasedVsPaidWindowOption? selectedPurchasedVsPaidWindow;

    [ObservableProperty]
    private decimal totalPurchased;

    [ObservableProperty]
    private decimal totalPaid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PurchasedVsPaidIndicatorMessage))]
    private decimal purchasedVsPaidDifference;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PurchasedVsPaidIndicatorEmoji))]
    [NotifyPropertyChangedFor(nameof(PurchasedVsPaidIndicatorMessage))]
    private bool isSpendingMoreThanPaying;

    [ObservableProperty]
    private bool isRecalculatingPurchasedVsPaid;

    [ObservableProperty]
    private string healthEmoji = string.Empty;

    [ObservableProperty]
    private string healthMessage = string.Empty;

    /// <summary>Drives the Deactivate-vs-Reactivate button (edit/delete slice spec §2.3/§1.3).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInactive))]
    private bool isActive = true;

    public bool IsInactive => !IsActive;

    [ObservableProperty]
    private string? actionErrorMessage;

    public ObservableCollection<CreditCardStatementListItem> Statements { get; } = [];

    public ObservableCollection<PurchasedVsPaidWindowOption> PurchasedVsPaidWindowOptions { get; } = [];

    public bool IsEmpty => HasLoaded && Statements.Count == 0;

    public bool CycleIsNotClosed => !CanRecordStatement;

    public string CycleNotClosedMessage =>
        string.Format(CultureInfo.CurrentCulture, AppResources.CreditCardDetail_CycleNotClosedMessage, CycleEndDate);

    public string PurchasedVsPaidIndicatorEmoji => PurchasedVsPaidIndicator.GetEmoji(IsSpendingMoreThanPaying);

    public string PurchasedVsPaidIndicatorMessage => IsSpendingMoreThanPaying
        ? AppResources.CreditCardDetail_PurchasedVsPaidOverMessage
        : string.Format(CultureInfo.CurrentCulture, AppResources.CreditCardDetail_PurchasedVsPaidUnderMessage, PurchasedVsPaidDifference);

    public CreditCardDetailViewModel(
        ICreditAccountRepository creditAccountRepository,
        ICreditCardStatementRepository statementRepository,
        ITransactionRepository transactionRepository,
        ICreditCardPurchasedVsPaidCalculator purchasedVsPaidCalculator,
        ICreditCardHealthEvaluator healthEvaluator,
        ICreditAccountLifecycleService creditAccountLifecycleService)
    {
        _creditAccountRepository = creditAccountRepository;
        _statementRepository = statementRepository;
        _transactionRepository = transactionRepository;
        _purchasedVsPaidCalculator = purchasedVsPaidCalculator;
        _healthEvaluator = healthEvaluator;
        _creditAccountLifecycleService = creditAccountLifecycleService;

        PurchasedVsPaidWindowOptions.Add(new PurchasedVsPaidWindowOption(PurchasedVsPaidWindow.Month, AppResources.CreditCardDetail_PurchasedVsPaidWindow_Month));
        PurchasedVsPaidWindowOptions.Add(new PurchasedVsPaidWindowOption(PurchasedVsPaidWindow.Cycle, AppResources.CreditCardDetail_PurchasedVsPaidWindow_Cycle));
        PurchasedVsPaidWindowOptions.Add(new PurchasedVsPaidWindowOption(PurchasedVsPaidWindow.ThreeMonths, AppResources.CreditCardDetail_PurchasedVsPaidWindow_ThreeMonths));
        PurchasedVsPaidWindowOptions.Add(new PurchasedVsPaidWindowOption(PurchasedVsPaidWindow.SixMonths, AppResources.CreditCardDetail_PurchasedVsPaidWindow_SixMonths));
        PurchasedVsPaidWindowOptions.Add(new PurchasedVsPaidWindowOption(PurchasedVsPaidWindow.Year, AppResources.CreditCardDetail_PurchasedVsPaidWindow_Year));

        selectedPurchasedVsPaidWindow = PurchasedVsPaidWindowOptions[0];
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var creditAccount = await _creditAccountRepository.GetByIdAsync(CreditAccountId);
            if (creditAccount is not CreditCard card)
                return;

            CardName = card.Name;
            cardCurrency = card.Currency;
            IsActive = card.IsActive;

            var today = DateOnly.FromDateTime(DateTime.Today);

            var latestStatement = await _statementRepository.GetLatestForCardAsync(CreditAccountId);

            var paymentsMadeThisCycle = latestStatement is null
                ? 0m
                : (await _transactionRepository.GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(
                    latestStatement.CycleEndDate.AddDays(1), today, CreditAccountId)).Sum(t => t.Amount);
            var assessment = _healthEvaluator.Evaluate(card, latestStatement, paymentsMadeThisCycle, today);

            HealthEmoji = CreditCardHealthIndicator.GetEmoji(assessment.Status);
            HealthMessage = assessment.Status switch
            {
                CreditCardHealthStatus.Green => AppResources.CreditCardHealth_GreenMessage,
                CreditCardHealthStatus.Yellow => string.Format(CultureInfo.CurrentCulture, AppResources.CreditCardHealth_YellowMessage, assessment.DueDate!.Value.DayNumber - today.DayNumber),
                CreditCardHealthStatus.Orange => AppResources.CreditCardHealth_OrangeMessage,
                CreditCardHealthStatus.Red => AppResources.CreditCardHealth_RedMessage,
                _ => AppResources.CreditCardHealth_NoStatementMessage
            };

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

            await RecalculatePurchasedVsPaidAsync();
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RecalculatePurchasedVsPaidAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var (windowStart, windowEnd) = SelectedPurchasedVsPaidWindow?.Window switch
        {
            PurchasedVsPaidWindow.Month => (new DateOnly(today.Year, today.Month, 1), today),
            PurchasedVsPaidWindow.Cycle => (CycleStartDate, CycleEndDate < today ? CycleEndDate : today),
            PurchasedVsPaidWindow.ThreeMonths => (today.AddMonths(-3), today),
            PurchasedVsPaidWindow.SixMonths => (today.AddMonths(-6), today),
            PurchasedVsPaidWindow.Year => (today.AddYears(-1), today),
            _ => (new DateOnly(today.Year, today.Month, 1), today)
        };

        IsRecalculatingPurchasedVsPaid = true;
        try
        {
            var purchases = await _transactionRepository.GetByDateRangeAndSpendAccountAsync(windowStart, windowEnd, CreditAccountId);
            var payments = await _transactionRepository.GetCreditCardPaymentsByDateRangeAndCreditAccountAsync(windowStart, windowEnd, CreditAccountId);

            var result = _purchasedVsPaidCalculator.Calculate(CreditAccountId, cardCurrency, windowStart, windowEnd, purchases, payments);

            TotalPurchased = result.TotalPurchased;
            TotalPaid = result.TotalPaid;
            PurchasedVsPaidDifference = result.Difference;
            IsSpendingMoreThanPaying = result.IsSpendingMoreThanPaying;
        }
        finally
        {
            IsRecalculatingPurchasedVsPaid = false;
        }
    }

    partial void OnSelectedPurchasedVsPaidWindowChanged(PurchasedVsPaidWindowOption? value) => RecalculatePurchasedVsPaidCommand.Execute(null);

    [RelayCommand]
    private async Task RecordStatementAsync() =>
        await Shell.Current.GoToAsync($"{nameof(RecordCreditCardStatementPage)}?creditAccountId={CreditAccountId}");

    [RelayCommand]
    private async Task OpenStatementAsync(Guid statementId) =>
        await Shell.Current.GoToAsync($"{nameof(RecordCreditCardStatementPage)}?creditAccountId={CreditAccountId}&statementId={statementId}");

    [RelayCommand]
    private async Task EditAsync() =>
        await Shell.Current.GoToAsync($"{nameof(AddCreditCardPage)}?creditAccountId={CreditAccountId}");

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        ActionErrorMessage = null;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.CreditCardDetail_DeactivateConfirmTitle,
            AppResources.CreditCardDetail_DeactivateConfirmMessage,
            AppResources.CreditCardDetail_DeactivateConfirmAccept,
            AppResources.CreditCardDetail_DeactivateConfirmCancel);

        if (!confirmed)
            return;

        try
        {
            await _creditAccountLifecycleService.DeactivateAsync(CreditAccountId);
        }
        catch (Exception)
        {
            ActionErrorMessage = AppResources.CreditCardDetail_ActionError;
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ReactivateAsync()
    {
        ActionErrorMessage = null;

        try
        {
            await _creditAccountLifecycleService.ReactivateAsync(CreditAccountId);
        }
        catch (Exception)
        {
            ActionErrorMessage = AppResources.CreditCardDetail_ActionError;
            return;
        }

        await LoadAsync();
    }

    /// <summary>
    /// Same "check the guard before offering the confirm dialog" pattern as
    /// <c>AccountsListViewModel.DeleteAccountAsync</c> — see its doc comment.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        ActionErrorMessage = null;

        if (!await _creditAccountLifecycleService.CanHardDeleteAsync(CreditAccountId))
        {
            ActionErrorMessage = AppResources.CreditCardDetail_CannotDeleteMessage;
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.CreditCardDetail_DeleteConfirmTitle,
            AppResources.CreditCardDetail_DeleteConfirmMessage,
            AppResources.CreditCardDetail_DeleteConfirmAccept,
            AppResources.CreditCardDetail_DeleteConfirmCancel);

        if (!confirmed)
            return;

        try
        {
            await _creditAccountLifecycleService.DeleteAsync(CreditAccountId);
        }
        catch (InvalidOperationException)
        {
            ActionErrorMessage = AppResources.CreditCardDetail_CannotDeleteMessage;
            return;
        }

        await Shell.Current.GoToAsync("..");
    }
}
