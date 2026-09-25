using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Transactions;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Read-only detail for a single <see cref="Expense"/>/<see cref="Income"/>/<see cref="Transfer"/>
/// (edit/delete slice spec §4/§5) — the only three transaction types this slice supports editing/
/// deleting (§4.2/§4.3), reached only from <c>HistoryViewModel.OpenTransactionDetailCommand</c>.
/// Edit navigates to <see cref="AddTransactionPage"/>'s edit mode; Delete calls the matching
/// <c>ReverseXAsync</c> directly (no re-post) and navigates back.
/// </summary>
[QueryProperty(nameof(TransactionIdText), "transactionId")]
public sealed partial class TransactionDetailViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IMedicalExpenseDetailRepository _medicalExpenseDetailRepository;
    private readonly ITransactionEntryService _transactionEntryService;

    [ObservableProperty]
    private Guid transactionId;

    /// <summary>Same defensive-parse idiom as <c>MedicalExpenseDetailViewModel.TransactionIdText</c>.</summary>
    [ObservableProperty]
    private string? transactionIdText;

    partial void OnTransactionIdTextChanged(string? value)
    {
        if (Guid.TryParse(value, out var parsed))
            TransactionId = parsed;
    }

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpense))]
    [NotifyPropertyChangedFor(nameof(IsIncome))]
    [NotifyPropertyChangedFor(nameof(IsTransfer))]
    private TransactionType type;

    [ObservableProperty]
    private DateOnly date;

    [ObservableProperty]
    private decimal amount;

    [ObservableProperty]
    private string accountLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategoryName))]
    private string? categoryName;

    [ObservableProperty]
    private string? payerName;

    [ObservableProperty]
    private string? beneficiaryName;

    [ObservableProperty]
    private string? personName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    private string? description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotes))]
    private string? notes;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// True when this Expense has a linked <see cref="Domain.MedicalExpenses.MedicalExpenseDetail"/>
    /// (found via a defensive extra query, App-layer only) -- reachable here despite
    /// <c>HistoryViewModel.OpenTransactionDetailCommand</c>'s medical-priority routing whenever the
    /// linked detail's status is None/PaidDirectly (no badge, see <c>HistoryEntryItem</c>'s doc comment),
    /// which still leaves a real detail row pointing at this transaction id. Reversing/reposting or
    /// deleting this transaction via the <c>ReverseXAsync</c>-based flows would orphan that detail
    /// row (it has no cascade/cleanup) -- this slice does not add that cleanup (Application-layer work,
    /// out of scope), so Edit/Delete are hidden instead, with an explanation, rather than silently
    /// producing a dangling reference.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditOrDelete))]
    private bool hasLinkedMedicalDetail;

    public bool CanEditOrDelete => !HasLinkedMedicalDetail;

    /// <summary>
    /// Explicit bool properties for XAML <c>IsVisible</c> bindings -- <c>IsVisible</c> is a
    /// <see cref="bool"/> bindable property and does not implicitly coerce a bound <see cref="string"/>,
    /// unlike <c>Text</c>.
    /// </summary>
    public bool HasCategoryName => !string.IsNullOrEmpty(CategoryName);

    public bool HasDescription => !string.IsNullOrEmpty(Description);

    public bool HasNotes => !string.IsNullOrEmpty(Notes);

    public bool IsExpense => Type == TransactionType.Expense;

    public bool IsIncome => Type == TransactionType.Income;

    public bool IsTransfer => Type == TransactionType.Transfer;

    public TransactionDetailViewModel(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        ICategoryRepository categoryRepository,
        IPersonRepository personRepository,
        IMedicalExpenseDetailRepository medicalExpenseDetailRepository,
        ITransactionEntryService transactionEntryService)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _medicalExpenseDetailRepository = medicalExpenseDetailRepository;
        _transactionEntryService = transactionEntryService;
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
            ErrorMessage = null;

            var transaction = await _transactionRepository.GetByIdAsync(TransactionId);
            if (transaction is not (Expense or Income or Transfer))
            {
                ErrorMessage = AppResources.TransactionDetail_NotFound;
                return;
            }

            var accounts = await _accountRepository.GetAllAsync();
            var creditAccounts = await _creditAccountRepository.GetAllAsync();
            var accountNames = AccountNameMapBuilder.Build(accounts, creditAccounts);
            var categories = await _categoryRepository.GetAllAsync();
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            var people = await _personRepository.GetAllAsync();
            var personNames = people.ToDictionary(p => p.Id, p => p.Name);

            Date = transaction.Date;
            Amount = transaction.Amount;
            Description = transaction.Description;
            Notes = transaction.Notes;
            AccountLabel = TransactionLabelFormatter.BuildLabel(transaction, accountNames, categoryNames);

            CategoryName = null;
            PayerName = null;
            BeneficiaryName = null;
            PersonName = null;
            HasLinkedMedicalDetail = false;

            switch (transaction)
            {
                case Expense expense:
                    Type = TransactionType.Expense;
                    CategoryName = NameOf(categoryNames, expense.CategoryId);
                    PayerName = expense.PayerPersonId is { } payerId ? NameOf(personNames, payerId) : null;
                    BeneficiaryName = expense.BeneficiaryPersonId is { } beneficiaryId ? NameOf(personNames, beneficiaryId) : null;
                    HasLinkedMedicalDetail = await _medicalExpenseDetailRepository.GetForTransactionAsync(TransactionId) is not null;
                    if (HasLinkedMedicalDetail)
                        ErrorMessage = AppResources.TransactionDetail_MedicalLinkedMessage;
                    break;
                case Income income:
                    Type = TransactionType.Income;
                    CategoryName = NameOf(categoryNames, income.CategoryId);
                    PersonName = income.PersonId is { } personId ? NameOf(personNames, personId) : null;
                    break;
                case Transfer:
                    Type = TransactionType.Transfer;
                    break;
            }

            HasLoaded = true;
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }
    }

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid id) =>
        names.TryGetValue(id, out var name) ? name : null;

    [RelayCommand]
    private async Task EditAsync()
    {
        if (HasLinkedMedicalDetail)
        {
            ErrorMessage = AppResources.TransactionDetail_MedicalLinkedMessage;
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(AddTransactionPage)}?editingTransactionId={TransactionId}");
    }

    /// <summary>
    /// Standalone delete (edit/delete slice spec §4.1's "simpler" half): confirm dialog → the matching
    /// <c>ReverseXAsync</c> (fetched fresh inside the service, never trusting this screen's in-memory
    /// state) → navigate back. No re-post — that's Edit's job.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        ErrorMessage = null;

        if (HasLinkedMedicalDetail)
        {
            ErrorMessage = AppResources.TransactionDetail_MedicalLinkedMessage;
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.TransactionDetail_DeleteConfirmTitle,
            AppResources.TransactionDetail_DeleteConfirmMessage,
            AppResources.TransactionDetail_DeleteConfirmAccept,
            AppResources.TransactionDetail_DeleteConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            switch (Type)
            {
                case TransactionType.Expense:
                    await _transactionEntryService.ReverseExpenseAsync(TransactionId);
                    break;
                case TransactionType.Income:
                    await _transactionEntryService.ReverseIncomeAsync(TransactionId);
                    break;
                case TransactionType.Transfer:
                    await _transactionEntryService.ReverseTransferAsync(TransactionId);
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = AppResources.TransactionDetail_DeleteError;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await Shell.Current.GoToAsync("..");
    }
}
