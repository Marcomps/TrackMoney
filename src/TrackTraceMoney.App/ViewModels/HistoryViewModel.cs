using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

/// <summary>
/// Backs the History screen (README §39) — a read-only browse+filter view over every transaction
/// ever recorded, as opposed to <see cref="TransactionsListViewModel"/> which only shows the current
/// month. Filters are applied in-memory over a single <c>ITransactionRepository.GetAllAsync</c>
/// snapshot (offline-first, local SQLite, MVP volume — no pagination/server-side filtering needed).
/// The "Card" filter from README's example is covered by <see cref="AccountOptions"/>, which now
/// includes credit cards alongside <c>FinancialAccount</c>s. "Status" is still intentionally omitted:
/// it has no backing field on <c>Transaction</c> yet.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IMedicalExpenseDetailRepository _medicalExpenseDetailRepository;

    private List<HistoryEntryItem> _allEntries = [];

    // The flat filtered list ApplyFilters() computes GroupedEntries from — GroupedEntries alone loses
    // the flat shape Export needs (a CollectionView-friendly IGrouping structure, not a plain list).
    // Kept in sync with GroupedEntries so Export always exports exactly what's currently on screen.
    private IReadOnlyList<HistoryEntryItem> _filteredEntries = [];

    private IReadOnlyDictionary<Guid, string> _categoryNames = new Dictionary<Guid, string>();

    private IReadOnlyDictionary<Guid, string> _personNames = new Dictionary<Guid, string>();

    private IReadOnlyDictionary<Guid, CurrencyCode> _accountCurrencies = new Dictionary<Guid, CurrencyCode>();

    private bool _isLoading;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isExporting;

    [ObservableProperty]
    private string? exportStatusMessage;

    [ObservableProperty]
    private string? exportErrorMessage;

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
        ICreditAccountRepository creditAccountRepository,
        ICategoryRepository categoryRepository,
        IPersonRepository personRepository,
        IMedicalExpenseDetailRepository medicalExpenseDetailRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _medicalExpenseDetailRepository = medicalExpenseDetailRepository;

        TypeOptions.Add(new TransactionTypeFilterOption(null, AppResources.History_AllTypesOption));
        foreach (var type in Enum.GetValues<TransactionType>())
            TypeOptions.Add(new TransactionTypeFilterOption(type, TransactionTypeToLabelConverter.GetDisplayName(type)));

        selectedTypeFilter = TypeOptions[0];
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        // Not an IsBusy check: pull-to-refresh sets IsBusy (IsRefreshing's two-way binding) *before*
        // invoking this command, so an IsBusy guard returned early and left the spinner stuck forever.
        if (_isLoading)
            return;

        _isLoading = true;
        IsBusy = true;
        try
        {
            var accounts = await _accountRepository.GetAllAsync();
            var creditAccounts = await _creditAccountRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();
            var people = await _personRepository.GetAllAsync();
            var transactions = await _transactionRepository.GetAllAsync();

            var accountNames = AccountNameMapBuilder.Build(accounts, creditAccounts);
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            _categoryNames = categoryNames;
            _personNames = people.ToDictionary(p => p.Id, p => p.Name);
            _accountCurrencies = AccountCurrencyMapBuilder.Build(accounts, creditAccounts);

            // Batched instead of one GetForTransactionAsync call per row below (same anti-N+1 pattern as
            // the Phase 2 checkpoint's batch-fetch fixes) — a single query for every transaction id on
            // this screen, since History shows the full local history, not just one page.
            var transactionIds = transactions.Select(t => t.Id).ToList();
            var medicalDetailsByTransaction = await _medicalExpenseDetailRepository.GetForTransactionsAsync(transactionIds);

            _allEntries = transactions
                .Select(t => HistoryEntryItem.FromDomain(
                    t,
                    accountNames,
                    categoryNames,
                    medicalDetailsByTransaction.TryGetValue(t.Id, out var detail) ? detail.Status : null))
                .ToList();

            AccountOptions.Clear();
            AccountOptions.Add(new NamedOption(Guid.Empty, AppResources.History_AllAccountsOption));
            foreach (var account in accounts)
                AccountOptions.Add(new NamedOption(account.Id, account.Name, account.Currency));
            foreach (var creditAccount in creditAccounts)
                AccountOptions.Add(new NamedOption(creditAccount.Id, $"💳 {creditAccount.Name}", creditAccount.Currency, IsCreditAccount: true));

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
            _isLoading = false;
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

        _filteredEntries = query.ToList();

        var groups = _filteredEntries
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

    /// <summary>
    /// Exports exactly what's currently filtered/visible (<see cref="_filteredEntries"/>, kept in sync
    /// with <see cref="GroupedEntries"/> by <see cref="ApplyFilters"/>) as a CSV file and hands it to
    /// the platform share sheet — same mechanism §42's local backup export already uses
    /// (<c>SettingsViewModel.ExportAsync</c>: write to <see cref="FileSystem.CacheDirectory"/>, then
    /// <c>Share.Default.RequestAsync</c>), not a new file-sharing approach.
    /// </summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        ExportErrorMessage = null;
        ExportStatusMessage = null;

        if (_filteredEntries.Count == 0)
        {
            ExportErrorMessage = AppResources.History_ExportCsvEmpty;
            return;
        }

        IsExporting = true;
        try
        {
            var csv = TransactionCsvRowBuilder.Build(_filteredEntries, _categoryNames, _personNames, _accountCurrencies);

            var fileName = $"tracktracemoney-transactions-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var exportPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(exportPath, csv);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = AppResources.History_ExportCsvButton,
                File = new ShareFile(exportPath)
            });

            // Worded as "ready to share," not "exported"/"shared successfully": Share.Default.RequestAsync
            // returns normally whether the user actually picks a share target or backs out of the chooser
            // -- the platform API gives no reliable cancellation signal (found via checkpoint code review;
            // SettingsViewModel.ExportAsync's local-backup export shares this same limitation). What IS
            // verifiably true by this point is that the file was written and the share sheet was shown.
            ExportStatusMessage = AppResources.History_ExportCsvSuccess;
        }
        catch (Exception)
        {
            ExportErrorMessage = AppResources.History_ExportCsvError;
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// Opens <see cref="MedicalExpenseDetailPage"/> for a row's underlying transaction. The tap gesture
    /// in <c>HistoryPage.xaml</c> is wired on every row (this codebase has no established
    /// null-to-bool XAML converter to conditionally disable a <c>GestureRecognizer</c>), so the
    /// non-null <see cref="HistoryEntryItem.MedicalStatusBadge"/> check happens here instead — a tap on
    /// a badge-less row (no linked <c>MedicalExpenseDetail</c> at all) is a no-op.
    /// </summary>
    [RelayCommand]
    private static async Task OpenMedicalExpenseDetailAsync(HistoryEntryItem? entry)
    {
        if (entry?.MedicalStatusBadge is null)
            return;

        await Shell.Current.GoToAsync($"{nameof(MedicalExpenseDetailPage)}?transactionId={entry.Id}");
    }

    /// <summary>
    /// General-purpose row-tap routing (edit/delete slice spec §5) — supersedes
    /// <see cref="OpenMedicalExpenseDetailAsync"/> as the tap gesture actually wired in
    /// <c>HistoryPage.xaml</c> (that method is kept, unmodified, rather than removed, since its own
    /// medical-priority branch is now folded in here verbatim). Medical detail still takes priority,
    /// unchanged; Expense/Income/Transfer route to the new <see cref="TransactionDetailPage"/>; every
    /// other type (CreditCardPurchase, CreditCardPayment, LoanPayment, InvestmentContribution/
    /// Withdrawal, InterestIncome, Reimbursement) is intentionally still a no-op, now by documented
    /// exclusion (§4.3) rather than accident — no toast/disabled-row treatment is added this slice.
    /// </summary>
    [RelayCommand]
    private static async Task OpenTransactionDetailAsync(HistoryEntryItem? entry)
    {
        if (entry is null)
            return;

        if (entry.MedicalStatusBadge is not null)
        {
            await Shell.Current.GoToAsync($"{nameof(MedicalExpenseDetailPage)}?transactionId={entry.Id}");
            return;
        }

        if (entry.Type is TransactionType.Expense or TransactionType.CreditCardPurchase or TransactionType.Income or TransactionType.Transfer)
        {
            await Shell.Current.GoToAsync($"{nameof(TransactionDetailPage)}?transactionId={entry.Id}");
            return;
        }
    }
}
