# Real surplus ("sobrante real") — slice spec

Scoped by `product-owner`, 2026-09-17, per README §35 (worked example at README.md:1075-1100) + CLAUDE.md's
"Non-obvious domain rules" (saldo-disponible-vs-sobrante-real) + the existing dormant scaffold at
`src/TrackTraceMoney.Application/Reporting/RealSurplus.cs`. **Status: scoped, not implemented — hand off to
`dev`.**

## Phase

**Phase 1 (MVP).** CLAUDE.md's own "Roadmap phase boundaries" section lists "real surplus" by name in the
Phase 1 bullet list — this is not a scope pull from a later phase, it's finishing already-scoped Phase 1 work
that shipped as a formula-only stub. The debt-payments component reads already-shipped Phase 2 entities
(`CreditCard`, `Loan`, `CreditCardStatement`) but introduces nothing new from Phase 2 — it only consumes data
those slices already built and shipped (confirmed complete per `trackmoney_roadmap_progress.md`). No
descope/defer/accept question for the user here.

## What exists today (read directly, not assumed)

- `RealSurplus.cs` — bare record, `Calculate(decimal availableBalance, decimal upcomingObligations)`, one
  blended obligations number, no currency field, zero callers anywhere in the codebase.
- Dashboard's Tile 1 (`DashboardViewModel.cs:112-119`) — available balance per currency, `accounts.Where(a =>
  a.CountsAsAvailableBalance).GroupBy(a => a.Currency)`, active accounts only.
- Dashboard's Tile 5 (`DashboardViewModel.cs:170-190`) — recurring expenses due within `today.AddDays(7)`, a
  "heads up" reminder list, read-only.
- Dashboard's Net Worth tile (`DashboardViewModel.cs:192-202`, `DashboardPage.xaml:52-84`) — the only other
  "derived number, breakdown display, per currency" tile; closest UI/service-shape precedent.
- `INetWorthCalculator`/`NetWorthCalculator`/`NetWorthSummary` (`Application/Reporting/`) — pure aggregator
  pattern precedent, `AddSingleton` in `MauiProgram.cs:75`.
- `CreditCardHealthEvaluator.cs` — confirms `NoStatementYet` is a legitimate, already-modeled "no data" state
  for a card (short-circuits before computing anything from a missing statement), and confirms the existing
  `$0`-minimum edge case is about delinquency status, not dollar amounts (see Decision 2).
- `CreditCardsListViewModel.LoadCreditCardsAsync` (`App/ViewModels/CreditCardsListViewModel.cs:45-98`) — the
  exact, already-shipped "payments made this cycle" formula and the exact batch repository calls this slice
  reuses verbatim (`ICreditCardStatementRepository.GetLatestForCardsAsync`,
  `ITransactionRepository.GetCreditCardPaymentsUpToDateForCreditAccountsAsync`).
- Grepped `src/` for `Scheduled`/`one-time`/`unconfirmed` — no "scheduled-but-unconfirmed one-time expense"
  entity exists anywhere in this codebase. Only `RecurringExpense` has a due-occurrence workflow.

## Decisions

### 1. "Upcoming expenses": entities + window

**Entities: `RecurringExpense` only.** Confirmed (see above) there is no scheduled-but-unconfirmed one-time
expense concept in this codebase to also include — nothing else to add even if we wanted to.

**Window: due on or before the end of the current calendar month — NOT Tile 5's 7-day `dueSoonCutoff`.**
Deliberately different windows for the two tiles:
- README §30 frames Real Surplus as answering "Do I have a surplus?" — a number people budget against, not
  a reminder list. Most recurring obligations in this app are monthly (rent, subscriptions, installments); a
  7-day window would show `$0` upcoming for a $600 rent due in 12 days, making "Real surplus" barely
  different from raw balance for most of the month — directly defeating CLAUDE.md's explicit warning not to
  conflate the two.
- "Calendar month" is this app's own pre-existing, pervasive time-bucketing convention (Budgets are monthly;
  Tile 2's income/expense summary is month-to-date) — lower-risk and more consistent than inventing a new
  rolling-30-day concept with no other precedent anywhere in this codebase.
- Accepted, documented limitation: the obligations total will shrink near month-end (day 28 sees almost
  nothing; day 1 sees nearly a full month) even though next-month's bills are just as "known." Not treated as
  a blocker — same class of accepted edge behavior as other shipped slices (e.g. TermDeposit auto-renewal's
  drained-deposit case), not escalated further.
- Implementation: reuse the exact `RecurringExpense.IsDue(DateOnly asOf)` method that already exists
  (`RecurringExpense.cs:137-140`, no lower bound, so an already-overdue item still counts) — just call it
  with a new cutoff, `realSurplusHorizonEnd = new DateOnly(today.Year, today.Month,
  DateTime.DaysInMonth(today.Year, today.Month))`, instead of Tile 5's `dueSoonCutoff`. Zero Domain changes.
- Source: reuse the *same* `recurringExpenses` collection `LoadDashboardAsync` already fetches
  (`_recurringExpenseRepository.GetActiveAsync()`, line 110) for Tile 5 — just re-filter it a second time with
  the new cutoff. Zero new repository calls.
- Currency: `RecurringExpense` has no `Currency` field of its own. Resolve via the `accountCurrencies` map
  already built at `DashboardViewModel.cs:129` (`AccountCurrencyMapBuilder.Build(allAccounts,
  allCreditAccounts)`) keyed by `r.IsCreditCardBacked ? r.CreditAccountId!.Value : r.AccountId!.Value`. Reuse
  the existing map, don't build a new one.

### 2. "Debt payments": loans + cards

**Loans: every active `Loan` (from `activeCreditAccounts.OfType<Loan>()`, already fetched at
`DashboardViewModel.cs:176` for Tile 5) whose `NextPaymentDate <= realSurplusHorizonEnd` contributes its full
`RequiredPayment`.** No netting against "already paid" needed: `Loan.RequiredPayment`/`NextPaymentDate` are
atomically advanced together by `AdvanceSchedule` every time a `LoanPayment` is recorded (confirmed via
`Loan.cs:149-163`'s own doc comments — "Called by LoanPayment's recording flow... both values are supplied by
the caller"), so the field is *already* forward-only ("the next amount due, not yet paid") by construction —
unlike a card's statement-level `MinimumPayment`, which does not self-advance.

**Cards: every active `CreditCard` (from `activeCreditAccounts.OfType<CreditCard>()`) *with* a recorded
latest statement** whose computed due date (`card.GetPaymentDueDateForCycleEndingOn(statement.CycleEndDate)`)
falls `<= realSurplusHorizonEnd` contributes `Math.Max(0m, statement.MinimumPayment -
paymentsMadeThisCycle)`, where `paymentsMadeThisCycle` is the exact formula already shipped in
`CreditCardsListViewModel.cs:78-80` (`payments.Where(p => p.Date > statement.CycleEndDate).Sum(p =>
p.Amount)`), fed by the same two existing batch repository methods that ViewModel already uses:
`ICreditCardStatementRepository.GetLatestForCardsAsync(cardIds)` and
`ITransactionRepository.GetCreditCardPaymentsUpToDateForCreditAccountsAsync(today, cardIds)`. **Zero new
repository/Infrastructure methods needed** — both already exist specifically because the Phase 2 checkpoint
review had this codebase fix their N+1 predecessors.

Netting against `paymentsMadeThisCycle` (floored at 0 via `Math.Max`) is necessary here, unlike loans: if the
user already paid this cycle's minimum, that money already left the available balance the moment the payment
posted — subtracting the full `MinimumPayment` again would double-count it and understate the surplus.
Flooring at 0 also means an overpaid/paid-in-full card never contributes a *negative* debt-payment (which
would nonsensically inflate the surplus for money that's already gone).

**`NoStatementYet` cards (no recorded `CreditCardStatement` at all) are excluded entirely — contribute
`$0`, not `AmountOwed` or any estimate.** There is no known minimum payment amount for such a card.
Inventing one would violate CLAUDE.md's explicit rule that "pago mínimo" is always user-entered, never
derived — and mirrors `CreditCardHealthEvaluator`'s own precedent of short-circuiting into a distinct
"no data" state rather than guessing (`CreditCardHealthEvaluator.cs:33-44`). This is a real, accepted
limitation (a freshly-added card with real debt but no statement yet will understate obligations) — same
"known obligations only" framing CLAUDE.md's own doc comment on `RealSurplus.cs` already uses.

**Deliberately NOT reusing `CreditCardHealthEvaluator`'s `$0`-minimum special case** (`paidAtLeastMinimum =
... : card.AmountOwed <= 0` at `CreditCardHealthEvaluator.cs:51-53`). That logic answers "is this card
delinquent" (a status question); this calculation answers "how many known dollars are owed" (a math
question). A genuinely recorded `$0` minimum has no known dollar obligation to subtract here, regardless of
`AmountOwed` — using the evaluator's fallback would silently pull in a derived-from-`AmountOwed` number,
which is exactly what CLAUDE.md says never to do for a minimum payment.

Loan and card contributions are blended into one `DebtPayments` total per currency — matches README §35's own
worked example, which shows one blended "Debt payments" line, not separate loan/card lines.

### 3. Per-currency + which "balance"

**Per-currency: yes, unconditionally, no exception.** Matches every existing Dashboard tile (Tile 1, Tile 2,
Net Worth) and CLAUDE.md's hard rule against summing/converting across currencies, generalized here exactly
as it was for Net Worth (2026-09-10 decision, `trackmoney_roadmap_progress.md`). No FX mechanism, ever.

**Balance: Tile 1's "available balance" (`CountsAsAvailableBalance`-filtered, active `FinancialAccount`s),
NOT Net Worth's "total assets."** README §35's own example says "Current balance" in the context of what's
left over to allocate — spendable money, not locked-up net worth. CLAUDE.md pairs "saldo disponible"
(available balance) with "sobrante real" (real surplus) as the two concepts to keep distinct, which only
makes sense if sobrante real *refines* saldo disponible (available balance minus obligations), not patrimonio
neto (net worth). A locked `TermDeposit` balance isn't money that can cover an upcoming bill — including it
would reintroduce exactly the overstatement `CountsAsAvailableBalance` was invented to prevent for Tile 1.
Implementation: reuse Tile 1's already-computed `Balances` collection directly
(`Balances.ToDictionary(b => b.Currency, b => b.Total)`) — zero recomputation, zero new account-filtering
logic.

A currency that has debt but zero available-balance accounts (e.g. a USD loan with no USD cash account) must
still produce a row with `AvailableBalance = 0` and a negative result — this is a correct, valuable signal
("you owe in a currency you hold no cash in"), not an edge case to suppress. Mirrors Net Worth's existing
no-floor, union-of-all-currency-keys behavior.

### 4. Application-layer service shape

**Reshape the existing `RealSurplus.cs` in place** (its current 2-arg, no-currency shape cannot support
per-currency breakdown display — flagging explicitly since it's easy to assume "already exists" means "leave
the signature alone"). Zero existing callers (confirmed), so this is a safe, non-breaking reshape:

```csharp
// src/TrackTraceMoney.Application/Reporting/RealSurplus.cs — same file, new shape, same folder as
// NetWorthSummary.cs holds both NetWorthSummary + NetWorthByCurrency together; mirror that here.
public sealed record RealSurplus(
    CurrencyCode Currency, decimal AvailableBalance, decimal UpcomingExpenses, decimal DebtPayments, decimal Amount)
{
    public static RealSurplus Calculate(CurrencyCode currency, decimal availableBalance, decimal upcomingExpenses, decimal debtPayments) =>
        new(currency, availableBalance, upcomingExpenses, debtPayments, availableBalance - upcomingExpenses - debtPayments);
}

