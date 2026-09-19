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
        ILocalNotifier localNotifier)
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

            // Upcoming expenses: same RecurringExpense collection Tile 5 already fetched, re-filtered
            // with the wider horizon and grouped by currency via the same accountCurrencies map Tile 2
            // built above. Mirrors SpendingCalculator's own "resolve defensively, skip if unresolvable,
            // never bucket under a wrong/default currency" idiom rather than a plain GroupBy, since an
            // unresolvable account must never silently misattribute an amount to the wrong currency.
            var upcomingExpensesByCurrency = new Dictionary<CurrencyCode, decimal>();
            foreach (var recurringExpense in recurringExpenses)
            {
                // Not a plain IsDue() filter counting each expense once: a Weekly recurring expense can
                // have several occurrences between now and month-end, and each one is a real upcoming
                // obligation against the same account. Found via a checkpoint code review -- IsDue()-only
                // silently undercounted Weekly (and, in a short month, some Monthly-near-boundary) expenses.
                var occurrenceCount = recurringExpense.CountOccurrencesThrough(realSurplusHorizonEnd);
                if (occurrenceCount == 0)
                    continue;

                var sourceAccountId = recurringExpense.IsCreditCardBacked
                    ? recurringExpense.CreditAccountId!.Value
                    : recurringExpense.AccountId!.Value;

                if (!accountCurrencies.TryGetValue(sourceAccountId, out var currency))
                    continue; // Defensive: shouldn't happen (accountCurrencies is the complete map).

                upcomingExpensesByCurrency[currency] = upcomingExpensesByCurrency.GetValueOrDefault(currency) + recurringExpense.Amount * occurrenceCount;
            }

            // Debt payments: every active Loan due by the horizon contributes its full RequiredPayment
            // (already forward-only/not-yet-paid by construction — AdvanceSchedule advances both
            // RequiredPayment and NextPaymentDate together every time a LoanPayment posts, so no netting
            // needed here, unlike cards below) plus every active CreditCard's remaining minimum this
            // cycle. Reuses CreditCardsListViewModel.LoadCreditCardsAsync's exact batched formula/repository
            // calls verbatim (see that ViewModel) rather than a new N+1 per-card loop.
            var debtPaymentsByCurrency = new Dictionary<CurrencyCode, decimal>();
            foreach (var loan in activeCreditAccounts.OfType<Loan>().Where(l => l.NextPaymentDate <= realSurplusHorizonEnd))
                debtPaymentsByCurrency[loan.Currency] = debtPaymentsByCurrency.GetValueOrDefault(loan.Currency) + loan.RequiredPayment;

            var activeCards = activeCreditAccounts.OfType<CreditCard>().ToList();
            var activeCardIds = activeCards.Select(c => c.Id).ToList();
            var latestStatementsForRealSurplus = await _statementRepository.GetLatestForCardsAsync(activeCardIds);
            var paymentsForRealSurplus = (await _transactionRepository.GetCreditCardPaymentsUpToDateForCreditAccountsAsync(today, activeCardIds))
                .GroupBy(p => p.CreditAccountId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var card in activeCards)
            {
                // NoStatementYet: contributes $0, never AmountOwed or a derived estimate — "pago mínimo"
                // is always a user-entered, per-statement field, never derived (CLAUDE.md).
                if (!latestStatementsForRealSurplus.TryGetValue(card.Id, out var statement))
                    continue;

                var dueDate = card.GetPaymentDueDateForCycleEndingOn(statement.CycleEndDate);
                if (dueDate > realSurplusHorizonEnd)
                    continue;

                var paymentsMadeThisCycle = paymentsForRealSurplus.TryGetValue(card.Id, out var payments)
                    ? payments.Where(p => p.Date > statement.CycleEndDate).Sum(p => p.Amount)
                    : 0m;

                // Floored at 0: if the minimum was already fully paid this cycle, that money already
                // left the available balance the moment it posted — subtracting the full MinimumPayment
                // again would double-count it, and a negative contribution would nonsensically inflate
                // the surplus for money that's already gone.
                var remainingMinimum = Math.Max(0m, statement.MinimumPayment - paymentsMadeThisCycle);
                debtPaymentsByCurrency[card.Currency] = debtPaymentsByCurrency.GetValueOrDefault(card.Currency) + remainingMinimum;
            }

            // Balance: Tile 1's already-computed available balance, reused directly — not Net Worth's
            // "total assets" (a locked TermDeposit isn't money that can cover an upcoming bill; see the
            // slice spec's Decision 3). Zero recomputation, zero new account-filtering logic.
            var availableBalanceByCurrency = Balances.ToDictionary(b => b.Currency, b => b.Total);

            var realSurplusSummary = _realSurplusCalculator.Calculate(availableBalanceByCurrency, upcomingExpensesByCurrency, debtPaymentsByCurrency);

            RealSurplusByCurrency.Clear();
            foreach (var surplus in realSurplusSummary.ByCurrency.Values)
                RealSurplusByCurrency.Add(RealSurplusTileItem.FromDomain(surplus));

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
        }
        finally
        {
            IsBusy = false;
        }
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
