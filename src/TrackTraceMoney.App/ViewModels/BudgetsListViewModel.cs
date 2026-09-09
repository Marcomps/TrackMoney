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
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
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
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        ISpendingCalculator spendingCalculator,
        IBudgetEvaluator budgetEvaluator)
    {
        _budgetRepository = budgetRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
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
            var accounts = await _accountRepository.GetAllAsync();
            var creditAccounts = await _creditAccountRepository.GetAllAsync();

            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            var accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);

            var summary = _spendingCalculator.Calculate(transactions, accountCurrencies);
            // BudgetEvaluator.Evaluate returns one status per input budget, in the same order — zipped
            // by position rather than a CategoryId-keyed dictionary, because a category can now
            // legitimately have more than one budget in the same month (one per currency).
            var statuses = _budgetEvaluator.Evaluate(budgets, summary);

            Budgets.Clear();
            foreach (var (budget, status) in budgets.Zip(statuses))
            {
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
