using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Application.Reporting;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class BudgetsListViewModel : ObservableObject
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly IBudgetEvaluator _budgetEvaluator;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<BudgetListItem> Budgets { get; } = [];

    public bool IsEmpty => HasLoaded && Budgets.Count == 0;

    public BudgetsListViewModel(
        IBudgetRepository budgetRepository,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        ISpendingCalculator spendingCalculator,
        IBudgetEvaluator budgetEvaluator)
    {
        _budgetRepository = budgetRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _spendingCalculator = spendingCalculator;
        _budgetEvaluator = budgetEvaluator;
    }

    [RelayCommand]
    private async Task LoadBudgetsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);

            var budgets = await _budgetRepository.GetForMonthAsync(today.Year, today.Month);
            var transactions = await _transactionRepository.GetByDateRangeAsync(startOfMonth, today);
            var categories = await _categoryRepository.GetAllAsync();

            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);

            var summary = _spendingCalculator.Calculate(transactions);
            var statuses = _budgetEvaluator.Evaluate(budgets, summary).ToDictionary(s => s.CategoryId);

            Budgets.Clear();
            foreach (var budget in budgets)
            {
                if (!statuses.TryGetValue(budget.CategoryId, out var status))
                    continue;

                var categoryName = categoryNames.TryGetValue(budget.CategoryId, out var name) ? name : "?";
                Budgets.Add(BudgetListItem.FromDomain(budget, status, categoryName));
            }

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddBudgetAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddBudgetPage));
    }
}
