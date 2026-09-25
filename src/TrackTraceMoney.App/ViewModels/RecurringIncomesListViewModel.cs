using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Services;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.RecurringIncomes;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class RecurringIncomesListViewModel : ObservableObject
{
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly IRecurringIncomeService _recurringIncomeService;

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<DueRecurringIncomeListItem> DueRecurringIncomes { get; } = [];

    public ObservableCollection<RecurringIncomeListItem> RecurringIncomes { get; } = [];

    public bool IsEmpty => HasLoaded && RecurringIncomes.Count == 0;

    public bool HasDueRecurringIncomes => DueRecurringIncomes.Count > 0;

    public RecurringIncomesListViewModel(
        IRecurringIncomeRepository recurringIncomeRepository,
        ICategoryRepository categoryRepository,
        IFinancialAccountRepository accountRepository,
        IRecurringIncomeService recurringIncomeService)
    {
        _recurringIncomeRepository = recurringIncomeRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
        _recurringIncomeService = recurringIncomeService;
    }

    [RelayCommand]
    private async Task LoadRecurringIncomesAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var recurringIncomes = await _recurringIncomeRepository.GetActiveAsync();
            var categories = await _categoryRepository.GetAllAsync();
            var accounts = await _accountRepository.GetAllAsync();

            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);

            static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid id) =>
                names.TryGetValue(id, out var name) ? name : "?";

            DueRecurringIncomes.Clear();
            RecurringIncomes.Clear();

            foreach (var recurringIncome in recurringIncomes.OrderBy(r => r.Name))
            {
                var categoryName = NameOf(categoryNames, recurringIncome.CategoryId);
                var accountName = NameOf(accountNames, recurringIncome.DestinationAccountId);

                var listItem = RecurringIncomeListItem.FromDomain(recurringIncome, categoryName, accountName);
                RecurringIncomes.Add(listItem);

                if (recurringIncome.CanConfirm(today))
                    DueRecurringIncomes.Add(DueRecurringIncomeListItem.FromListItem(listItem, today));
            }

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasDueRecurringIncomes));
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync(Guid recurringIncomeId)
    {
        if (IsBusy)
            return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        var item = DueRecurringIncomes.FirstOrDefault(d => d.Id == recurringIncomeId);
        if (item is not null && !await RecurringIncomeConfirmPrompt.ProceedAsync(item.Name, item.OccurrenceDate, item.Amount, today))
            return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await _recurringIncomeService.ConfirmOccurrenceAsync(recurringIncomeId, today);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.RecurringIncomes_ConfirmError;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        // A confirmed occurrence might immediately become due again if multiple periods were missed
        // — reload both the due list and the full list to reflect that.
        await LoadRecurringIncomesAsync();
    }

    [RelayCommand]
    private async Task DeactivateAsync(Guid recurringIncomeId)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var recurringIncome = await _recurringIncomeRepository.GetByIdAsync(recurringIncomeId);
            if (recurringIncome is null)
                return;

            recurringIncome.Deactivate();
            await _recurringIncomeRepository.SaveChangesAsync();
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.RecurringIncomes_DeactivateError;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadRecurringIncomesAsync();
    }

    [RelayCommand]
    private static async Task AddRecurringIncomeAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddRecurringIncomePage));
    }

    [RelayCommand]
    private static async Task EditAsync(Guid recurringIncomeId) =>
        await Shell.Current.GoToAsync($"{nameof(AddRecurringIncomePage)}?id={recurringIncomeId}");
}
