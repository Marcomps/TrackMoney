using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.App.ViewModels;

[QueryProperty(nameof(RecurringExpenseIdText), "id")]
public sealed partial class AddRecurringExpenseViewModel : ObservableObject
{
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;

    /// <summary>
    /// Set when opened from the list's Edit button (<c>?id=</c>): the form then prefills from the existing
    /// definition and Save updates it instead of adding a new one. String-typed query target, parsed
    /// defensively — Shell can't convert a query string to <see cref="Guid"/> (same idiom as
    /// <c>AddBudgetViewModel.BudgetIdText</c>).
    /// </summary>
    [ObservableProperty]
    private string? recurringExpenseIdText;

    partial void OnRecurringExpenseIdTextChanged(string? value) =>
        EditingId = Guid.TryParse(value, out var parsed) ? parsed : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private Guid? editingId;

    /// <summary>The start date anchors the schedule, so it's locked once an occurrence has been confirmed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStartDateLocked))]
    private bool isStartDateEnabled = true;

    private Guid? _prefilledId;

    public bool IsEditMode => EditingId is not null;

    public bool IsStartDateLocked => !IsStartDateEnabled;

    public string PageTitle => IsEditMode ? AppResources.AddRecurringExpense_EditTitle : AppResources.AddRecurringExpense_Title;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedCategory;

    [ObservableProperty]
    private NamedOption? selectedAccount;

    [ObservableProperty]
    private RecurringExpenseFrequency selectedFrequency = RecurringExpenseFrequency.Monthly;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private bool hasEndDate;

    [ObservableProperty]
    private DateTime endDate = DateTime.Today;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<RecurringExpenseFrequency> AvailableFrequencies { get; } = Enum.GetValues<RecurringExpenseFrequency>();

    public ObservableCollection<NamedOption> Categories { get; } = [];

    /// <summary>
    /// Combined FinancialAccount + credit card picker for this recurring expense's payment source —
    /// same shape as <c>AddTransactionViewModel.PaymentAccounts</c> (README §11's "Payment method"
    /// concept, extended to the recurring analogue). Excludes <c>TermDeposit</c> (locked until
    /// maturity — no legitimate "spend directly from a term deposit" use case, mirroring
    /// <c>PaymentAccounts</c>) and <c>InvestmentFund</c> (would bypass
    /// <c>InvestmentFund.RecordContribution</c>/<c>RecordWithdrawal</c> tracking if debited directly,
    /// mirroring <c>AddTransactionViewModel.Accounts</c>) — neither exclusion existed here before this
    /// picker became credit-card-aware; both are the same bug class CLAUDE.md flags as this domain's
    /// #1 correctness risk, just via silent balance corruption/a permanently-stuck due item rather than
    /// double-counted spend.
    /// </summary>
    public ObservableCollection<NamedOption> Accounts { get; } = [];

