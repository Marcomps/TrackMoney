using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Also doubles as the edit screen (budget edit/delete slice §1) when navigated to with a
/// <c>budgetId</c> query parameter: <see cref="LoadOptionsAsync"/> additionally prefills the
/// existing budget's fields and locks Category/Currency (both are part of the upsert key used by
/// <see cref="SaveAsync"/>'s existing <c>GetForCategoryAndMonthAsync</c> + <c>UpdateAmount</c> path,
/// so locking them means that path is reused completely unmodified in edit mode -- see
/// <c>AddAccountViewModel</c>'s doc comment for the precedent this mirrors).
/// </summary>
[QueryProperty(nameof(BudgetIdText), "budgetId")]
public sealed partial class AddBudgetViewModel : ObservableObject
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsCategoryPickerEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyPickerEnabled))]
    private Guid? editingBudgetId;

    /// <summary>
    /// The actual <c>[QueryProperty]</c> target -- a non-nullable <see cref="Guid"/> can't be bound
    /// directly (see <c>AddAccountViewModel.AccountIdText</c>'s doc comment, the precedent this
    /// copies). Absent/unparseable simply means "add mode", not an error.
    /// </summary>
    [ObservableProperty]
    private string? budgetIdText;

    partial void OnBudgetIdTextChanged(string? value) =>
        EditingBudgetId = Guid.TryParse(value, out var parsed) ? parsed : null;

    public bool IsEditMode => EditingBudgetId is not null;

    public string PageTitle => IsEditMode ? AppResources.AddBudget_EditTitle : AppResources.AddBudget_Title;

    public bool IsCategoryPickerEnabled => !IsEditMode;

    public bool IsCurrencyPickerEnabled => !IsEditMode;

    [ObservableProperty]
    private NamedOption? selectedCategory;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private CurrencyCode selectedCurrency = CurrencyCode.USD;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<NamedOption> Categories { get; } = [];

    public IReadOnlyList<CurrencyCode> AvailableCurrencies { get; } = Enum.GetValues<CurrencyCode>();

    public AddBudgetViewModel(IBudgetRepository budgetRepository, ICategoryRepository categoryRepository)
    {
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
    }

    [RelayCommand]
    private async Task LoadOptionsAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();

        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

        if (EditingBudgetId is not { } budgetId)
            return;

        var budget = await _budgetRepository.GetByIdAsync(budgetId);
        if (budget is null)
        {
            ErrorMessage = AppResources.AddBudget_EditNotFound;
            return;
        }

        SelectedCategory = Categories.FirstOrDefault(c => c.Id == budget.CategoryId);
        AmountText = budget.Amount.ToString("N2", CultureInfo.CurrentCulture);
        SelectedCurrency = budget.Currency;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedCategory is null)
        {
            ErrorMessage = AppResources.AddBudget_ValidationCategoryRequired;
            return;
        }

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            ErrorMessage = AppResources.AddBudget_ValidationAmountInvalid;
            return;
        }

        var today = DateTime.Today;

        IsBusy = true;
        try
        {
            var existing = await _budgetRepository.GetForCategoryAndMonthAsync(SelectedCategory.Id, today.Year, today.Month, SelectedCurrency);
            if (existing is not null)
            {
                existing.UpdateAmount(amount);
            }
            else
            {
                var budget = new Budget(SelectedCategory.Id, amount, today.Year, today.Month, SelectedCurrency);
                await _budgetRepository.AddAsync(budget);
            }

            await _budgetRepository.SaveChangesAsync();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Visible only in edit mode (budget edit/delete slice §1). Confirms via a destructive dialog
    /// first, matching this codebase's established convention for entity delete (see
    /// <c>AccountsListViewModel.DeleteAccountAsync</c>) -- no dependent-entity guard is needed first
    /// (unlike that Account precedent) since Budget has no <c>IsActive</c>/lifecycle concept and no
    /// other entity references a Budget, so deleting one is always safe once confirmed.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditingBudgetId is not { } budgetId)
            return;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.AddBudget_DeleteConfirmTitle,
            AppResources.AddBudget_DeleteConfirmMessage,
            AppResources.AddBudget_DeleteConfirmAccept,
            AppResources.AddBudget_DeleteConfirmCancel);

        if (!confirmed)
            return;

        IsBusy = true;
        try
        {
            var budget = await _budgetRepository.GetByIdAsync(budgetId);
            if (budget is null)
            {
                ErrorMessage = AppResources.AddBudget_EditNotFound;
                return;
            }

            _budgetRepository.Remove(budget);
            await _budgetRepository.SaveChangesAsync();
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
