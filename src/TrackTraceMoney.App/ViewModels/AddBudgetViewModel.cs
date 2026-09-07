using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Budgets;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class AddBudgetViewModel : ObservableObject
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;

    [ObservableProperty]
    private NamedOption? selectedCategory;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<NamedOption> Categories { get; } = [];

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
            var existing = await _budgetRepository.GetForCategoryAndMonthAsync(SelectedCategory.Id, today.Year, today.Month);
            if (existing is not null)
            {
                existing.UpdateAmount(amount);
            }
            else
            {
                var budget = new Budget(SelectedCategory.Id, amount, today.Year, today.Month);
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

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