public sealed class RealSurplusSummary
{
    public IReadOnlyDictionary<CurrencyCode, RealSurplus> ByCurrency { get; init; } = new Dictionary<CurrencyCode, RealSurplus>();
}
```

**New `IRealSurplusCalculator`/`RealSurplusCalculator`** (own files, mirroring `INetWorthCalculator.cs`/
`NetWorthCalculator.cs`'s file split exactly):

```csharp
// IRealSurplusCalculator.cs
public interface IRealSurplusCalculator
{
    RealSurplusSummary Calculate(
        IReadOnlyDictionary<CurrencyCode, decimal> availableBalanceByCurrency,
        IReadOnlyDictionary<CurrencyCode, decimal> upcomingExpensesByCurrency,
        IReadOnlyDictionary<CurrencyCode, decimal> debtPaymentsByCurrency);
}
```

Deliberately simpler than `INetWorthCalculator` (which takes raw entity lists): Real Surplus's three inputs
come from three structurally different sources with genuinely different filtering rules (recurring-expense
due-date filtering + currency-map lookup; loan due-date filtering; card statement/payment/due-date lookup)
that are real domain logic, not pure aggregation — that filtering belongs colocated with Tile 1/2/5's
existing equivalent gather-and-filter code already living in `DashboardViewModel`, not hidden inside a
"calculator" that's supposed to be a dumb aggregator (mirrors `INetWorthCalculator`'s own stated design
stance verbatim — "a dumb aggregator... it does not filter"). `RealSurplusCalculator.Calculate` itself is
just a 3-way `Union` of currency keys + `GetValueOrDefault` + `RealSurplus.Calculate`, structurally identical
to `NetWorthCalculator.Calculate`'s existing 2-way union.

No new orchestrating service (no `IRealSurplusService` parallel to `INetWorthSnapshotService`): unlike net
worth, there's no persistence/evolution requirement here (see Decision 5 / non-scope) and no second call site
anywhere else in the app — the gather-inputs-and-call-the-calculator logic lives directly in
`DashboardViewModel.LoadDashboardAsync`, exactly where Tile 1/2/5's equivalent logic already lives.

**DI**: `builder.Services.AddSingleton<IRealSurplusCalculator, RealSurplusCalculator>();` in `MauiProgram.cs`,
next to `INetWorthCalculator`'s registration (~line 75) — pure/stateless, same lifetime as every other
Reporting calculator.

**Zero Domain-layer changes anywhere in this slice** — `RecurringExpense.IsDue`, `Loan.NextPaymentDate`,
`CreditCard.GetPaymentDueDateForCycleEndingOn` all already exist and already accept/expose exactly what's
needed. **Zero Infrastructure-layer changes** — both credit-card batch repository methods already exist. This
is an Application + App(ViewModel/View/Models/Resources)-only slice; confirms it's independently shippable
without touching Domain/Infrastructure at all.

### 5. UI

**New Dashboard tile, breakdown style** (4 rows: Available balance / Upcoming expenses / Debt payments /
**Real surplus**, last row bold), mirroring the Net Worth tile's exact `DataTemplate` structure
(`DashboardPage.xaml:55-84`) row-for-row, per the task's own steer and no reason found to deviate.

**Placement: last tile in `DashboardPage.xaml`, immediately after the existing Tile 5 (upcoming payments).**
Directly justified by README §30's own question order: Q4 "What do I need to pay?" (Tile 5) is immediately
followed by Q5 "Do I have a surplus?" (this new tile) — not an arbitrary placement.

**Comment style: unnumbered** ("Real surplus tile", no "Tile N"), mirroring how the *other* derived/computed
tile (Net Worth) is also unnumbered while primary-data tiles carry §30-derived numbers. Internally
consistent: both computed tiles are unnumbered, both primary-data tiles are numbered.

**No embedded horizon-date caption, no in-UI disclosure of the `NoStatementYet` exclusion.** Matches this
Dashboard's existing precedent — no tile explains its own inclusion/exclusion methodology in UI copy (Tile
2's title is the static "This month", no exact date; Tile 1 doesn't disclose that TermDeposits are excluded).
Consistent, avoids scope creep past README §35's terse wording (same "don't over-build" stance the Net Worth
slice took for §24).

**Obligation rows shown as plain positive numbers (`{0:N2}`), no inserted minus sign** — deviates from
README's literal example text ("-$200") in favor of matching the Net Worth tile's own Liabilities-row
convention (plain positive number despite being subtracted), for cross-tile visual consistency within this
app. The row *labels* ("Upcoming expenses", "Debt payments") already communicate the subtraction, same as
Net Worth's "Liabilities" label does today.

**New model** `App/Models/RealSurplusTileItem.cs`, mirroring `NetWorthTileItem.cs` exactly:

```csharp
public sealed record RealSurplusTileItem(CurrencyCode Currency, decimal AvailableBalance, decimal UpcomingExpenses, decimal DebtPayments, decimal Amount)
{
    public static RealSurplusTileItem FromDomain(RealSurplus surplus) =>
        new(surplus.Currency, surplus.AvailableBalance, surplus.UpcomingExpenses, surplus.DebtPayments, surplus.Amount);
}
```

**`DashboardViewModel` changes**:
- New constructor dependency: `ICreditCardStatementRepository` (not currently injected in this ViewModel).
- New Application dependency: `IRealSurplusCalculator`.
- New `public ObservableCollection<RealSurplusTileItem> RealSurplusByCurrency { get; } = [];`
- New orchestration block in `LoadDashboardAsync`, placed after the existing Tile 5 block, that: computes
  `realSurplusHorizonEnd`; re-filters the already-fetched `recurringExpenses` and groups by currency via the
  already-built `accountCurrencies` map; filters the already-fetched `activeCreditAccounts` into due loans
  and cards-with-statements via the two existing batch repository calls; calls
  `_realSurplusCalculator.Calculate(...)`; populates `RealSurplusByCurrency`.
- No new `ShowXEmpty`/`HasX` computed booleans — mirrors Tile 1/Net Worth's precedent of no empty-state
  handling (only Tile 3/Tile 5 have one, and both are named lists of items, not currency breakdown cards).

**New resx keys** (`AppResources.resx` = Spanish default, `AppResources.en.resx` = English — confirmed via
existing key inspection), 5 keys, both files:

| Key | ES (default) | EN |
|---|---|---|
| `Dashboard_RealSurplusSectionTitle` | Sobrante real | Real surplus |
| `Dashboard_RealSurplusBalanceLabel` | Saldo disponible | Available balance |
| `Dashboard_RealSurplusUpcomingExpensesLabel` | Gastos próximos | Upcoming expenses |
| `Dashboard_RealSurplusDebtPaymentsLabel` | Pagos de deuda | Debt payments |
| `Dashboard_RealSurplusResultLabel` | Sobrante real | Real surplus |

`Dashboard_RealSurplusBalanceLabel` is deliberately *not* a reuse of the existing `Dashboard_AvailableLabel`
("Disponible"/"Available") — that key already names a conceptually different Tile 2 figure (this month's
income-minus-expenses leftover). Reusing the same short word for two different numbers on the same screen
risks direct confusion between adjacent tiles; "Available balance"/"Saldo disponible" is CLAUDE.md's own
paired term for this exact concept, used verbatim.
`Dashboard_RealSurplusResultLabel` intentionally duplicates the section title's text, mirroring
`Dashboard_NetWorthSectionTitle`/`Dashboard_NetWorthLabel`'s existing identical-text precedent.

## Explicit non-scope

- **README §36 "Recommendations for the surplus"** (snowball/emergency-fund/savings/investment/distribute
  suggestions) — a separate, later feature. §35 and §36 are adjacent but distinct sections; this spec covers
  §35 only.
- **No Real Surplus history/evolution/snapshot table**, unlike Net Worth. §35's wording has no "must be
  possible to view evolution" language the way §24 does — this is a point-in-time Dashboard number only. No
  persistence, no recording service, no trend screen. A future ask for a Real Surplus trend is a new,
  separate scope decision, not implied by this one.
- **Tile 5 is untouched** — its 7-day window/behavior stays exactly as shipped. Real Surplus is an additional
  tile with its own wider horizon, not a replacement or a refactor of Tile 5.
- **No new Infrastructure, no EF Core migration.** This feature adds no entity, no column, no table — it only
  reads existing data through repository methods that already exist. A generated migration in this slice's
  diff would indicate something went wrong.
- **No FX conversion / cross-currency netting**, matching every other multi-currency figure in this app.
- **No conditional visual styling for a negative surplus** (e.g. red text) — no existing tile in this
  Dashboard does per-value conditional styling within a breakdown card; the overall financial-health tile
  already serves as the at-a-glance risk indicator.

## Slicing decision

**Single slice, not broken down further.** Already small and self-contained (2 new Application files + 1
modified Application file + 1 new App model + `DashboardViewModel`/`DashboardPage.xaml`/`MauiProgram.cs`
changes + resx + tests), needs zero Domain/Infrastructure work, and has no natural partial-value cut point —
shipping the calculator without wiring it into the Dashboard would leave the feature exactly as dormant as it
is today, which is the problem this task exists to fix.

## Testing expectations

- New `tests/TrackTraceMoney.Application.Tests/Reporting/RealSurplusCalculatorTests.cs` (mirrors the existing
  `NetWorthCalculatorTests.cs` in the same folder): union-of-currency-keys behavior (a currency present in
  only one of the three inputs still produces a row via `GetValueOrDefault`), arithmetic correctness
  (`Amount = AvailableBalance - UpcomingExpenses - DebtPayments`), no floor on a negative result (mirrors Net
  Worth's own no-floor precedent — insolvency for a currency is a legitimate state to surface).
- No new `DashboardViewModel` test — this codebase has zero App-layer ViewModel test coverage for *any*
  Dashboard tile today (a standing, already-accepted gap per `trackmoney_roadmap_progress.md`, not a new gap
  introduced by this slice). Build + manual/live verification only, consistent with every other Dashboard
  tile shipped so far.
- resx parity check: both `.resx` files must gain the same 5 keys (this codebase's standard check on every
  localized-string-adding slice).

## Acceptance criteria

1. Given a profile with a $1,000 USD available balance (per Tile 1's own definition), a $200 USD recurring
   expense due within the current calendar month, and a $250 USD combined loan-required-payment +
   card-remaining-minimum due within the same month, the Dashboard's Real Surplus tile shows Available
   balance $1,000.00 / Upcoming expenses $200.00 / Debt payments $250.00 / Real surplus $550.00 — matching
   README §35's own worked example exactly.
2. A recurring expense due in 20 days (outside Tile 5's 7-day window but inside the current calendar month)
   is excluded from Tile 5 but included in the Real Surplus tile's Upcoming expenses figure.
3. A credit card with `NoStatementYet` (no `CreditCardStatement` ever recorded) contributes `$0` to Debt
   payments regardless of its `AmountOwed`.
4. A credit card whose recorded minimum payment was already fully paid this cycle contributes `$0` (not a
   negative number) to Debt payments.
5. A currency with active debt (loan or card) but zero available-balance accounts in that currency still
   produces a row, with `AvailableBalance = 0.00` and a negative Real Surplus.
6. Every figure on the tile is per-currency; a profile with both USD and MXN activity shows two separate
   rows, never a blended total.
7. `dotnet build` is clean (0 warnings/0 errors) and no EF Core migration is generated.
8. resx key parity holds (same key set, both languages) after the 5 new keys are added.
