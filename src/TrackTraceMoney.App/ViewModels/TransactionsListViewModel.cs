using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class TransactionsListViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    public ObservableCollection<TransactionListItem> Transactions { get; } = [];

    public bool IsEmpty => HasLoaded && Transactions.Count == 0;

    public TransactionsListViewModel(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICategoryRepository categoryRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
    }

    [RelayCommand]
    private async Task LoadTransactionsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var accounts = await _accountRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();

            var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            var transactions = await _transactionRepository.GetByDateRangeAsync(startOfMonth, today);

            Transactions.Clear();
            foreach (var transaction in transactions.OrderByDescending(t => t.Date))
                Transactions.Add(TransactionListItem.FromDomain(transaction, accountNames, categoryNames));

            HasLoaded = true;
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task AddTransactionAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddTransactionPage));
    }

    [RelayCommand]
    private static async Task ViewHistoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(HistoryPage));
    }
}
