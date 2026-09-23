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

public sealed partial class AddRecurringIncomeViewModel : ObservableObject
{
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;

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
            var recurringIncome = new RecurringIncome(
                Name,
                amount,
                SelectedCategory.Id,
                SelectedAccount.Id,
                SelectedFrequency,
                startDateOnly,
                endDateOnly);

            await _recurringIncomeRepository.AddAsync(recurringIncome);
            await _recurringIncomeRepository.SaveChangesAsync();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
