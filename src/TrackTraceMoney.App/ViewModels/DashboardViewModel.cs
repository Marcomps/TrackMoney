using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Application.NetWorth;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.TermDeposits;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly IIncomeCalculator _incomeCalculator;
    private readonly IBudgetEvaluator _budgetEvaluator;
    private readonly INetWorthCalculator _netWorthCalculator;
    private readonly INetWorthSnapshotService _netWorthSnapshotService;
    private readonly ITermDepositRenewalService _termDepositRenewalService;
    private readonly ILocalNotifier _localNotifier;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasLoaded;

    [ObservableProperty]
    private string healthEmoji = string.Empty;

    [ObservableProperty]
    private string healthLabel = string.Empty;

    public ObservableCollection<CurrencyBalance> Balances { get; } = [];

    public ObservableCollection<CurrencyIncomeExpense> IncomeExpenseSummaries { get; } = [];

    public ObservableCollection<BudgetListItem> OverBudgetCategories { get; } = [];

    public ObservableCollection<RecurringExpenseListItem> UpcomingPayments { get; } = [];

    public ObservableCollection<NetWorthTileItem> NetWorthByCurrency { get; } = [];

    public bool HasOverBudgetCategories => OverBudgetCategories.Count > 0;

    public bool ShowOverBudgetEmpty => HasLoaded && !HasOverBudgetCategories;

    public bool HasUpcomingPayments => UpcomingPayments.Count > 0;

    public bool ShowUpcomingPaymentsEmpty => HasLoaded && !HasUpcomingPayments;

    public DashboardViewModel(
        IFinancialAccountRepository accountRepository,
        ICreditAccountRepository creditAccountRepository,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IRecurringExpenseRepository recurringExpenseRepository,
        ISpendingCalculator spendingCalculator,
        IIncomeCalculator incomeCalculator,
        IBudgetEvaluator budgetEvaluator,
        INetWorthCalculator netWorthCalculator,
        INetWorthSnapshotService netWorthSnapshotService,
        ITermDepositRenewalService termDepositRenewalService,
        ILocalNotifier localNotifier)
    {
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
        _spendingCalculator = spendingCalculator;
        _incomeCalculator = incomeCalculator;
        _budgetEvaluator = budgetEvaluator;
        _netWorthCalculator = netWorthCalculator;
        _netWorthSnapshotService = netWorthSnapshotService;
        _termDepositRenewalService = termDepositRenewalService;
        _localNotifier = localNotifier;
    }

    [RelayCommand]
    private async Task LoadDashboardAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);

            var accounts = await _accountRepository.GetActiveAsync();
            var allAccounts = await _accountRepository.GetAllAsync();
            var allCreditAccounts = await _creditAccountRepository.GetAllAsync();
            var transactions = await _transactionRepository.GetByDateRangeAsync(startOfMonth, today);
            var budgets = await _budgetRepository.GetForMonthAsync(today.Year, today.Month);
            var categories = await _categoryRepository.GetAllAsync();
            var recurringExpenses = await _recurringExpenseRepository.GetActiveAsync();

            // Tile 1: available balance per currency — never summed across currencies. Deliberately
            // FinancialAccount only: CreditAccount.AmountOwed is debt, never "available balance"
            // (CLAUDE.md hard constraint — a card's debt must never inflate this tile). Also filtered to
            // CountsAsAvailableBalance: a TermDeposit's balance is locked until maturity, so it must not
            // inflate "what you have" here even though it's a FinancialAccount (README §30 Q1).
            Balances.Clear();
            foreach (var group in accounts.Where(a => a.CountsAsAvailableBalance).GroupBy(a => a.Currency))
                Balances.Add(new CurrencyBalance(group.Key, group.Sum(a => a.Balance)));

            // Tile 2: income / expenses / available this month — per currency, never blended
            // (mirrors Tile 1's accounts.GroupBy(a => a.Currency) above). Built from ALL accounts
            // (active and inactive), not the active-only `accounts` used for Tile 1's balance sum: this
            // month's transaction history can include a transaction against an account the user has
            // since deactivated, and that transaction still needs its currency resolved or it silently
            // vanishes from the expense/income totals (SpendingCalculator skips unresolvable accounts
            // defensively rather than throwing — see its own doc comment — so the caller must hand it a
            // complete map; this is the #1 correctness risk called out in CLAUDE.md).
            var accountCurrencies = AccountCurrencyMapBuilder.Build(allAccounts, allCreditAccounts);
            var spendingSummary = _spendingCalculator.Calculate(transactions, accountCurrencies);
            var incomeSummary = _incomeCalculator.Calculate(transactions, accountCurrencies);

            IncomeExpenseSummaries.Clear();
            var currencies = spendingSummary.TotalSpentByCurrency.Keys.Union(incomeSummary.TotalIncomeByCurrency.Keys);
            foreach (var currency in currencies)
            {
                var currencyIncome = incomeSummary.TotalIncomeByCurrency.GetValueOrDefault(currency);
                var currencyExpenses = spendingSummary.TotalSpentByCurrency.GetValueOrDefault(currency);
                IncomeExpenseSummaries.Add(new CurrencyIncomeExpense(currency, currencyIncome, currencyExpenses));
            }

            // Tile 3: over-budget categories this month.
            var categoryNames = categories.ToDictionary(c => c.Id, SystemCategoryKeyToLabelConverter.GetDisplayName);
            // BudgetEvaluator.Evaluate returns one status per input budget, in the same order — zipped
            // by position rather than a CategoryId-keyed dictionary, because a category can now
            // legitimately have more than one budget in the same month (one per currency).
            var statuses = _budgetEvaluator.Evaluate(budgets, spendingSummary);

            OverBudgetCategories.Clear();
            foreach (var (budget, status) in budgets.Zip(statuses))
            {
                if (!status.IsOverBudget)
                    continue;

                var categoryName = categoryNames.TryGetValue(budget.CategoryId, out var name) ? name : "?";
                OverBudgetCategories.Add(BudgetListItem.FromDomain(budget, status, categoryName));
            }

            // Tile 4: financial health indicator v0.
            var hasNegativeBalance = accounts.Any(a => a.Balance < 0);
            var health = FinancialHealthEvaluator.Evaluate(hasNegativeBalance, OverBudgetCategories.Count > 0);
            HealthEmoji = FinancialHealthEvaluator.GetEmoji(health);
            HealthLabel = health switch
            {
                FinancialHealthStatus.Critical => AppResources.Dashboard_Health_Critical,
                FinancialHealthStatus.AtRisk => AppResources.Dashboard_Health_AtRisk,
                _ => AppResources.Dashboard_Health_Healthy
            };

            // Tile 5: upcoming payments — recurring expenses due now or due within the next 7 days
            // (README §30 question 4, "What do I need to pay?"). Read-only here: confirming or
            // deactivating an occurrence stays exclusively on the Recurring Expenses screen.
            var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);
            var dueSoonCutoff = today.AddDays(7);

            UpcomingPayments.Clear();
            foreach (var recurringExpense in recurringExpenses
                         .Where(r => r.IsDue(dueSoonCutoff))
                         .OrderBy(r => r.NextOccurrenceDate))
            {
                var categoryName = categoryNames.TryGetValue(recurringExpense.CategoryId, out var name) ? name : "?";
                var accountName = accountNames.TryGetValue(recurringExpense.AccountId, out var accName) ? accName : "?";
                UpcomingPayments.Add(RecurringExpenseListItem.FromDomain(recurringExpense, categoryName, accountName));
            }

            // Net worth tile (README §24) — sum of ALL active FinancialAccount balances (regardless of
            // CountsAsAvailableBalance, unlike Tile 1 above) minus active CreditAccount debt, per
            // currency, never blended across currencies. Computed here directly from the active accounts
            // already fetched (plus a fresh active-only credit account fetch, since Tile 2 above needs
            // ALL credit accounts, not active-only) so the tile never needs to re-read the snapshot table
            // that RecordSnapshotAsync below writes to.
            var activeCreditAccounts = await _creditAccountRepository.GetActiveAsync();
            var netWorthSummary = _netWorthCalculator.Calculate(accounts, activeCreditAccounts);

            NetWorthByCurrency.Clear();
            foreach (var byCurrency in netWorthSummary.ByCurrency.Values)
                NetWorthByCurrency.Add(NetWorthTileItem.FromDomain(byCurrency));

            // Side effect: advances the net worth evolution timeline (README §24) by upserting today's
            // snapshot per currency. No manual "record" button exists — every Dashboard load is the only
            // trigger. Passes the netWorthSummary already computed above for the tile, so the service
            // skips its own redundant active-account fetch/recalculation. Guarded by its own try/catch
            // (not the whole method's) — a failure here (e.g. NetWorthSnapshot's constructor rejecting
            // negative TotalLiabilities, or a SQLite write failure) must not prevent the rest of the
            // Dashboard, including the net worth tile itself, from rendering; there is no global
            // exception handler in this app, so an unguarded throw here would crash the Dashboard on
            // every future load.
            try
            {
                await _netWorthSnapshotService.RecordSnapshotAsync(today, netWorthSummary);
            }
            catch (Exception)
            {
                // Best-effort: skip persisting today's snapshot for this load. Silently swallowed —
                // this codebase has no logging abstraction to route this through (no ILogger usage
                // anywhere in the App project) and introducing one is out of scope for this fix.
            }

            // Side effect: rolls any matured, auto-renewal-enabled term deposit into a fresh one
            // (README §22 AutoRenewal) and raises a best-effort notification per renewal. Same
            // defensive-catch shape as the net worth snapshot above — a failure here must never break
            // the rest of the Dashboard.
            try
            {
                var renewals = await _termDepositRenewalService.ProcessMaturedRenewalsAsync(today, ct: default);
                foreach (var renewal in renewals)
                    await _localNotifier.NotifyTermDepositRenewedAsync(
                        renewal.Institution, renewal.RenewedAmount, renewal.Currency, renewal.NewMaturityDate);
            }
            catch (Exception)
            {
                // Best-effort, same reasoning as the net-worth-snapshot catch above: no logger exists in
                // this codebase, and a renewal failure must never break the rest of the Dashboard.
            }

            HasLoaded = true;
            OnPropertyChanged(nameof(HasOverBudgetCategories));
            OnPropertyChanged(nameof(ShowOverBudgetEmpty));
            OnPropertyChanged(nameof(HasUpcomingPayments));
            OnPropertyChanged(nameof(ShowUpcomingPaymentsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task GoToRecurringExpensesAsync()
    {
        // The Recurring Expenses tab's ShellContent registers "RecurringExpensesList" as its own
        // route (see AppShell.xaml) rather than the page type name — it's reached by switching tabs
        // (an absolute "//" route), unlike the "AddXxxPage" pages pushed via Routing.RegisterRoute.
        await Shell.Current.GoToAsync("//RecurringExpensesList");
    }

    [RelayCommand]
    private static async Task GoToNetWorthHistoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(NetWorthPage));
    }
}
