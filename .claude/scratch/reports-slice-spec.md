# README §40 "Reports" — Slice 1 scope

Scoped 2026-09-17. Companion in-flight work this round (app-lock security, CSV export) is explicitly untouched here.

## 1. Chart approach — DECIDED: option (b), hand-rolled proportional bars

Confirmed via `.csproj` review: no charting library exists in this codebase today. The Net Worth slice (Phase 3 slice 5) hit this same question and deliberately chose a plain list ("no charting library added... don't over-build past §24's terse wording"). §40's wording is equally terse — one sentence plus a bare bullet list, no mockup, no named chart types.

**Decision (confirmed by the coordinating session, low-risk/reversible default): option (b) — no charting library, hand-rolled proportional bars** (styled `Border`/`BoxView` rectangles sized proportional to value, using the app's existing "glossy" dark-theme visual language). This is meaningfully more visual than this app's existing plain lists (History, Transactions, every List page) with zero new dependency and zero licensing/cost risk. If proportional bars prove insufficient later, a follow-up slice can hand-roll real charts (no new dependency) or adopt a reviewed third-party package.

## 2. Slice 1 subset — 4 of 17 report types

All four are sourced from Application-layer building blocks that already exist and are already exercised (`SpendingCalculator`, `IncomeCalculator`, `BudgetEvaluator`, `ITransactionRepository.GetByDateRangeAsync`) — zero new domain accounting logic, only one small new interface.

1. **Expenses by category** — `SpendingCalculator.SpentByCategoryAndCurrency` (src/TrackTraceMoney.Application/Reporting/SpendingCalculator.cs) already computes this. New value: an arbitrary month/year picker instead of Dashboard's hardcoded "this month."
2. **Income vs. expenses** — `SpendingCalculator` + `IncomeCalculator` already drive Dashboard Tile 2 (`DashboardViewModel.cs` lines 143-150). New value: historical lookback via month picker instead of current-month-only.
3. **Monthly expenses trend** — genuinely new aggregation, but cheap: `GetByDateRangeAsync(from, to)` already accepts arbitrary ranges and is already exercised at up-to-a-year spans by `HistoryViewModel` (default filter `DateTime.Today.AddYears(-1)`..today, confirmed by reading `HistoryViewModel.cs`) — proven-safe query shape. Needs one new small interface (§4).
4. **Budget vs. spending** — `BudgetEvaluator.Evaluate` (src/TrackTraceMoney.Application/Budgets/BudgetEvaluator.cs) already computes actual-vs-budget per category+currency; Dashboard's tile only ever surfaces categories that are OVER budget. This report is the first place every budget (including comfortably-under ones) is shown side by side. Zero new calculation logic.

### Deferred / skipped (13 of 17), with reasons

- **Daily / Weekly expenses** — folded into a future granularity toggle on the Monthly trend report, not built as 2 more aggregators this slice.
- **Card purchases vs. payments** — already a dedicated screen/calculator (`ICreditCardPurchasedVsPaidCalculator`, Phase 2 slice 5). Later slice: link Reports hub to it, don't rebuild.
- **Debt evolution** — no persisted historical snapshot table exists for debt (unlike Net Worth's `NetWorthSnapshot`); real net-new infra, own slice.
- **Snowball** — existing Snowball planner screen already IS this report.
- **Net worth** — `NetWorthPage` already exists with its own evolution timeline, reachable from Dashboard (`DashboardViewModel.GoToNetWorthHistoryAsync`). Later slice: link, don't duplicate.
- **Medical expenses** — `MedicalExpenseDetail` exists per-transaction but no aggregate report exists yet (Phase 3 slices 6/7 deliberately deferred this).
- **Expenses by person** — `Payer/BeneficiaryPersonId` exist but `SpendingCalculator` has zero person-aware aggregation dimension today; needs a new calculator axis.
- **Insurance-covered expenses / Pending reimbursements / Reimbursements received** — underlying fields (`InsuranceCoveredAmount`, `MedicalExpenseDetail.Status`, `Reimbursement` transactions) exist but no aggregate query does; pair with deferred Medical report.
- **Investment returns** — `InvestmentFund.Gain`/`ReturnPercentage` computed per-fund already, but no aggregate "across all funds" report exists.
- **Surplus evolution** — `IRealSurplusCalculator`/`RealSurplusCalculator` are brand-new this session (freshly committed), compute a point-in-time value only, with no snapshot/evolution table (unlike Net Worth). Same net-new-infra category as Debt evolution. Defer to its own slice after that work stabilizes.

## 3. Screens

**One "Reports" hub page + one page per report type.** Hub lists the 4 slice-1 types as rows; each navigates to its own report page. Scales cleanly as later slices add more rows.

**Reachability: new button on `SettingsPage.xaml`**, mirroring the exact `GoToManageXxxCommand` + `Routing.RegisterRoute` pattern already used for Categories/Budgets/RecurringExpenses/People/Profiles/FinancialInstitutions/CardNetworks. Confirmed via `AppShell.xaml`: 5-tab structure (Dashboard, Accounts, Credit, Transactions, Settings), with an explicit in-file comment explaining these screens were deliberately moved off tabs because Android's Material bottom-nav auto-collapses a 6th+ tab into a native "More" sheet that ignores this app's dark theme — a bug already found and fixed once. Adding Reports as a 6th tab would reintroduce it. README §44's nav mockup lists Reports as a top-level item, but that mockup predates this app's actual restructuring; Settings-hub placement is correct for the codebase as it exists.

New files: `ReportsHubPage`/`ReportsHubViewModel`, `ExpensesByCategoryReportPage`, `IncomeVsExpensesReportPage`, `BudgetVsSpendingReportPage`, `MonthlyExpensesTrendReportPage` (+ ViewModels each).

## 4. Application-layer service shape

Mirrors `ISpendingCalculator`/`INetWorthCalculator`: pure calculators over already-fetched data, ViewModel does fetch/orchestration (same split as `DashboardViewModel`).

**3 of 4 report types need zero new interfaces** — direct reuse:
- Expenses by category → `ISpendingCalculator.Calculate(...).SpentByCategoryAndCurrency`
- Income vs. expenses → `ISpendingCalculator` + `IIncomeCalculator`, same shape as Dashboard Tile 2
- Budget vs. spending → `IBudgetEvaluator.Evaluate`, rendering every `BudgetStatus`, not filtering to `IsOverBudget`

**One new interface**, for Monthly expenses trend:

```csharp
namespace TrackTraceMoney.Application.Reporting;

public interface IMonthlySpendingTrendCalculator
{
    MonthlySpendingTrendSummary Calculate(
        IEnumerable<Transaction> transactions,
        IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies);
}

public sealed class MonthlySpendingTrendSummary
{
    // (Currency, Year, Month) keyed — never blended across currencies, same rule as every other Reporting DTO.
    public IReadOnlyDictionary<(CurrencyCode Currency, int Year, int Month), decimal>
        SpentByCurrencyAndMonth { get; init; } = new Dictionary<(CurrencyCode, int, int), decimal>();
}
```

Named `Monthly...` deliberately (not a `Granularity` enum with one implemented case) — daily/weekly are deferred, not designed-for-but-unbuilt; extending this later is a small explicit decision, not a premature abstraction now. Implementation reuses `SpendingCalculator`'s exact `CountsAsExpense`/`SpendAccountId` filtering logic, grouped by `(currency, transaction.Date.Year, transaction.Date.Month)` instead of just currency — leave exact internal shape to `dev`.

## 5. Per-currency handling

Confirmed unbroken precedent applies (CLAUDE.md, every existing Reporting DTO). Two presentation patterns, both already precedented:

- **Expenses by category / Income vs. expenses / Budget vs. spending** — single-period snapshots, mirror Dashboard: no currency picker, stack every currency group's bars on one page.
- **Monthly expenses trend** — multi-period history, mirror `NetWorthPage`: a currency picker + one chronological bar series for the selected currency — justified here specifically because stacking two currencies' bars on one trend axis would conflate unrelated scales in a way a single snapshot row doesn't.

## Explicit non-scope for slice 1

No export (§41, parallel scope this round). No app-lock changes (parallel scope this round). No new snapshot/history tables. No cross-currency totals, ever. Card purchases vs. payments / Snowball / Net worth get no duplicate Reports pages this slice — a later slice can add hub links to their existing screens.

## Files read while scoping

`README.md` (§40-45), `CLAUDE.md`, `src/TrackTraceMoney.App/AppShell.xaml`, `src/TrackTraceMoney.App/AppShell.xaml.cs`, `src/TrackTraceMoney.App/Views/SettingsPage.xaml`, `src/TrackTraceMoney.App/ViewModels/DashboardViewModel.cs`, `src/TrackTraceMoney.App/ViewModels/HistoryViewModel.cs`, `src/TrackTraceMoney.Application/Reporting/{ISpendingCalculator,SpendingCalculator,SpendingSummary,IIncomeCalculator,INetWorthCalculator}.cs`, `src/TrackTraceMoney.Application/Budgets/BudgetEvaluator.cs`, `src/TrackTraceMoney.Application/Abstractions/ITransactionRepository.cs`, and the project memory file `trackmoney_roadmap_progress.md`.
