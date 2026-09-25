using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.RecurringIncomes;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.App.ViewModels;

[QueryProperty(nameof(RecurringIncomeIdText), "id")]
public sealed partial class AddRecurringIncomeViewModel : ObservableObject
{
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;

    /// <summary>
    /// Set when opened from the list's Edit button (<c>?id=</c>): the form then prefills from the existing
    /// definition and Save updates it instead of adding a new one. String-typed query target, parsed
    /// defensively — Shell can't convert a query string to <see cref="Guid"/> (same idiom as
    /// <c>AddBudgetViewModel.BudgetIdText</c>).
    /// </summary>
    [ObservableProperty]
    private string? recurringIncomeIdText;

    partial void OnRecurringIncomeIdTextChanged(string? value) =>
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

    public string PageTitle => IsEditMode ? AppResources.AddRecurringIncome_EditTitle : AppResources.AddRecurringIncome_Title;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthlyEquivalentText))]
    [NotifyPropertyChangedFor(nameof(SemiMonthlyEquivalentText))]
    private string amountText = string.Empty;

    [ObservableProperty]
    private NamedOption? selectedCategory;

    [ObservableProperty]
    private NamedOption? selectedAccount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthlyEquivalentText))]
    [NotifyPropertyChangedFor(nameof(SemiMonthlyEquivalentText))]
    private RecurringIncomeFrequency selectedFrequency = RecurringIncomeFrequency.Monthly;

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

    // Explicit order (not Enum.GetValues) so SemiMonthly, appended last in the enum, sits next to Biweekly.
    public IReadOnlyList<RecurringIncomeFrequency> AvailableFrequencies { get; } =
    [
        RecurringIncomeFrequency.Weekly,
        RecurringIncomeFrequency.Biweekly,
        RecurringIncomeFrequency.SemiMonthly,
        RecurringIncomeFrequency.Monthly,
        RecurringIncomeFrequency.Yearly,
    ];

    public ObservableCollection<NamedOption> Categories { get; } = [];

    /// <summary>
    /// <c>FinancialAccount</c>-only picker for this recurring income's destination -- unlike
    /// <c>AddRecurringExpenseViewModel.Accounts</c>, there is no credit-card entry here at all
    /// (see the recurring-income slice spec's Decision B: <c>Income</c> has no credit-card
    /// destination concept). Still excludes <c>TermDeposit</c> (locked until maturity -- no
    /// legitimate "credit a term deposit directly" use case) and <c>InvestmentFund</c> (would
    /// bypass <c>InvestmentFund.RecordContribution</c> tracking if credited directly), same
    /// reasoning as <c>AddRecurringExpenseViewModel.Accounts</c>.
    /// </summary>
    public ObservableCollection<NamedOption> Accounts { get; } = [];

    /// <summary>Live-updating "enter once, see biweekly and monthly breakdowns" read-out as the user types/changes frequency (recurring-income slice spec §3/§4).</summary>
    public string MonthlyEquivalentText =>
        TryParseAmount(out var amount)
            ? string.Format(CultureInfo.CurrentCulture, AppResources.AddRecurringIncome_MonthlyEquivalentFormat, RecurringIncomeEquivalentCalculator.ToMonthlyEquivalent(amount, SelectedFrequency))
            : string.Empty;

    public string SemiMonthlyEquivalentText =>
        TryParseAmount(out var amount)
            ? string.Format(CultureInfo.CurrentCulture, AppResources.AddRecurringIncome_SemiMonthlyEquivalentFormat, RecurringIncomeEquivalentCalculator.ToSemiMonthlyEquivalent(amount, SelectedFrequency))
            : string.Empty;

    public AddRecurringIncomeViewModel(
        IRecurringIncomeRepository recurringIncomeRepository,
        ICategoryRepository categoryRepository,
        IFinancialAccountRepository accountRepository)
    {
        _recurringIncomeRepository = recurringIncomeRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
    }

    private bool TryParseAmount(out decimal amount) =>
        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) && amount > 0;

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        var accounts = await _accountRepository.GetActiveAsync();

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        Accounts.Clear();
        foreach (var account in accounts.Where(a => a is not TermDeposit and not InvestmentFund))
            Accounts.Add(new NamedOption(account.Id, account.Name, account.Currency));

        if (EditingId is { } id && _prefilledId != id)
        {
            var r = await _recurringIncomeRepository.GetByIdAsync(id);
            if (r is null)
            {
                ErrorMessage = AppResources.AddRecurringIncome_NotFound;
                return;
            }

            Name = r.Name;
            AmountText = r.Amount.ToString("0.##", CultureInfo.CurrentCulture);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == r.CategoryId);
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == r.DestinationAccountId);
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
            ErrorMessage = AppResources.AddRecurringIncome_ValidationNameRequired;
            return;
        }

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            ErrorMessage = AppResources.AddRecurringIncome_ValidationAmountInvalid;
            return;
        }

        if (SelectedCategory is null)
        {
            ErrorMessage = AppResources.AddRecurringIncome_ValidationCategoryRequired;
            return;
        }

        if (SelectedAccount is null)
        {
            ErrorMessage = AppResources.AddRecurringIncome_ValidationAccountRequired;
            return;
        }

        var startDateOnly = DateOnly.FromDateTime(StartDate);
        DateOnly? endDateOnly = null;

        if (HasEndDate)
        {
            endDateOnly = DateOnly.FromDateTime(EndDate);
            if (endDateOnly.Value < startDateOnly)
            {
                ErrorMessage = AppResources.AddRecurringIncome_ValidationEndDateInvalid;
                return;
            }
        }

        IsBusy = true;
        try
        {
            if (EditingId is { } editingId)
            {
                // Prospective only: already-posted transactions and the confirmed-through date are untouched.
                var existing = await _recurringIncomeRepository.GetByIdAsync(editingId);
                if (existing is null)
                {
                    ErrorMessage = AppResources.AddRecurringIncome_NotFound;
                    return;
                }

                existing.UpdateDetails(
                    Name,
                    amount,
                    SelectedCategory.Id,
                    SelectedAccount.Id,
                    SelectedFrequency,
                    startDateOnly,
                    endDateOnly);
            }
            else
            {
                var recurringIncome = new RecurringIncome(
                    Name,
                    amount,
                    SelectedCategory.Id,
                    SelectedAccount.Id,
                    SelectedFrequency,
                    startDateOnly,
                    endDateOnly);

                await _recurringIncomeRepository.AddAsync(recurringIncome);
            }

            await _recurringIncomeRepository.SaveChangesAsync();
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
            AppResources.AddRecurringIncome_DeleteConfirmTitle,
            string.Format(CultureInfo.CurrentCulture, AppResources.AddRecurringIncome_DeleteConfirmMessage, Name),
            AppResources.AddRecurringIncome_DeleteConfirmAccept,
            AppResources.AddRecurringIncome_DeleteConfirmCancel);
        if (!confirmed)
            return;

        var existing = await _recurringIncomeRepository.GetByIdAsync(id);
        if (existing is not null)
        {
            _recurringIncomeRepository.Remove(existing);
            await _recurringIncomeRepository.SaveChangesAsync();
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
