using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.RecurringExpenses;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class RecurringExpensesListViewModel : ObservableObject
{
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly IRecurringExpenseService _recurringExpenseService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<DueRecurringExpenseListItem> DueRecurringExpenses { get; } = [];

    public ObservableCollection<RecurringExpenseListItem> RecurringExpenses { get; } = [];

    public bool IsEmpty => HasLoaded && RecurringExpenses.Count == 0;

    public bool HasDueRecurringExpenses => DueRecurringExpenses.Count > 0;

    public RecurringExpensesListViewModel(
        IRecurringExpenseRepository recurringExpenseRepository,
        ICategoryRepository categoryRepository,
        IFinancialAccountRepository accountRepository,
        IRecurringExpenseService recurringExpenseService)
    {
        _recurringExpenseRepository = recurringExpenseRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
        _recurringExpenseService = recurringExpenseService;
    }

    [RelayCommand]
    private async Task LoadRecurringExpensesAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var recurringExpenses = await _recurringExpenseRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();
            var accounts = await _accountRepository.GetAllAsync();

            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);

            static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid id) =>
                names.TryGetValue(id, out var name) ? name : "?";

            DueRecurringExpenses.Clear();
            RecurringExpenses.Clear();

            foreach (var recurringExpense in recurringExpenses.OrderBy(r => r.Name))
            {
                var categoryName = NameOf(categoryNames, recurringExpense.CategoryId);
                var accountName = NameOf(accountNames, recurringExpense.AccountId);

                RecurringExpenses.Add(RecurringExpenseListItem.FromDomain(recurringExpense, categoryName, accountName));

                if (recurringExpense.IsDue(today))
                    DueRecurringExpenses.Add(DueRecurringExpenseListItem.FromDomain(recurringExpense, categoryName, accountName));
            }

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasDueRecurringExpenses));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync(Guid recurringExpenseId)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await _recurringExpenseService.ConfirmOccurrenceAsync(recurringExpenseId);
        }
        finally
        {
            IsBusy = false;
        }

        // A confirmed occurrence might immediately become due again if multiple periods were missed
        // — reload both the due list and the full list to reflect that.
        await LoadRecurringExpensesAsync();
    }

    [RelayCommand]
    private async Task DeactivateAsync(Guid recurringExpenseId)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var recurringExpense = await _recurringExpenseRepository.GetByIdAsync(recurringExpenseId);
            if (recurringExpense is null)
                return;

            recurringExpense.Deactivate();
            await _recurringExpenseRepository.SaveChangesAsync();
        }
        finally
        {
            IsBusy = false;
        }

        await LoadRecurringExpensesAsync();
    }

    [RelayCommand]
    private static async Task AddRecurringExpenseAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddRecurringExpensePage));
    }
}
