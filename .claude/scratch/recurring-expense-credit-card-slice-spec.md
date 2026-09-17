# Slice spec: Recurring Expense credit-card payment source

Scoped by `product-owner`, 2026-09-16. Extends already-shipped Phase 1 (Recurring expenses, README §38)
+ Phase 2 (Credit cards, README §14-18) — no phase-boundary pull, both source phases are complete.
Single slice, not split further (small, cohesive, every layer's change is small).

**Spec anchor**: README §11 Expenses already lists "Credit card" as a valid payment method for a
one-time expense (worked example: "BAC Card"). §38 Recurring expenses' "Account" field is the
recurring analogue of that same payment-method concept. Reading §38's "Account" to include a credit
card is a direct, spec-consistent extension of §11's existing option, not invented scope. Confirmed
via user: no new "Subscriptions" screen, existing free-text `Name` field already handles multiple
same-provider subscriptions (e.g. "ChatGPT Personal"/"ChatGPT Trabajo"), `SystemCategoryKey.Subscriptions`
already seeded (`CategorySeeder.cs:29`) — zero change needed on either front.

## Decision 1 — domain shape: two nullable FKs + computed discriminator, not an enum/discriminator column

`RecurringExpense` (`src/TrackTraceMoney.Domain/RecurringExpenses/RecurringExpense.cs`) gets:
- `AccountId` becomes `Guid?` (was `Guid`)
- new `CreditAccountId` : `Guid?`
- new computed `IsCreditCardBacked => CreditAccountId is not null`
- private full constructor takes both nullable IDs, guards XOR (exactly one non-null, else throws `ArgumentException`)
- **existing public constructor is UNCHANGED** (still `Guid accountId`, still means FinancialAccount-backed) — zero diff to existing call sites/tests
- new `public static RecurringExpense ForCreditCard(string name, decimal amount, Guid categoryId, Guid creditAccountId, RecurringExpenseFrequency frequency, DateOnly startDate, DateOnly? endDate)` factory, delegates to the private constructor

Why two nullable FKs (not a `PaymentMethod` enum + single `Guid SourceId`): mirrors this codebase's
existing `FinancialAccount`/`CreditAccount` sibling-hierarchy split (Phase 2 decision: "deliberately
sibling to FinancialAccount not a subtype") — there is no shared base type/table to point a single FK
at, so a single polymorphic ID column would need its own discriminator anyway. Two typed nullable FKs
is the same "presence of a nullable field is the discriminator" idiom this domain already uses
elsewhere (e.g. `MedicalExpenseDetail`'s optional fields) — no new idiom introduced.

Why a new static factory, not a second constructor overload:
`RecurringExpense(string, decimal, Guid, Guid, ...)` for the FinancialAccount case and a hypothetical
credit-card overload would have an identical parameter-type signature (both take a trailing `Guid`) —
genuinely ambiguous/uncompilable as overloads. A named factory (`ForCreditCard`) also mirrors this
codebase's established one-method-per-scenario convention (`RecordExpenseAsync`/
`RecordCreditCardPurchaseAsync`, `RecordMedicalExpenseAsync`/`RecordMedicalCreditCardPurchaseAsync`)
rather than one always-both-params constructor.

XOR guard is defense-in-depth/documentation, not independently unit-testable through the public API —
both public entry points (constructor, `ForCreditCard`) already satisfy it by construction. Don't
force an artificial reflection-based test for the unreachable both-null/both-set states.

## Decision 2 — confirm-occurrence flow: branch on `IsCreditCardBacked`, otherwise untouched

`RecurringExpenseService.ConfirmOccurrenceAsync` (`src/TrackTraceMoney.Application/RecurringExpenses/RecurringExpenseService.cs`):
replace the single `RecordExpenseAsync` call with:

```csharp
if (recurringExpense.IsCreditCardBacked)
    await _transactionEntryService.RecordCreditCardPurchaseAsync(
        occurrenceDate, recurringExpense.Amount, recurringExpense.CreditAccountId!.Value,
        recurringExpense.CategoryId, null, null, recurringExpense.Name, null, ct);
else
    await _transactionEntryService.RecordExpenseAsync(
        occurrenceDate, recurringExpense.Amount, recurringExpense.AccountId!.Value,
        recurringExpense.CategoryId, null, null, recurringExpense.Name, null, ct);
```

Everything else (the `IUnitOfWork` transaction wrapping both the post and `MarkConfirmed`, the
ambient-scope requirement, rollback-on-catch) is untouched — same atomicity requirement either way.

`payerPersonId`/`beneficiaryPersonId` stay `null` on both branches — `RecurringExpense` never captured
Person fields, this is pure parity, not a new gap.

`TransactionEntryService.RecordCreditCardPurchaseAsync`'s existing `if (creditAccount is not CreditCard)
throw` guard (already in the file) already protects this call — a `Loan` can never reach it because the
picker (Decision 3) only ever offers `CreditCard`s. **Zero change needed to `TransactionEntryService`.**

**Confirmed, no code needed**: `CreditCardHealthEvaluator`
(`src/TrackTraceMoney.Application/CreditAccounts/CreditCardHealthEvaluator.cs`) and
`CreditCardPurchasedVsPaidCalculator` (`src/TrackTraceMoney.Application/Reporting/CreditCardPurchasedVsPaidCalculator.cs`)
both take plain `CreditCard`/`IEnumerable<Transaction>` — read directly, confirmed neither has any
origin-awareness (no tag/flag distinguishing a manually-entered `CreditCardPurchase` from a
recurring-expense-originated one). A confirmed occurrence is a real `CreditCardPurchase` row like any
other and flows into both the semáforo and purchased-vs-paid analysis automatically.

## Decision 3 — UI: one combined picker, mirroring `AddTransactionViewModel.PaymentAccounts` exactly

`AddRecurringExpenseViewModel` (`src/TrackTraceMoney.App/ViewModels/AddRecurringExpenseViewModel.cs`):
inject `ICreditAccountRepository`. `LoadOptionsAsync`'s existing `Accounts` collection becomes a
combined picker, same shape as `AddTransactionViewModel.PaymentAccounts`:

```csharp
foreach (var account in accounts.Where(a => a is not TermDeposit and not InvestmentFund))
    Accounts.Add(new NamedOption(account.Id, account.Name, account.Currency, IsCreditAccount: false));
foreach (var creditCard in creditAccounts.OfType<CreditCard>())
    Accounts.Add(new NamedOption(creditCard.Id, $"💳 {creditCard.Name}", creditCard.Currency, IsCreditAccount: true));
```

`OfType<CreditCard>()` (never `Loan`) — same filter as the Phase 2 checkpoint's finding #1 fix to
`AddTransactionViewModel.PaymentAccounts`/`CreditCardOptions`; don't reintroduce that bug here.

`SaveAsync`: after existing validation, branch on `SelectedAccount.IsCreditAccount` —
`RecurringExpense.ForCreditCard(...)` vs the existing `new RecurringExpense(...)`, mirroring
`AddTransactionViewModel.SaveAsync`'s own `if (SelectedAccount.IsCreditAccount) ... else ...`
Expense-block shape verbatim.

**Zero XAML change** — `AddRecurringExpensePage.xaml`'s existing
`<Picker ItemsSource="{Binding Accounts}" SelectedItem="{Binding SelectedAccount}" />` already renders
via `NamedOption.ToString()` (no `ItemDisplayBinding`), same as `PaymentAccounts`'s own binding. No new
resx keys — "Account" label stays as-is (matches `AddTransactionViewModel` not relabeling its own
combined picker to "Payment method" either).

One combined picker, not two pickers/a toggle: matches this codebase's own established convention for
"account or card" everywhere it already exists (`PaymentAccounts`), not a new pattern.

## Decision 4 — read-side name resolution: 2 call sites need a credit-account-aware branch

Both build a `Guid → name` dictionary from `IFinancialAccountRepository` only and would break (or
silently show "?") once `AccountId` goes nullable:

1. `RecurringExpensesListViewModel.LoadRecurringExpensesAsync`
   (`src/TrackTraceMoney.App/ViewModels/RecurringExpensesListViewModel.cs`) — inject
   `ICreditAccountRepository`, build a second `creditAccountNames` dict from `GetAllAsync()` (not
   `GetActiveAsync()` — matches the Phase 2 checkpoint finding #7 precedent: a deactivated card's
   still-listed recurring expense must still resolve a name, not show "?"). Branch:
   `recurringExpense.IsCreditCardBacked ? "💳 " + NameOf(creditAccountNames, recurringExpense.CreditAccountId!.Value) : NameOf(accountNames, recurringExpense.AccountId!.Value)`.
2. `DashboardViewModel.LoadDashboardAsync`'s Tile 5 ("upcoming payments", README §30 Q4)
   (`src/TrackTraceMoney.App/ViewModels/DashboardViewModel.cs`, ~lines 170-184) — same branch. This
   method already fetches active credit accounts slightly later for the Net Worth tile (~lines
   188-189) — hoist that fetch earlier and reuse it for Tile 5's dictionary instead of adding a second
   redundant fetch.

`RecurringExpenseListItem`/`DueRecurringExpenseListItem` themselves need **no change** — both already
just carry a plain resolved `AccountName` string; the 💳-prefix trick is folded into the string at the
ViewModel layer, same convention as `PaymentAccounts`'s own label-prefix approach.

## Pre-existing gap found while scoping — fix in this same slice

`AddRecurringExpenseViewModel.Accounts` (today, before this slice) is built from unfiltered
`_accountRepository.GetActiveAsync()` with **zero** `TermDeposit`/`InvestmentFund` exclusion — the
exact bug class the Post-Phase-3 checkpoint review already found and fixed in every
`AddTransactionViewModel` picker, but that checkpoint's own audit scope never included this
ViewModel's independent picker (confirmed: it's a structurally separate `ObservableCollection<NamedOption>`
in a different class, never touched by that review).

- **TermDeposit as source**: not silently corrupting today — `RecordExpenseAsync`'s existing
  `if (account is TermDeposit) throw` guard catches it at confirm-time, but the failure surfaces as
  `RecurringExpensesListViewModel.ConfirmAsync`'s generic catch-all error, forever, with no fix
  available (no Edit flow exists for `RecurringExpense` at all). A stuck, permanently-failing due item.
- **InvestmentFund as source**: genuinely corrupting today — confirmed by reading `InvestmentFund.cs`:
  `RecordContribution`/`RecordWithdrawal` just call the inherited non-virtual `FinancialAccount.Credit`/
  `Debit`, so `RecordExpenseAsync`'s plain `account.Debit(amount)` succeeds silently against an
  `InvestmentFund`, desyncing `Contributions`/`Withdrawals` (and therefore `Gain`/`ReturnPercentage`)
  permanently — the same bug shape CLAUDE.md names as this domain's #1 risk class, just never yet found
  in this specific picker.

**Decision: exclude both in the same `LoadOptionsAsync` edit this slice already requires**
(`accounts.Where(a => a is not TermDeposit and not InvestmentFund)`, already folded into Decision 3's
code snippet above) — same file, same picker, same bug class already being touched to add CreditCard
support; near-zero marginal cost; matches this project's own established precedent of proactively
fixing an adjacent same-file picker gap rather than deferring it (slice 1/3's own crash-prevention
fixes, the Phase 3 checkpoint's finding #1 fix).

## Migration

Real, non-TPH schema change on the standalone `RecurringExpenses` table (this entity isn't part of the
`Transaction` TPH hierarchy, so this is not a shared-column-reuse case): `RecurringExpenseConfiguration.cs`
drops `.IsRequired()` on `AccountId` (nullable now) and adds a bare
`builder.Property(r => r.CreditAccountId);` (nullable, no FK/navigation, no index — matches `AccountId`'s
own existing lack of either). **Migration review gate**: confirm the generated migration's `Up()` shows
exactly `AlterColumn<Guid>("AccountId", nullable: true)` + `AddColumn<Guid>("CreditAccountId", nullable: true)`
on `RecurringExpenses` only — no `Transactions`-table changes, no unrelated column touches.

## Tests

- **Domain** (`tests/TrackTraceMoney.Domain.Tests/RecurringExpenses/RecurringExpenseTests.cs`):
  `ForCreditCard` produces `IsCreditCardBacked == true`, `CreditAccountId` set, `AccountId` null;
  existing constructor still produces `IsCreditCardBacked == false` (regression).
- **Application** (`tests/TrackTraceMoney.Application.Tests/RecurringExpenses/RecurringExpenseServiceTests.cs`):
  `ConfirmOccurrenceAsync` on a credit-card-backed recurring expense calls `RecordCreditCardPurchaseAsync`
  not `RecordExpenseAsync` (assert via whichever test-double convention this file already uses for
  `ITransactionEntryService`) and results in the target `CreditCard.AmountOwed` increasing by `Amount` —
  directly proves no double-counting, the #1 risk this whole slice exists to get right. Existing
  bank-account-path test(s) must still pass unchanged (regression).
- **App-layer**: none — matches this codebase's established zero-App-test-coverage gap (build + manual
  review only), not a new exception to carve out here.
- **resx**: no new keys; confirm parity count unchanged (0 diff expected, not "N new keys each").

## Build order (per `add-domain-feature` skill)

Domain (`RecurringExpense.cs`) → Infrastructure (`RecurringExpenseConfiguration.cs` + migration) →
Application (`RecurringExpenseService.cs`; zero `TransactionEntryService.cs` change) → App
(`AddRecurringExpenseViewModel.cs`, `RecurringExpensesListViewModel.cs`, `DashboardViewModel.cs`).
Each layer independently compiles at every step per this repo's own layering discipline.

## Explicitly not in scope this slice

- No edit flow for an existing `RecurringExpense`'s payment source (none exists for any field today —
  pre-existing limitation, not introduced here).
- No per-occurrence override of payment source — fixed at definition-time, mirrors `AccountId`'s own
  existing fixed-at-creation behavior; no precedent anywhere in this codebase for a per-confirm field
  override.
- No new over-limit guard at confirm-time — `CreditCard.AvailableCredit`'s own doc comment already
  states over-limit "is allowed and expected in real life, so this is never validated against" (§14);
  already decided, not reopened here.
- No new "card is inactive" guard — the existing bank-account path has no equivalent active-check
  either (`GetByIdAsync` is unfiltered); matching existing behavior exactly rather than inventing a
  stricter rule for only the new path.
- No escalated questions to the user this slice — every decision above either cites a direct spec
  section, mirrors an existing code precedent by name, or is a low-risk/reversible default consistent
  with this project's own established product-owner-decides-without-escalation pattern.
