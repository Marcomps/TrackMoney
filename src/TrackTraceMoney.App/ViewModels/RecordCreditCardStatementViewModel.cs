using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Create/edit a single <see cref="CreditCardStatement"/> (README §15). Create mode recomputes the
/// current cycle independently rather than trusting anything passed through navigation. Edit mode only
/// ever touches the two user-entered amounts — cycle dates are immutable once recorded (see
/// <see cref="CreditCardStatement"/>'s remarks).
/// </summary>
[QueryProperty(nameof(CreditAccountId), "creditAccountId")]
[QueryProperty(nameof(StatementId), "statementId")]
public sealed partial class RecordCreditCardStatementViewModel : ObservableObject
{
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICreditCardStatementRepository _statementRepository;
    private readonly ITransactionRepository _transactionRepository;

    private CreditCardStatement? _existingStatement;

    [ObservableProperty]
    private Guid creditAccountId;

    /// <summary>
    /// Bound as a plain string, not <c>Guid?</c>, because MAUI Shell query parameters always arrive as
    /// strings and this codebase has no existing precedent for a nullable-Guid <c>[QueryProperty]</c>
    /// target to rely on — parsed defensively via <see cref="TryGetStatementId"/> instead of trusting
    /// an automatic string→Guid? conversion. Absent/unparseable means create mode; a valid Guid means
    /// edit mode.
    /// </summary>
    [ObservableProperty]
    private string? statementId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool isEditMode;

    [ObservableProperty]
    private DateOnly cycleStartDate;

    [ObservableProperty]
    private DateOnly cycleEndDate;

    [ObservableProperty]
    private decimal purchasesThisCycle;

    [ObservableProperty]
    private string minimumPaymentText = string.Empty;

    [ObservableProperty]
    private string payInFullAmountText = string.Empty;

    [ObservableProperty]
    private bool canSave = true;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public string Title => IsEditMode
        ? AppResources.RecordCreditCardStatement_EditTitle
        : AppResources.RecordCreditCardStatement_Title;

    public RecordCreditCardStatementViewModel(
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
        ErrorMessage = null;
        try
        {
            var creditAccount = await _creditAccountRepository.GetByIdAsync(CreditAccountId);
            if (creditAccount is not CreditCard card)
                return;

            var today = DateOnly.FromDateTime(DateTime.Today);

            if (TryGetStatementId(out var existingStatementId))
            {
                _existingStatement = await _statementRepository.GetByIdAsync(existingStatementId);
                if (_existingStatement is null)
                    return;

                IsEditMode = true;
                CycleStartDate = _existingStatement.CycleStartDate;
                CycleEndDate = _existingStatement.CycleEndDate;
                MinimumPaymentText = _existingStatement.MinimumPayment.ToString(CultureInfo.CurrentCulture);
                PayInFullAmountText = _existingStatement.PayInFullAmount.ToString(CultureInfo.CurrentCulture);
                CanSave = true;
            }
            else
            {
                IsEditMode = false;

                // Recompute independently — never trust a cycle range passed through navigation.
                var latestStatement = await _statementRepository.GetLatestForCardAsync(CreditAccountId);
                var cycleStart = latestStatement is null
                    ? DateOnly.FromDateTime(card.CreatedAtUtc.DateTime)
                    : latestStatement.CycleEndDate.AddDays(1);
                var cycleEnd = card.GetCutOffDateOnOrAfter(cycleStart);

                CycleStartDate = cycleStart;
                CycleEndDate = cycleEnd;
                CanSave = cycleEnd <= today;

                if (!CanSave)
                {
                    ErrorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        AppResources.RecordCreditCardStatement_ValidationCycleNotClosed,
                        cycleEnd);
                }
            }

            var purchasesEnd = CycleEndDate < today ? CycleEndDate : today;
            var purchases = await _transactionRepository.GetByDateRangeAndSpendAccountAsync(CycleStartDate, purchasesEnd, CreditAccountId);
            PurchasesThisCycle = purchases.Sum(t => t.Amount);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!CanSave)
        {
            ErrorMessage = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.RecordCreditCardStatement_ValidationCycleNotClosed,
                CycleEndDate);
            return;
        }

        if (!decimal.TryParse(MinimumPaymentText, NumberStyles.Number, CultureInfo.CurrentCulture, out var minimumPayment) || minimumPayment < 0)
        {
            ErrorMessage = AppResources.RecordCreditCardStatement_ValidationMinimumPaymentInvalid;
            return;
        }

        if (!decimal.TryParse(PayInFullAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var payInFullAmount) || payInFullAmount < 0)
        {
            ErrorMessage = AppResources.RecordCreditCardStatement_ValidationPayInFullAmountInvalid;
            return;
        }

        IsBusy = true;
        try
        {
            if (IsEditMode && _existingStatement is not null)
            {
                _existingStatement.UpdateAmounts(minimumPayment, payInFullAmount);
                await _statementRepository.SaveChangesAsync();
            }
            else
            {
                var statement = new CreditCardStatement(CreditAccountId, CycleStartDate, CycleEndDate, minimumPayment, payInFullAmount);
                await _statementRepository.AddAsync(statement);
                await _statementRepository.SaveChangesAsync();
            }

            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync() =>
        await Shell.Current.GoToAsync("..");

    private bool TryGetStatementId(out Guid statementIdValue) =>
        Guid.TryParse(StatementId, out statementIdValue);
}
