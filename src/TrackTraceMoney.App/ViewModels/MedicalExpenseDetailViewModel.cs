using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.MedicalExpenses;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// A single medical expense's detail (README §27/§28, Phase 3 slice 7) — the original transaction's
/// amount/date/category plus its linked <see cref="MedicalExpenseDetail"/> (provider/gross/covered/
/// status), reached by tapping a badged row in History. Only ever navigated to for a transaction whose
/// badge is Pending/Reimbursed/Rejected (see <c>HistoryPage.xaml</c>), so a missing detail here means
/// something upstream navigated incorrectly rather than a normal empty state.
/// </summary>
[QueryProperty(nameof(TransactionId), "transactionId")]
public sealed partial class MedicalExpenseDetailViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMedicalExpenseDetailRepository _medicalExpenseDetailRepository;
    private readonly ITransactionEntryService _transactionEntryService;

    [ObservableProperty]
    private Guid transactionId;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private decimal originalAmount;

    [ObservableProperty]
    private DateOnly originalDate;

    [ObservableProperty]
    private string categoryName = string.Empty;

    [ObservableProperty]
    private string? insuranceProvider;

    [ObservableProperty]
    private decimal? grossAmount;

    [ObservableProperty]
    private decimal? coveredAmount;

    [ObservableProperty]
    private string statusLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending))]
    private MedicalReimbursementStatus status;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Only Pending shows the "Record reimbursement"/"Mark rejected" actions (README §27/§28).</summary>
    public bool IsPending => Status == MedicalReimbursementStatus.Pending;

    public MedicalExpenseDetailViewModel(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IMedicalExpenseDetailRepository medicalExpenseDetailRepository,
        ITransactionEntryService transactionEntryService)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _medicalExpenseDetailRepository = medicalExpenseDetailRepository;
        _transactionEntryService = transactionEntryService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            ErrorMessage = null;

            var transaction = await _transactionRepository.GetByIdAsync(TransactionId);
            var detail = await _medicalExpenseDetailRepository.GetForTransactionAsync(TransactionId);
            if (transaction is null || detail is null)
                return;

            OriginalAmount = transaction.Amount;
            OriginalDate = transaction.Date;

            CategoryName = string.Empty;
            if (transaction.SpendCategoryId is { } categoryId)
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category is not null)
                    CategoryName = SystemCategoryKeyToLabelConverter.GetDisplayName(category);
            }

            InsuranceProvider = detail.InsuranceProvider;
            GrossAmount = detail.GrossAmount;
            CoveredAmount = detail.InsuranceCoveredAmount;
            Status = detail.Status;
            StatusLabel = MedicalReimbursementStatusToLabelConverter.GetDisplayName(detail.Status);

            HasLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Navigates to <see cref="AddTransactionPage"/> with the linked transaction id as a query param,
    /// letting <c>AddTransactionViewModel.LoadOptionsAsync</c> pre-select the Reimbursement type and
    /// this expense in its pending-expense picker (see that method's doc comment).
    /// </summary>
    [RelayCommand]
    private async Task RecordReimbursementAsync() =>
        await Shell.Current.GoToAsync($"{nameof(AddTransactionPage)}?linkedTransactionId={TransactionId}");

    [RelayCommand]
    private async Task MarkRejectedAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.MedicalExpenseDetail_ConfirmRejectTitle,
            AppResources.MedicalExpenseDetail_ConfirmRejectMessage,
            AppResources.MedicalExpenseDetail_ConfirmRejectAccept,
            AppResources.MedicalExpenseDetail_ConfirmRejectCancel);

        if (!confirmed)
            return;

        try
        {
            await _transactionEntryService.RejectMedicalReimbursementAsync(TransactionId);
        }
        catch (InvalidOperationException)
        {
            // LoadAsync manages its own IsBusy guard/flag — do not wrap it in one here, or the reload
            // below would be skipped as a no-op re-entrant call.
            ErrorMessage = AppResources.AddTransaction_ValidationServiceError;
            return;
        }

        await LoadAsync();
    }
}