    public AddRecurringExpenseViewModel(
        IRecurringExpenseRepository recurringExpenseRepository,
        ICategoryRepository categoryRepository,
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository)
    {
        _recurringExpenseRepository = recurringExpenseRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        var accounts = await _accountRepository.GetActiveAsync();
        var creditAccounts = await _creditAccountRepository.GetActiveAsync();

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        // README §14's own example uses a "💳 " prefix for cards (e.g. "💳 BAC Card"); the Picker
        // renders via NamedOption.ToString(), so no ItemDisplayBinding is needed for this prefix — same
        // convention as AddTransactionViewModel.PaymentAccounts.
        Accounts.Clear();
        foreach (var account in accounts.Where(a => a is not TermDeposit and not InvestmentFund))
            Accounts.Add(new NamedOption(account.Id, account.Name, account.Currency, IsCreditAccount: false));
        foreach (var creditCard in creditAccounts.OfType<CreditCard>())
            Accounts.Add(new NamedOption(creditCard.Id, $"💳 {creditCard.Name}", creditCard.Currency, IsCreditAccount: true));

        if (EditingId is { } id && _prefilledId != id)
        {
            var r = await _recurringExpenseRepository.GetByIdAsync(id);
            if (r is null)
            {
                ErrorMessage = AppResources.AddRecurringExpense_NotFound;
                return;
            }

            Name = r.Name;
            AmountText = r.Amount.ToString("0.##", CultureInfo.CurrentCulture);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == r.CategoryId);
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == (r.AccountId ?? r.CreditAccountId));
            SelectedFrequency = r.Frequency;
            StartDate = r.StartDate.ToDateTime(TimeOnly.MinValue);
            HasEndDate = r.EndDate is not null;
            EndDate = (r.EndDate ?? r.StartDate).ToDateTime(TimeOnly.MinValue);
            IsStartDateEnabled = r.LastConfirmedDate is null;
            _prefilledId = id;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = AppResources.AddRecurringExpense_ValidationNameRequired;
            return;
        }

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            ErrorMessage = AppResources.AddRecurringExpense_ValidationAmountInvalid;
            return;
        }

        if (SelectedCategory is null)
        {
            ErrorMessage = AppResources.AddRecurringExpense_ValidationCategoryRequired;
            return;
        }

        if (SelectedAccount is null)
        {
            ErrorMessage = AppResources.AddRecurringExpense_ValidationAccountRequired;
            return;
        }

        var startDateOnly = DateOnly.FromDateTime(StartDate);
        DateOnly? endDateOnly = null;

        if (HasEndDate)
        {
            endDateOnly = DateOnly.FromDateTime(EndDate);
            if (endDateOnly.Value < startDateOnly)
            {
                ErrorMessage = AppResources.AddRecurringExpense_ValidationEndDateInvalid;
                return;
            }
        }

        IsBusy = true;
        try
        {
            if (EditingId is { } editingId)
            {
                // Prospective only: already-posted transactions and the confirmed-through date are untouched.
                var existing = await _recurringExpenseRepository.GetByIdAsync(editingId);
                if (existing is null)
                {
                    ErrorMessage = AppResources.AddRecurringExpense_NotFound;
                    return;
                }

                existing.UpdateDetails(
                    Name,
                    amount,
                    SelectedCategory.Id,
                    SelectedAccount.IsCreditAccount ? null : SelectedAccount.Id,
                    SelectedAccount.IsCreditAccount ? SelectedAccount.Id : null,
                    SelectedFrequency,
                    startDateOnly,
                    endDateOnly);
            }
            else
            {
                var recurringExpense = SelectedAccount.IsCreditAccount
                    ? RecurringExpense.ForCreditCard(
                        Name,
                        amount,
                        SelectedCategory.Id,
                        SelectedAccount.Id,
                        SelectedFrequency,
                        startDateOnly,
                        endDateOnly)
                    : new RecurringExpense(
                        Name,
                        amount,
                        SelectedCategory.Id,
                        SelectedAccount.Id,
                        SelectedFrequency,
                        startDateOnly,
                        endDateOnly);

                await _recurringExpenseRepository.AddAsync(recurringExpense);
            }

            await _recurringExpenseRepository.SaveChangesAsync();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Deletes the definition (edit mode only). Occurrences already confirmed stay in the history as the
    /// ordinary transactions they are — nothing links them back to this definition.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingId is not { } id)
            return;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.AddRecurringExpense_DeleteConfirmTitle,
            string.Format(CultureInfo.CurrentCulture, AppResources.AddRecurringExpense_DeleteConfirmMessage, Name),
            AppResources.AddRecurringExpense_DeleteConfirmAccept,
            AppResources.AddRecurringExpense_DeleteConfirmCancel);
        if (!confirmed)
            return;

        var existing = await _recurringExpenseRepository.GetByIdAsync(id);
        if (existing is not null)
        {
            _recurringExpenseRepository.Remove(existing);
            await _recurringExpenseRepository.SaveChangesAsync();
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
