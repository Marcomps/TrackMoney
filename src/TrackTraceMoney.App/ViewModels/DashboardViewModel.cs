using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackTraceMoney.App.Converters;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.App.Services;
using TrackTraceMoney.App.Views;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Budgets;
using TrackTraceMoney.Application.NetWorth;
using TrackTraceMoney.Application.RecurringIncomes;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Application.TermDeposits;
using TrackTraceMoney.Domain.Common;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IFinancialAccountRepository _accountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly ICreditCardStatementRepository _statementRepository;
    private readonly ISpendingCalculator _spendingCalculator;
    private readonly IIncomeCalculator _incomeCalculator;
    private readonly IBudgetEvaluator _budgetEvaluator;
    private readonly INetWorthCalculator _netWorthCalculator;
    private readonly INetWorthSnapshotService _netWorthSnapshotService;
    private readonly ITermDepositRenewalService _termDepositRenewalService;
    private readonly IRealSurplusCalculator _realSurplusCalculator;
    private readonly ILocalNotifier _localNotifier;
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;
    private readonly IRecurringIncomeService _recurringIncomeService;

    private bool _isLoading;

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

    public ObservableCollection<RealSurplusTileItem> RealSurplusByCurrency { get; } = [];

    public ObservableCollection<ProjectionTileItem> Projections { get; } = [];

    public ObservableCollection<UpcomingIncomeItem> UpcomingIncomes { get; } = [];

    [ObservableProperty]
    private string halfMonthThroughText = string.Empty;

    [ObservableProperty]
    private string monthThroughText = string.Empty;

    public bool HasUpcomingIncomes => UpcomingIncomes.Count > 0;

    public bool ShowUpcomingIncomesEmpty => HasLoaded && !HasUpcomingIncomes;

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
        ICreditCardStatementRepository statementRepository,
        ISpendingCalculator spendingCalculator,
        IIncomeCalculator incomeCalculator,
        IBudgetEvaluator budgetEvaluator,
        INetWorthCalculator netWorthCalculator,
        INetWorthSnapshotService netWorthSnapshotService,
        ITermDepositRenewalService termDepositRenewalService,
        IRealSurplusCalculator realSurplusCalculator,
        ILocalNotifier localNotifier,
        IRecurringIncomeRepository recurringIncomeRepository,
        IRecurringIncomeService recurringIncomeService)
    {
        _accountRepository = accountRepository;
        _creditAccountRepository = creditAccountRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
        _statementRepository = statementRepository;
        _spendingCalculator = spendingCalculator;
        _incomeCalculator = incomeCalculator;
        _budgetEvaluator = budgetEvaluator;
        _netWorthCalculator = netWorthCalculator;
        _netWorthSnapshotService = netWorthSnapshotService;
        _termDepositRenewalService = termDepositRenewalService;
        _realSurplusCalculator = realSurplusCalculator;
        _localNotifier = localNotifier;
        _recurringIncomeRepository = recurringIncomeRepository;
        _recurringIncomeService = recurringIncomeService;
    }

    [RelayCommand]
    private async Task LoadDashboardAsync()
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
            // deactivating an occurrence stays exclusively on the Recurring Expenses screen. Hoisted
            // from the Net Worth tile below (was fetched there only) so this dictionary can resolve a
            // credit-card-backed recurring expense's account name without a second, redundant fetch.
            var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);
            var activeCreditAccounts = await _creditAccountRepository.GetActiveAsync();
            var creditAccountNames = activeCreditAccounts.ToDictionary(a => a.Id, a => a.Name);
            var dueSoonCutoff = today.AddDays(7);

            UpcomingPayments.Clear();
            foreach (var recurringExpense in recurringExpenses
                         .Where(r => r.IsDue(dueSoonCutoff))
                         .OrderBy(r => r.NextOccurrenceDate))
            {
                var categoryName = categoryNames.TryGetValue(recurringExpense.CategoryId, out var name) ? name : "?";
                var accountName = recurringExpense.IsCreditCardBacked
                    ? "💳 " + (creditAccountNames.TryGetValue(recurringExpense.CreditAccountId!.Value, out var cardName) ? cardName : "?")
                    : (accountNames.TryGetValue(recurringExpense.AccountId!.Value, out var accName) ? accName : "?");
                UpcomingPayments.Add(RecurringExpenseListItem.FromDomain(recurringExpense, categoryName, accountName));
            }

            // Real surplus tile (README §35) — "sobrante real": Tile 1's available balance minus known
            // upcoming obligations, per currency. Deliberately a WIDER horizon than Tile 5's 7-day
            // dueSoonCutoff above (through the end of the current calendar month instead) — a 7-day
            // window would show ~$0 upcoming for a monthly rent/subscription due in 12 days, making this
            // number barely different from raw balance for most of the month (see the slice spec's
            // Decision 1). Tile 5 itself is untouched; this is an additional, independently-windowed tile.
            var realSurplusHorizonEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            // Debt payments need the active cards' latest statements and this cycle's payments --
            // fetched once here (batched, the same calls CreditCardsListViewModel.LoadCreditCardsAsync
            // uses, not an N+1 per-card loop) and reused for every horizon ObligationsThrough is asked for.
            var activeCards = activeCreditAccounts.OfType<CreditCard>().ToList();
            var activeCardIds = activeCards.Select(c => c.Id).ToList();
            var latestStatementsForRealSurplus = await _statementRepository.GetLatestForCardsAsync(activeCardIds);
            var paymentsForRealSurplus = (await _transactionRepository.GetCreditCardPaymentsUpToDateForCreditAccountsAsync(today, activeCardIds))
                .GroupBy(p => p.CreditAccountId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Known obligations falling due on or before horizonEnd, per currency: recurring expenses
            // (upcoming) and loan payments / cards' remaining minimums (debt). Used for the real surplus
            // (month end) and for the half-month and month projections below.
            (Dictionary<CurrencyCode, decimal> Upcoming, Dictionary<CurrencyCode, decimal> Debt) ObligationsThrough(DateOnly horizonEnd)
            {
                // Upcoming expenses: grouped via the same accountCurrencies map Tile 2 built above,
                // mirroring SpendingCalculator's "resolve defensively, skip if unresolvable, never bucket
                // under a wrong/default currency" idiom rather than a plain GroupBy.
                var upcoming = new Dictionary<CurrencyCode, decimal>();
                foreach (var recurringExpense in recurringExpenses)
                {
                    // Not a plain IsDue() filter counting each expense once: a Weekly recurring expense
                    // can have several occurrences before horizonEnd, and each one is a real upcoming
                    // obligation against the same account.
                    var occurrenceCount = recurringExpense.CountOccurrencesThrough(horizonEnd);
                    if (occurrenceCount == 0)
                        continue;

                    var sourceAccountId = recurringExpense.IsCreditCardBacked
                        ? recurringExpense.CreditAccountId!.Value
                        : recurringExpense.AccountId!.Value;

                    if (!accountCurrencies.TryGetValue(sourceAccountId, out var currency))
                        continue; // Defensive: shouldn't happen (accountCurrencies is the complete map).

                    upcoming[currency] = upcoming.GetValueOrDefault(currency) + recurringExpense.Amount * occurrenceCount;
                }

                // Debt payments: every active Loan due by the horizon contributes its full
                // RequiredPayment (already forward-only -- AdvanceSchedule moves RequiredPayment and
                // NextPaymentDate together whenever a LoanPayment posts), plus every active CreditCard's
                // remaining minimum this cycle.
                var debt = new Dictionary<CurrencyCode, decimal>();
                foreach (var loan in activeCreditAccounts.OfType<Loan>().Where(l => l.NextPaymentDate <= horizonEnd))
                    debt[loan.Currency] = debt.GetValueOrDefault(loan.Currency) + loan.RequiredPayment;

                foreach (var card in activeCards)
                {
                    // NoStatementYet: contributes $0, never AmountOwed or a derived estimate -- "pago
                    // minimo" is always a user-entered, per-statement field, never derived (CLAUDE.md).
                    if (!latestStatementsForRealSurplus.TryGetValue(card.Id, out var statement))
                        continue;

                    var dueDate = card.GetPaymentDueDateForCycleEndingOn(statement.CycleEndDate);
                    if (dueDate > horizonEnd)
                        continue;

                    var paymentsMadeThisCycle = paymentsForRealSurplus.TryGetValue(card.Id, out var payments)
                        ? payments.Where(p => p.Date > statement.CycleEndDate).Sum(p => p.Amount)
                        : 0m;

                    // Floored at 0: a minimum already fully paid this cycle already left the available
                    // balance when it posted -- subtracting it again would double-count it.
                    var remainingMinimum = Math.Max(0m, statement.MinimumPayment - paymentsMadeThisCycle);
                    debt[card.Currency] = debt.GetValueOrDefault(card.Currency) + remainingMinimum;
                }

                return (upcoming, debt);
            }

            // Balance: Tile 1's already-computed available balance, reused directly -- not Net Worth's
            // "total assets" (a locked TermDeposit isn't money that can cover an upcoming bill; see the
            // slice spec's Decision 3).
            var availableBalanceByCurrency = Balances.ToDictionary(b => b.Currency, b => b.Total);

            // Real surplus tile (README §35): through month end, deliberately income-free.
            var (monthUpcoming, monthDebt) = ObligationsThrough(realSurplusHorizonEnd);
            var realSurplusSummary = _realSurplusCalculator.Calculate(availableBalanceByCurrency, monthUpcoming, monthDebt);

            RealSurplusByCurrency.Clear();
            foreach (var surplus in realSurplusSummary.ByCurrency.Values)
                RealSurplusByCurrency.Add(RealSurplusTileItem.FromDomain(surplus));

            // Projection tile: the same obligations through the end of this quincena and of this month,
            // plus recurring income expected by then. A separate tile, never merged into the real
            // surplus above -- expected income isn't available balance until it's confirmed.
            var recurringIncomes = await _recurringIncomeRepository.GetActiveAsync();
            var halfMonthEnd = RecurrenceDates.HalfMonthEnd(today);
            var (halfMonthUpcoming, halfMonthDebt) = ObligationsThrough(halfMonthEnd);
            var halfMonthSurplus = _realSurplusCalculator.Calculate(availableBalanceByCurrency, halfMonthUpcoming, halfMonthDebt);

            var halfMonthProjections = CashFlowProjectionCalculator.Project(
                    halfMonthSurplus,
                    CashFlowProjectionCalculator.ExpectedIncomeThrough(recurringIncomes, halfMonthEnd, accountCurrencies))
                .ToDictionary(p => p.Currency);
            var monthProjections = CashFlowProjectionCalculator.Project(
                    realSurplusSummary,
                    CashFlowProjectionCalculator.ExpectedIncomeThrough(recurringIncomes, realSurplusHorizonEnd, accountCurrencies))
                .ToDictionary(p => p.Currency);

            HalfMonthThroughText = string.Format(CultureInfo.CurrentCulture, AppResources.Dashboard_ProjectionThroughFormat, halfMonthEnd);
            MonthThroughText = string.Format(CultureInfo.CurrentCulture, AppResources.Dashboard_ProjectionThroughFormat, realSurplusHorizonEnd);

            Projections.Clear();
            foreach (var currency in monthProjections.Keys.Union(halfMonthProjections.Keys).OrderBy(c => c))
            {
                var empty = CashFlowProjection.From(RealSurplus.Calculate(currency, 0m, 0m, 0m), 0m);
                Projections.Add(new ProjectionTileItem(
                    currency,
                    halfMonthProjections.GetValueOrDefault(currency, empty),
                    monthProjections.GetValueOrDefault(currency, empty)));
            }

            // Upcoming income: every active recurring income whose next payday falls this month, with a
            // "got paid" button once it can be confirmed (due, or within the early-confirmation window).
            UpcomingIncomes.Clear();
            foreach (var recurringIncome in recurringIncomes
                         .Where(r => r.NextOccurrenceDate <= realSurplusHorizonEnd)
                         .OrderBy(r => r.NextOccurrenceDate))
            {
                UpcomingIncomes.Add(new UpcomingIncomeItem(
                    recurringIncome.Id,
                    recurringIncome.Name,
                    recurringIncome.Amount,
                    recurringIncome.NextOccurrenceDate,
                    recurringIncome.CanConfirm(today),
                    recurringIncome.NextOccurrenceDate > today));
            }

            // Net worth tile (README §24) — sum of ALL active FinancialAccount balances (regardless of
            // CountsAsAvailableBalance, unlike Tile 1 above) minus active CreditAccount debt, per
            // currency, never blended across currencies. Computed here directly from the active accounts
            // already fetched, plus the active-only credit account fetch hoisted above for Tile 5 (since
            // Tile 2 above needs ALL credit accounts, not active-only) so the tile never needs to re-read
            // the snapshot table that RecordSnapshotAsync below writes to.
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
            OnPropertyChanged(nameof(HasUpcomingIncomes));
            OnPropertyChanged(nameof(ShowUpcomingIncomesEmpty));
        }
        finally
        {
            _isLoading = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// "I got paid": confirms a recurring income's next occurrence from the Dashboard (posting the real
    /// Income), asking first when it's ahead of its scheduled date -- see RecurringIncomeConfirmPrompt.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmIncomeAsync(UpcomingIncomeItem? item)
    {
        if (item is null || !item.CanConfirm || IsBusy)
            return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!await RecurringIncomeConfirmPrompt.ProceedAsync(item.Name, item.Date, item.Amount, today))
            return;

        try
        {
            await _recurringIncomeService.ConfirmOccurrenceAsync(item.Id, today);
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlertAsync(
                AppResources.Dashboard_UpcomingIncomesSectionTitle,
                AppResources.RecurringIncomes_ConfirmError,
                AppResources.Dashboard_ConfirmIncomeErrorDismiss);
            return;
        }

        await LoadDashboardAsync();
    }

    [RelayCommand]
    private static async Task GoToAddTransactionAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddTransactionPage));
    }

    [RelayCommand]
    private static async Task GoToRecurringExpensesAsync()
    {
        // RecurringExpensesListPage is a pushed page (Routing.RegisterRoute in AppShell.xaml.cs), not a
        // Tab, since the bottom-nav-overflow fix moved it into Settings — same relative-route idiom as
        // GoToNetWorthHistoryAsync below. Previously an absolute "//RecurringExpensesList" route, back
        // when this page's ShellContent lived directly in the TabBar.
        await Shell.Current.GoToAsync(nameof(RecurringExpensesListPage));
    }

    [RelayCommand]
    private static async Task GoToNetWorthHistoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(NetWorthPage));
    }
}
