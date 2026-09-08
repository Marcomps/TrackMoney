using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Backs the History screen (README §39) — a read-only browse+filter view over every transaction
/// ever recorded, as opposed to <see cref="TransactionsListViewModel"/> which only shows the current
/// month. Filters are applied in-memory over a single <c>ITransactionRepository.GetAllAsync</c>
/// snapshot (offline-first, local SQLite, MVP volume — no pagination/server-side filtering needed).
/// "Card" and "Status" filters from README's example are intentionally omitted: credit card accounts
/// are Phase 2, and "Status" has no backing field on <c>Transaction</c> yet.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPersonRepository _personRepository;

    private List<HistoryEntryItem> _allEntries = [];

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasLoaded;

    [ObservableProperty]
    private NamedOption? selectedAccountFilter;

    [ObservableProperty]
    private NamedOption? selectedCategoryFilter;

    [ObservableProperty]
    private NamedOption? selectedPersonFilter;

    [ObservableProperty]
    private TransactionTypeFilterOption? selectedTypeFilter;

    [ObservableProperty]
    private DateTime fromDate = DateTime.Today.AddYears(-1);

    [ObservableProperty]
    private DateTime toDate = DateTime.Today;

    [ObservableProperty]
    private string minAmountText = string.Empty;

    [ObservableProperty]
    private string maxAmountText = string.Empty;

    public ObservableCollection<NamedOption> AccountOptions { get; } = [];

    public ObservableCollection<NamedOption> CategoryOptions { get; } = [];

    public ObservableCollection<NamedOption> PersonOptions { get; } = [];

    public ObservableCollection<TransactionTypeFilterOption> TypeOptions { get; } = [];

    public ObservableCollection<HistoryDateGroup> GroupedEntries { get; } = [];

    public bool IsEmpty => HasLoaded && GroupedEntries.Count == 0;

    public HistoryViewModel(
        ITransactionRepository transactionRepository,
        IFinancialAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IPersonRepository personRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;

        TypeOptions.Add(new TransactionTypeFilterOption(null, AppResources.History_AllTypesOption));
        foreach (var type in Enum.GetValues<TransactionType>())
            TypeOptions.Add(new TransactionTypeFilterOption(type, TransactionTypeToLabelConverter.GetDisplayName(type)));

        selectedTypeFilter = TypeOptions[0];
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var accounts = await _accountRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();
            var people = await _personRepository.GetAllAsync();
            var transactions = await _transactionRepository.GetAllAsync();

            var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);

            _allEntries = transactions
                .Select(t => HistoryEntryItem.FromDomain(t, accountNames, categoryNames))
                .ToList();

            AccountOptions.Clear();
            AccountOptions.Add(new NamedOption(Guid.Empty, AppResources.History_AllAccountsOption));
            foreach (var account in accounts)
                AccountOptions.Add(new NamedOption(account.Id, account.Name, account.Currency));

            CategoryOptions.Clear();
            CategoryOptions.Add(new NamedOption(Guid.Empty, AppResources.History_AllCategoriesOption));
            foreach (var category in categories)
                CategoryOptions.Add(new NamedOption(category.Id, SystemCategoryKeyToLabelConverter.GetDisplayName(category)));

            PersonOptions.Clear();
            PersonOptions.Add(new NamedOption(Guid.Empty, AppResources.History_AllPeopleOption));
            foreach (var person in people)
                PersonOptions.Add(new NamedOption(person.Id, person.Name));

            SelectedAccountFilter ??= AccountOptions[0];
            SelectedCategoryFilter ??= CategoryOptions[0];
            SelectedPersonFilter ??= PersonOptions[0];
            SelectedTypeFilter ??= TypeOptions[0];

            HasLoaded = true;
            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedAccountFilter = AccountOptions.Count > 0 ? AccountOptions[0] : null;
        SelectedCategoryFilter = CategoryOptions.Count > 0 ? CategoryOptions[0] : null;
        SelectedPersonFilter = PersonOptions.Count > 0 ? PersonOptions[0] : null;
        SelectedTypeFilter = TypeOptions.Count > 0 ? TypeOptions[0] : null;
        FromDate = DateTime.Today.AddYears(-1);
        ToDate = DateTime.Today;
        MinAmountText = string.Empty;
        MaxAmountText = string.Empty;

        ApplyFilters();
    }

    partial void OnSelectedAccountFilterChanged(NamedOption? value) => ApplyFilters();

    partial void OnSelectedCategoryFilterChanged(NamedOption? value) => ApplyFilters();

    partial void OnSelectedPersonFilterChanged(NamedOption? value) => ApplyFilters();

    partial void OnSelectedTypeFilterChanged(TransactionTypeFilterOption? value) => ApplyFilters();

    partial void OnFromDateChanged(DateTime value) => ApplyFilters();

    partial void OnToDateChanged(DateTime value) => ApplyFilters();

    partial void OnMinAmountTextChanged(string value) => ApplyFilters();

    partial void OnMaxAmountTextChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        IEnumerable<HistoryEntryItem> query = _allEntries;

        var fromDate = DateOnly.FromDateTime(FromDate);
        var toDate = DateOnly.FromDateTime(ToDate);
        query = query.Where(e => e.Date >= fromDate && e.Date <= toDate);

        if (SelectedAccountFilter is { Id: var accountId } && accountId != Guid.Empty)
            query = query.Where(e => e.AccountIds.Contains(accountId));

        if (SelectedCategoryFilter is { Id: var categoryId } && categoryId != Guid.Empty)
            query = query.Where(e => e.CategoryId == categoryId);

        if (SelectedPersonFilter is { Id: var personId } && personId != Guid.Empty)
            query = query.Where(e => e.PersonIds.Contains(personId));

        if (SelectedTypeFilter is { Type: { } type })
            query = query.Where(e => e.Type == type);

        if (decimal.TryParse(MinAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var minAmount))
            query = query.Where(e => e.Amount >= minAmount);

        if (decimal.TryParse(MaxAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var maxAmount))
            query = query.Where(e => e.Amount <= maxAmount);

        var groups = query
            .GroupBy(e => e.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new HistoryDateGroup(
                g.Key.ToDateTime(TimeOnly.MinValue).ToString("d", CultureInfo.CurrentCulture),
                g));

        GroupedEntries.Clear();
        foreach (var group in groups)
            GroupedEntries.Add(group);

        OnPropertyChanged(nameof(IsEmpty));
    }
}
