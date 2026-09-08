using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddRecurringExpenseViewModel : ObservableObject
{
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;

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

    public ObservableCollection<NamedOption> Accounts { get; } = [];

    public AddRecurringExpenseViewModel(
        IRecurringExpenseRepository recurringExpenseRepository,
        ICategoryRepository categoryRepository,
        IFinancialAccountRepository accountRepository)
    {
        _recurringExpenseRepository = recurringExpenseRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        var accounts = await _accountRepository.GetActiveAsync();

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        Accounts.Clear();
        foreach (var account in accounts)
            Accounts.Add(new NamedOption(account.Id, account.Name, account.Currency));
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
            var recurringExpense = new RecurringExpense(
                Name,
                amount,
                SelectedCategory.Id,
                SelectedAccount.Id,
                SelectedFrequency,
                startDateOnly,
                endDateOnly);

            await _recurringExpenseRepository.AddAsync(recurringExpense);
            await _recurringExpenseRepository.SaveChangesAsync();
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
