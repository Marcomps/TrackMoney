# Transaction Type Customization — Slice Spec

Scoped by `product-owner`, 2026-09-21. Origin: user's clarifying answer to "what do you want?" —
**"mejorar categorías, nuevos tipos y personalizados"** (improve categories, new types, and
customized ones). Per `MEMORY.md`'s own framing (2026-09-19 entry, quoted verbatim below), this
is explicitly NOT yet scoped and NOT started — this is the first real scoping pass.

> Needs a proper `product-owner` scoping pass (e.g., "custom types" might really mean "let the
> user define sub-kinds of Expense/Income with their own default category+icon", not "let the
> user invent a type with untested new accounting semantics") before any implementation starts.

**Roadmap placement**: not in README §51 at all — like Recurring Income and the
FinancialInstitution/CardNetwork migration, this is a **README-adjacent, out-of-roadmap user
request**, not a numbered Phase 1-5 item. README §12 says only "Categories must be customizable"
(one sentence, no mention of icons/color/hierarchy/types) and README §9-11/§45 define
`TransactionType`'s members exhaustively with no mention of user-defined types anywhere. Nothing
here pulls forward Phase 2/3/4/5 scope (no cards/loans/investment/cloud concerns touched) — this
is a Phase-1-adjacent quality-of-life slice, sequenced independently of the phase roadmap the same
way Recurring Income was.

---

## 0. Why this splits into two genuinely different problems

Read `TransactionEntryService.cs` in full (all 10 `RecordXAsync` methods) and built the complete
dispatch table below. The finding that drives this entire spec: **every `TransactionType` member
is a distinct code path with its own money-movement semantics, hand-verified against this
codebase's #1 documented risk (double-counting).** `Category`, by contrast, has zero behavior —
it's `Name` + `SystemKey` + `Icon`, read by nothing except display-string lookups. These are not
the same kind of "customization" and must not be designed as if they were.

### Complete `TransactionType` → behavior table (from `TransactionEntryService.cs`)

| Type | Money movement | Counts as spend? | Counts as income? | Other side effects | User-picked in top-level Picker? |
|---|---|---|---|---|---|
| `Expense` | Debit 1 account | Yes | No | Budget-crossing notify | Yes |
| `Income` | Credit 1 account | No | Yes | — | Yes |
| `Transfer` | Debit A, Credit B | No | No | Cross-currency guard | Yes |
| `CreditCardPurchase` | Charge card (`RegisterCharge`) | Yes | No | Budget-crossing notify | **No** — auto-routed from the Expense block when payment method = card (§11 "Payment method") |
| `CreditCardPayment` | Debit account, reduce card debt (`RegisterPayment`) | No | No | Overpayment guard | Yes |
| `LoanPayment` | Debit account, reduce loan debt, advance schedule | No | No | User supplies next-payment-date/required-payment (never derived) | Yes |
| `InvestmentContribution` | Debit account, credit fund (`RecordContribution`) | No | No | Cross-currency guard | Yes |
| `InvestmentWithdrawal` | Debit fund (`RecordWithdrawal`), credit account | No | No | No overdraft guard (deliberate) | Yes |
| `InterestIncome` | Credit term deposit + its `InterestReceived` total | No | **Yes** | — | **No** — auto-routed from the Income block when destination = a term deposit |
| `Reimbursement` | Credit account, `MarkReimbursed` on linked `MedicalExpenseDetail` | No | **Yes** (deliberate "+", nets against the original Expense) | Guarded to `Pending`-status medical expenses only | Yes |

Two members (`CreditCardPurchase`, `InterestIncome`) are **already** excluded from the top-level
type picker and auto-detected from context — confirmed directly in
`AddTransactionViewModel.cs:220-229`'s own doc comment. This is the existing, working precedent
for "a user doesn't pick raw accounting mechanics from a menu — the UI infers them from a more
natural choice (payment method, destination account type)." Any Half-B design should extend this
precedent, not invent a new one.

**Conclusion driving the rest of this spec**: `Category` can be freely extended (more fields, more
UI) with zero money-correctness risk, because nothing in `TransactionEntryService` branches on
category identity for anything except display-name resolution and budget lookup-by-id. `Transaction
Type` cannot be freely extended the same way — any "new type" must either (a) reuse one of the 10
existing, already-correct dispatch behaviors, or (b) require a human (not the user) to write and
test a new `RecordXAsync` method, migration, and exhaustive-switch update across ~6 call sites
(`AddTransactionViewModel`, `TransactionListItem`, `HistoryEntryItem`, `TransactionLabelFormatter`,
plus whatever calculator(s) the new behavior should or shouldn't feed). There is no safe third
option where the user, from a phone screen, defines new accounting semantics from scratch.

---

## Half A — "Improve/more flexible Categories"

### A.1 What's already there vs. what's missing (confirmed by reading code, not assumed)

Read `Category.cs`, `SystemCategoryKey.cs`, `CategoriesListViewModel.cs`, `AddCategoryViewModel.cs`,
`AddCategoryPage.xaml`, `CategoriesListPage.xaml`, `CategoryConfiguration.cs`. Findings:

| Candidate | Domain layer | Application layer | UI | Verdict |
|---|---|---|---|---|
| **Icon per category** | `Category.Icon`/`SetIcon` **already exist** (`Category.cs:16,50`), mapped in EF (`CategoryConfiguration.cs:16`, `HasMaxLength(50)`) | — | **Nothing reads or writes it anywhere** — grepped `.Icon`/`SetIcon` across `src/`: zero call sites outside `Category.cs` itself and the EF config. `AddCategoryPage.xaml` has only a `Name` `Entry`. `CategoryListItem` (the list row's DTO) doesn't even carry `Icon`. | **Real gap, cheapest possible fix** — the field is fully built and dead; this is a pure UI/DTO-wiring task, zero schema change. |
| **Rename existing category** | `Category.Rename(string)` **already exists** (`Category.cs:42`) | — | **Zero call sites in `App/`** (grepped `.Rename(` — only `AddAccountViewModel`/`AddCreditCardViewModel`/`AddLoanViewModel` call `.Rename` on their own entities; no `EditCategoryPage`/`EditCategoryViewModel` exists at all) | **Real gap** — every other list-with-history entity (Accounts/Cards/Loans/Transactions) got Edit in the 2026-09-19 slice; Categories was never included in that sweep. Mirrors that slice's in-place-edit precedent exactly (rename has no side effect on another aggregate). |
| **Archive/deactivate unused categories** | **Does not exist** — `Category` has no `IsActive` field at all, unlike `FinancialAccount`/`CreditAccount` (`IsActive`/`Deactivate()`/`Reactivate()`, per the 2026-09-19 Edit/Delete slice) | — | No delete/deactivate button anywhere on `CategoriesListPage.xaml` | **Real gap.** A user who created a category and later wants it gone has genuinely no path today (can't even hard-delete). |
| **Hard delete when unreferenced** | N/A | No existence-check guard exists (`ICategoryRepository` has no `HasAnyReferencing...` method; grepped, confirmed absent) | No delete button | **Real gap**, same shape as the Deactivate/Reactivate + hard-delete-when-zero-history pattern already shipped for Accounts/Cards/Loans. |
| **Color** | Does not exist — no field | — | — | Not requested by README, not blocking anything; low-risk to add if the user wants it (see open question below), but not confirmed missing/needed from code alone — this one genuinely needs the user's steer (icon covers most of the same "make categories visually distinguishable" need at lower cost). |
| **Sub-categories / parent-child hierarchy** | Does not exist | — | — | **Not a "missing gap," a new modeling decision** — nothing in README §12 or any existing entity suggests hierarchy, and `Budget` (`CategoryId`, one row per category per month) has no concept of rolling up a child into a parent's budget. This is scope invention, not a confirmed gap — flagged as an open question, not defaulted into the plan. |
| **Default category suggestions per transaction type** | N/A — `Category` has no Income/Expense "kind" split at all today (confirmed directly in the Recurring Income spec's own note: *"Category entity has no Income/Expense Kind split"*, and `AddTransactionViewModel`'s Expense/Income/CreditCardPurchase blocks all bind to the exact same unfiltered `Categories` list) | — | — | Real, but this is closer to Half B's "default sub-type per Expense" idea (§B.4) than a bare Category field — see cross-reference below. Not building a redundant separate mechanism for it here. |
| **Reordering** | N/A | — | — | Not requested, not blocking anything, zero evidence of need (14 system categories + however many user ones is not a list that needs manual reordering to be usable; alphabetical/insertion order is a fine default). Not building this. |
| **Category-specific budget defaults** | N/A — `Budget` already IS "amount for category X, month Y" (`Budget.cs`), i.e., the budget-default concept already exists per-month; a "template that pre-fills next month's budget" is a distinct, separable feature | — | — | Not a Category field gap — it's a Budget UX feature (auto-carry-forward last month's amount). Out of this slice's scope; flag as a separate future idea, not folded in here. |

### A.2 Confirmed scope for Half A

1. **Icon picker on Add/Edit Category** — wire the already-existing `Category.Icon`/`SetIcon` into
   the UI. Simplest possible implementation: a fixed emoji palette (`Picker` or a small
   `CollectionView` grid of emoji strings), same tier of complexity as this codebase's existing
   emoji-as-icon convention (🔄, 🪙, 📈, 💰, 💳, 🏦 already used directly in
   `TransactionLabelFormatter`/README §45 — no image-asset/icon-library system exists anywhere in
   this app, so introducing one now would be a disproportionate addition). `CategoryListItem` gains
   an `Icon` field; `CategoriesListPage.xaml`'s row template renders it ahead of `Name` when present.
2. **Edit Category (rename + icon)** — new `EditCategoryPage`/`EditCategoryViewModel`, mirroring
   the in-place-edit precedent from the 2026-09-19 Accounts/Cards/Loans slice (a small mutator-driven
   page, not a reuse of the Add page's insert flow — same reasoning as Recurring Income's Decision F).
   Available for both system-defined and user-defined categories — **but a system-defined category's
   `Rename` needs a decision**: does renaming "Food" to something else break the localized-name
   fallback? See open question A-1 below; my recommendation is inline there.
3. **Deactivate/Reactivate + hard-delete-when-unreferenced for Category** — new `Category.IsActive`
   field + `Deactivate()`/`Reactivate()` mutators (mirrors `FinancialAccount`'s exact shape), new
   `ICategoryLifecycleService`/`CategoryLifecycleService` (mirrors
   `IFinancialAccountLifecycleService`/`FinancialAccountLifecycleService` file-for-file), new
   `ICategoryRepository.HasAnyReferencingCategoryAsync`-style existence checks across every
   consumer of `CategoryId` (`Expense`, `Income`, `CreditCardPurchase`, `RecurringExpense`,
   `RecurringIncome`, `Budget` — six reference sites, confirmed by grepping `CategoryId` across
   `Domain/`). `CategoriesListPage` gains a "show inactive" toggle + Deactivate/Reactivate/Delete
   buttons, mirroring `AccountsListPage`'s SwipeView pattern exactly.
4. **NOT in Half A this slice**: color, sub-category hierarchy, reordering, category-specific
   budget-template carry-forward. See open questions below for which of these need the user's
   explicit steer before a future slice picks them up.

### A.3 Data model — Half A

```csharp
// Category.cs — additive, no breaking change to the existing public API
public sealed class Category : Entity
{
    // ...existing Name/SystemKey/Icon/IsSystemDefined unchanged...
    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
```

Migration: `AddColumn<bool>("IsActive", defaultValue: true)` on the `Categories` table — genuine
schema change (unlike several prior TPH-column-reuse slices), single table, no TPH collision risk
(`Category` isn't part of any TPH hierarchy).

`ICategoryLifecycleService` (new, `Application/Categories/`):

```csharp
public interface ICategoryLifecycleService
{
    Task<bool> CanHardDeleteAsync(Guid categoryId, CancellationToken ct = default);
    Task DeleteAsync(Guid categoryId, CancellationToken ct = default);
    Task DeactivateAsync(Guid categoryId, CancellationToken ct = default);
    Task ReactivateAsync(Guid categoryId, CancellationToken ct = default);
}
```

`CanHardDeleteAsync` checks, in order (mirrors `FinancialAccountLifecycleService.CanHardDeleteAsync`'s
three-part shape, extended to six reference sites since Category is referenced more widely than a
single account): zero `Expense`/`Income`/`CreditCardPurchase` transactions reference it (one new
`ITransactionRepository.HasAnyTransactionReferencingCategoryAsync`), zero `RecurringExpense`/
`RecurringIncome` definitions reference it (two new repository methods, same idiom as the existing
`HasAnyReferencingAccountAsync`), zero `Budget` rows reference it (one new
`IBudgetRepository.HasAnyReferencingCategoryAsync`). **System-defined categories additionally
can never be hard-deleted or deactivated** — see open question A-2, recommendation stated there.

### A.4 UI — Half A

Extends existing screens, no new tab/nav entry point needed (`Categories` is already a top-level
tab per `AppShell.xaml`'s 9-tab bar):

- `AddCategoryPage.xaml`/`AddCategoryViewModel.cs` — add an icon picker below the Name `Entry`.
- New `EditCategoryPage.xaml`/`EditCategoryViewModel.cs` — Name + Icon, `[QueryProperty]` on a
  **string** id (per this codebase's own documented `Guid`-`[QueryProperty]` P0 bug class — see
  Recurring Income's Decision F/the People-screen commit history), Save calls `Rename`+`SetIcon`,
  Cancel navigates back.
- `CategoriesListPage.xaml` — row template gains the `Icon` label, a "show inactive" `Switch`
  (mirrors `AccountsListPage`'s precedent), and per-row Edit/Deactivate/Reactivate/Delete actions
  via `SwipeView` (mirrors `AccountsListPage`'s exact SwipeView shape, including the known
  Appium-testing caveat already documented in `MEMORY.md` — not a new risk, a known one).
- `CategoriesListViewModel.cs` — `LoadCategoriesCommand` gains a `ShowInactive` toggle-driven
  filter (mirrors `AccountsListViewModel`'s `GetActiveAsync` vs `GetAllAsync` split), new
  `DeactivateCommand`/`ReactivateCommand`/`DeleteCommand`.

### A.5 Half A test list

- **Domain**: `Category.Deactivate`/`Reactivate` toggle `IsActive` correctly; `SetIcon`/`Rename`
  already have implicit coverage via construction tests — add direct mutator tests since they'll
  now have real callers.
- **Application**: `CategoryLifecycleServiceTests` — `CanHardDeleteAsync` returns false for each of
  the six reference kinds individually (one test per kind, not one combined test, matching this
  codebase's existing granularity for `FinancialAccountLifecycleServiceTests`); `DeleteAsync` throws
  if a reference exists even when called directly (re-check-before-delete, mirrors the account
  service's own defensive re-check); Deactivate/Reactivate round-trip.
- **Infrastructure**: new repository existence-check methods, real-SQLite round-trip tests (mirrors
  `TransactionRepositoryDateRangeQueriesTests`' realism standard).
- **resx**: new keys for Edit/Deactivate/Reactivate/Delete buttons + "show inactive" toggle label +
  icon-picker labels, ES/EN parity diff confirmed 0, per every prior slice's closing check.

---

## Half B — "New, user-defined Transaction Types" (the consequential half)

### B.1 The two options, evaluated honestly

**Option (a): user-defined sub-types riding on an existing type's already-correct behavior.**
A user creates e.g. "Gasolina" as a *named preset* that bundles: which safe base `TransactionType`
it rides on (Expense, Income, or Transfer — the three types with no cross-account-type or
schedule-mutation entanglement), a default `Category`, a default `Icon`, and optionally a default
`Account`/payment method. Selecting "Gasolina" in the Add-Transaction flow pre-fills those fields
onto a completely ordinary `Expense` (or `Income`/`Transfer`) — the underlying `Transaction` row
that gets saved is byte-for-byte the same kind of row the app already knows how to reverse, report
on, budget against, and exclude/include correctly everywhere. **Zero new accounting code.** The
user gets a "new type" in the sense that matters to them (a one-tap, icon-bearing, pre-filled
shortcut for a thing they do often) without the app ever executing user-authored money logic.

**Option (b): a genuinely new user-authorable accounting behavior** — e.g., a screen where a user
declares "this new type debits Account A and credits Account B" or "counts as income" via some
config surface, and the app dynamically dispatches on that config instead of a hardcoded
`RecordXAsync` method.

Concretely, why (b) is dangerous, grounded in what's actually in `TransactionEntryService.cs`, not
abstractly:

- **Double-counting is trivially reachable.** A user could define a type that both counts as
  income AND targets the same account a Transfer would use — nothing stops them from effectively
  recreating `Transfer` but flagged `CountsAsIncome = true`, which would silently inflate the
  Income vs. Expenses report and the real-surplus calculation every time it's used. The existing 10
  types get this right today only because a human engineer reasoned through each one against
  `CLAUDE.md`'s domain rules (see the dispatch table in §0) — that reasoning is exactly the part a
  config screen can't replicate.
- **The exhaustive-switch surface is real and already fragile.** This codebase has hit
  `NotSupportedException` crashes from adding new `Transaction` subtypes and forgetting one of the
  ~4-6 exhaustive switches (`TransactionListItem.FromDomain`, `HistoryEntryItem.FromDomain`,
  `TransactionLabelFormatter.BuildLabel`, `AddTransactionViewModel.AvailableTypes`) at least three
  times in this project's own history (per `MEMORY.md`'s slice notes for Investment
  Contribution/Withdrawal and Interest Income). A dynamically-user-defined type can't participate in
  a compile-time exhaustive switch at all — every one of those call sites would need a fallback
  "unknown/generic" rendering path, which is a permanent category of under-tested code, not a
  one-time migration cost.
- **"Pago mínimo"/"pago para evitar intereses" and snowball-style rules are exactly the shape of
  mistake a naive user-authored type invites** — `CLAUDE.md`'s own explicit warning is "never derive
  one field from an assumed formula." A user given a general-purpose "define money movement" tool
  has no guardrail stopping them from wiring up something that looks like it validates/derives an
  amount and quietly corrupts a balance the first time it's used against a credit card or loan.
- **No sandboxing mechanism exists or is proposed.** There's no "dry-run" or "preview the effect on
  balances before this type goes live" concept anywhere in this app's design; a bad custom-type
  definition would corrupt real balances on first use, with reversal only as good as this
  codebase's existing (already partial — CreditCardPurchase/LoanPayment/Investment*/Reimbursement
  are explicitly *not* reversible per the 2026-09-19 Edit/Delete slice's own scope table) undo
  support.

**Guardrails that WOULD be required to attempt (b) safely, if ever attempted:** a closed,
enumerable set of "movement shapes" (not free-form scripting) — e.g. exactly the shapes this
codebase already has (`debit-one`, `credit-one`, `debit-one-credit-other`, `debit-one-reduce-debt`)
— a mandatory, non-overridable `CountsAsSpend`/`CountsAsIncome` mapping *derived from the chosen
shape*, not independently settable by the user (this alone closes the "flag both income and
expense" hole); a hard block on ever touching `CreditAccount`/`RecurringExpense`/loan-schedule
state from a user-defined type (confine custom types to the two safest entities,
`FinancialAccount`↔`FinancialAccount`, nothing else); and a real preview/dry-run step before first
use. This is a non-trivial design-and-build effort in its own right — bigger than this entire slice
spec — and still leaves the exhaustive-switch fragility problem only partially addressed (every
switch would still need a "handle the generic/custom case" branch, forever).

### B.2 Recommendation

**Build option (a). Do not build option (b) in this slice, and I recommend not building it at all
without a much more specific, narrower user ask than "nuevos tipos"** — the risk/effort described
above is disproportionate to what the user's actual stated goal appears to be (fast, personalized,
icon-bearing shortcuts for things they do often — "Gasolina," presumably "Uber," "Supermercado,"
etc. — not a desire to invent new accounting rules). Option (a) satisfies "nuevos tipos y
personalizados" in every way that matters for a personal-finance app used by one person: it feels
like a new type when adding a transaction (own name, own icon, own default fields, shows up as a
first-class choice), while being, underneath, one of the three already-bulletproof types.

**This is the single most consequential design decision in this spec and should be confirmed with
the user explicitly before implementation starts** — per this codebase's own established escalation
bar (mirrors how Phase 4's sync model and the term-deposit-auto-renewal question were escalated
rather than self-decided). I am recommending (a), not silently picking it — see open question B-1.

**Confirmed by the user, 2026-09-21: Option (a) is the direction. Not open for reconsideration.**

### B.3 Data model — Option (a): `TransactionPreset`

New entity, own folder `Domain/TransactionPresets/`, NOT a new `TransactionType` enum member and
NOT a `Transaction` subtype — it never gets saved as a transaction row itself, only used to
pre-fill one:

```csharp
public sealed class TransactionPreset : Entity
{
    public string Name { get; private set; } = null!;          // e.g. "Gasolina"
    public string? Icon { get; private set; }                   // same fixed-emoji convention as Category
    public TransactionPresetBaseType BaseType { get; private set; }  // Expense | Income | Transfer — closed set, exactly the 3 safest existing types
    public Guid? DefaultCategoryId { get; private set; }         // null for Transfer (no category concept there)
    public Guid? DefaultAccountId { get; private set; }          // optional convenience pre-fill; user can still override at entry time
    public bool IsActive { get; private set; } = true;

    // Rename/SetIcon/UpdateDefaults/Deactivate/Reactivate mutators, mirroring Category's shape
}

public enum TransactionPresetBaseType { Expense, Income, Transfer }
```

**Deliberately excludes** `CreditCardPurchase`/`CreditCardPayment`/`LoanPayment`/
`InvestmentContribution`/`InvestmentWithdrawal`/`InterestIncome`/`Reimbursement` as valid
`BaseType` values — every one of those either targets a second, non-`FinancialAccount` entity
(`CreditAccount`), mutates a schedule (`LoanPayment.AdvanceSchedule`), or is already
auto-routed/context-detected rather than user-picked (`CreditCardPurchase`, `InterestIncome`).
Widening the closed set later is possible but is its own future decision, not defaulted into this
slice.

**Update, 2026-09-21 (see §B.7 below)**: this closed set stays exactly these 3 values even after
resolving open question A-5's card-backed-preset ask — `CreditCardPurchase` is deliberately NOT
added as a 4th `BaseType`, for the identical reason stated in the sentence above (it's already
auto-routed/context-detected, never user-picked, anywhere in this app). The card-backed case is
handled by widening `TransactionPreset`'s *field set* instead — full reasoning, data model, UI
dispatch, and updated test list in §B.7.

`TransactionEntryService` needs **zero changes** — a preset is purely an App/Application-layer
convenience that pre-fills `AddTransactionViewModel`'s existing fields before the user hits Save;
the actual `RecordExpenseAsync`/`RecordIncomeAsync`/`RecordTransferAsync` call is byte-identical to
today's, with no new parameter. This holds for the card-backed case added in §B.7 too — confirmed
explicitly there, not just assumed by extension.

### B.4 UI — Option (a)

- New "Manage Transaction Presets" screen (List+Add+Edit, mirrors `CategoriesListPage`/
  `AddCategoryPage` exactly, reached via Settings — same "used at setup time, not every screen"
  placement reasoning as FinancialInstitution/CardNetwork, not a new tab).
- `AddTransactionPage`: a new, small "Quick presets" row (horizontal chip/button list, icon +
  name) shown above the existing Type `Picker`, populated from active `TransactionPreset`s filtered
  to those whose `BaseType` is legal for the account context (no filtering needed at MVP — a preset
  targets Expense/Income/Transfer directly, doesn't depend on account type). Tapping a preset chip
  sets `SelectedType` to the mapped `TransactionType`, pre-fills `SelectedCategory`/`SelectedAccount`
  from the preset's defaults (still overridable — this is a pre-fill, not a lock), and leaves the
  rest of the existing Expense/Income/Transfer block exactly as it already renders today. No new
  `TransactionType` enum member, no new branch in `AvailableTypes`, no new case in
  `TransactionLabelFormatter`/`TransactionListItem`/`HistoryEntryItem` — the saved row is a plain
  `Expense`/`Income`/`Transfer`, indistinguishable after the fact from one entered the "normal" way
  (this is intentional — a preset is an entry-time shortcut, not a permanently-tagged category of
  transaction; if the user later wants to filter "all my Gasolina expenses," that's what the
  `DefaultCategoryId`-linked `Category` already does, via the existing Category-based reporting).

**Update, 2026-09-21**: the above was left underspecified about exactly which picker collection
`SelectedAccount` resolves against and about the card-backed case. Both are now made precise in
§B.7.3 (UI dispatch) and §B.7.4 (updated Manage Presets Add/Edit UI) — read those alongside this
section rather than treating this section alone as current for the Expense case.

### B.5 Cross-reference to Half A's "default category suggestions per transaction type" gap

`TransactionPreset.DefaultCategoryId` is exactly the mechanism that satisfies Half A's flagged
"default category suggestions per transaction type" gap (§A.1's table) — not a second, redundant
feature. One mechanism, two motivating asks, confirmed non-duplicative.

### B.6 Half B test list

- **Domain**: `TransactionPreset` construction validation (name required, `BaseType` must be one
  of the 3 closed values — enforced by the enum itself, not a runtime check, but add a test
  confirming `DefaultCategoryId` is rejected as required when `BaseType != Transfer` and irrelevant/
  ignored when `BaseType == Transfer`); Deactivate/Reactivate/Rename/SetIcon/UpdateDefaults mutator
  tests.
- **Application**: `TransactionPresetLifecycleService` — hard-delete guard (a preset has no
  transaction-history reference by design, since it never appears on a saved `Transaction` row, so
  `CanHardDeleteAsync` should almost always be true — confirm this explicitly with a test, since
  it's the one lifecycle service in this app where the guard is expected to basically never block,
  and that expectation should be pinned down, not assumed).
- **App-layer** (no test harness exists per this codebase's established gap — build+manual-review
  only, consistent with every other App-layer change in this project's history): confirm selecting
  a preset chip correctly sets `SelectedType`/`SelectedCategory`/`SelectedAccount` without touching
  `TransactionEntryService`; confirm overriding a pre-filled field after tapping a preset still
  saves the overridden value (pre-fill, not lock).
- **A regression test worth calling out explicitly, given this project's own bug history**: confirm
  `TransactionListItem.FromDomain`/`HistoryEntryItem.FromDomain`/`TransactionLabelFormatter.BuildLabel`'s
  exhaustive switches need **zero changes** for this slice (i.e., grep confirms no new case was
  added to any of them) — this is the concrete, checkable proof that Option (a) actually avoided
  the exhaustive-switch fragility problem Option (b) would have created.
- **resx**: new keys for the presets screen + the Add-Transaction quick-preset row, ES/EN parity.

**Update, 2026-09-21**: see §B.7.5 for the additional test cases the card-backed-preset follow-up
requires (extends, does not replace, the list above).

---

## B.7 — Card-backed presets (follow-up, 2026-09-21)

Resolves open question A-5, confirmed by the user directly: presets need to support routing to a
specific credit card — e.g. a "Gasolina" preset that always posts as a `CreditCardPurchase` against
one specific card, not just a plain `Expense`. Re-read `RecurringExpense.cs` (the
`AccountId`/`CreditAccountId` "exactly one" precedent), `AddTransactionViewModel.cs` (the
`SelectedAccount.IsCreditAccount` routing branch and the `PaymentAccounts` collection that feeds
it), `TransactionEntryService.cs` (`RecordCreditCardPurchaseAsync`), `NamedOption.cs`, and
`AddRecurringExpenseViewModel.cs` (the Add/Edit screen that already has this exact "does this
target a plain account or a card" problem solved) in full before writing this section. Every claim
below is grounded in what that code actually does today, cited by file and line, not assumed.

### B.7.1 — Does this require any change to `TransactionEntryService`? No — confirmed, not assumed

Confirmed: **zero** change to `TransactionEntryService`, in either the plain-preset case (already
established in §B.3) or the newly-added card-backed case. `RecordCreditCardPurchaseAsync`
(`TransactionEntryService.cs:133-163`) is called with the exact same signature
`AddTransactionViewModel.SaveAsync`'s pre-existing Expense-block branch already uses today for a
manually-entered, card-targeted expense (`AddTransactionViewModel.cs:641-652`, the
`if (SelectedAccount.IsCreditAccount)` branch). A card-backed preset changes none of that method's
inputs or their meaning — it only changes *how `SelectedAccount` gets populated* before `SaveAsync`
runs its pre-existing, unmodified check.

So the answer to your own question is exactly what you predicted: **only the dispatch decision
changes, and it's a smaller decision than that phrase implies** — it collapses to a single
`.FirstOrDefault` lookup against a collection (`PaymentAccounts`) that already merges both account
kinds and already carries the `IsCreditAccount` flag `SaveAsync` already branches on (see §B.7.3).
The only genuinely new code anywhere in this follow-up is (1) two new nullable fields plus a
construction-time guard on `TransactionPreset` itself (Domain layer, §B.7.2), and (2) that one
lookup inside the preset-tap handler (App layer, §B.7.3). `RecordExpenseAsync` and
`RecordCreditCardPurchaseAsync` themselves are untouched — same as §B.3 already established for the
non-card case, now confirmed to hold for the card case too.

### B.7.2 — Data model: widened field set, not a widened `BaseType`

`TransactionPresetBaseType` does **not** gain a 4th (`CreditCardPurchase`) value. This mirrors,
rather than reinvents, the exact mechanism `CreditCardPurchase` already uses for manual entry:
nothing in this app ever lets a user directly pick `TransactionType.CreditCardPurchase` from a
menu (confirmed in §0's dispatch table and `AddTransactionViewModel.cs`'s own doc comment cited
there) — the routing is entirely account-type-driven, decided by inspecting the selected
account/card (`NamedOption.IsCreditAccount`), never type-declared by the user. Widening
`TransactionPresetBaseType` to a 4th `CreditCardPurchase` value would reintroduce exactly the
user-declares-the-accounting-mechanism shape §B.1's Option (a)/(b) analysis rejected — it would
mean a preset's *type* encodes a routing decision the rest of the app deliberately keeps out of the
user's hands, and it would resurrect the exhaustive-switch risk §B.6's regression test exists to
catch (a 4th `BaseType` value is one more thing every switch over `TransactionPresetBaseType` would
need a case for, forever). Keeping `BaseType` at exactly 3 and expressing "this Expense preset
happens to target a card" as an *optional field on the Expense case* keeps the same
account-type-driven, context-inferred shape — just applied once, at preset-authoring time, instead
of only at entry time. Same mechanism, applied earlier.

Widened shape:

```csharp
public sealed class TransactionPreset : Entity
{
    public string Name { get; private set; } = null!;
    public string? Icon { get; private set; }
    public TransactionPresetBaseType BaseType { get; private set; }   // still exactly Expense | Income | Transfer — unchanged by this follow-up
    public Guid? DefaultCategoryId { get; private set; }
    public Guid? DefaultAccountId { get; private set; }               // a FinancialAccount id; mutually exclusive with DefaultCreditAccountId
    public Guid? DefaultCreditAccountId { get; private set; }         // a CreditCard id; only ever non-null when BaseType == Expense
    public bool IsActive { get; private set; } = true;

    public TransactionPreset(
        string name,
        string? icon,
        TransactionPresetBaseType baseType,
        Guid? defaultCategoryId,
        Guid? defaultAccountId,
        Guid? defaultCreditAccountId)
    {
        ValidateDefaults(baseType, defaultAccountId, defaultCreditAccountId);
        // ...existing name/BaseType-vs-DefaultCategoryId validation from §B.6 unchanged...
        Name = name.Trim();
        Icon = icon;
        BaseType = baseType;
        DefaultCategoryId = defaultCategoryId;
        DefaultAccountId = defaultAccountId;
        DefaultCreditAccountId = defaultCreditAccountId;
    }

    public void UpdateDefaults(Guid? categoryId, Guid? accountId, Guid? creditAccountId)
    {
        ValidateDefaults(BaseType, accountId, creditAccountId);
        DefaultCategoryId = categoryId;
        DefaultAccountId = accountId;
        DefaultCreditAccountId = creditAccountId;
    }

    private static void ValidateDefaults(TransactionPresetBaseType baseType, Guid? accountId, Guid? creditAccountId)
    {
        if (accountId is not null && creditAccountId is not null)
            throw new ArgumentException("A preset cannot default to both a FinancialAccount and a credit card at once.");

        if (creditAccountId is not null && baseType != TransactionPresetBaseType.Expense)
            throw new ArgumentException("Only an Expense-based preset can default to a credit card.", nameof(creditAccountId));
    }

    // Rename/SetIcon/Deactivate/Reactivate unchanged from §B.3.
}

public enum TransactionPresetBaseType { Expense, Income, Transfer }   // unchanged — still exactly 3 values
```

Two deliberate, explainable divergences from a naive "just copy `RecurringExpense`" mirror:

1. **The invariant is "at most one of `DefaultAccountId`/`DefaultCreditAccountId`," not "exactly
   one."** `RecurringExpense.AccountId`/`CreditAccountId` is a *mandatory* XOR — a recurring
   expense always has to know where it pulls money from, because it posts a real transaction on its
   own schedule with no human present to fill in a blank. A `TransactionPreset` is a shortcut
   template, not a standing obligation — §B.3 already established `DefaultAccountId` as an optional
   convenience the user can leave unset and fill in manually at entry time, and that stays true:
   both fields can be null (a preset that only pre-fills category+icon), exactly one can be set
   (the plain case from §B.3, or the new card-backed case), but never both at once.
2. **No named-factory-method trick is needed here**, unlike `RecurringExpense.ForCreditCard`.
   `RecurringExpense` needed that workaround specifically because its two entry points
   (`FinancialAccount`-backed vs. `CreditCard`-backed) are two constructor overloads with an
   *identical trailing parameter shape* — `RecurringExpense.cs:69-75`'s own doc comment says this
   explicitly: a hypothetical second `(string, decimal, Guid, Guid, ...)` overload is uncompilable
   next to the first. `TransactionPreset`'s constructor instead takes both `defaultAccountId` and
   `defaultCreditAccountId` as two independently-nullable parameters in a **single** constructor —
   there is no overload collision to route around, so introducing a named factory here would add
   ceremony without solving a real compilation problem. Mirroring `RecurringExpense`'s *invariant
   enforcement discipline* (validate the XOR-shaped rule at every mutation entry point) is the right
   thing to copy; mirroring its *factory-method mechanism* is not, because the reason that mechanism
   exists doesn't apply here.

`DefaultAccountId` does **not** become a single polymorphic field that sometimes holds a
`FinancialAccount` id and sometimes a `CreditAccount` id with no type tag — a bare `Guid` can't
self-describe which repository to resolve it against, and `RecurringExpense` itself doesn't do this
either (it keeps two separate nullable fields, not one ambiguous one). The two-field shape above is
the established convention, not a new one.

Foreign-key existence (does this `Guid` actually reference a real `FinancialAccount`/`CreditCard`
row) is validated the same way `RecurringExpense`'s is: not by the Domain constructor (Domain has no
repository access), but structurally, by construction — the Add/Edit Preset ViewModel only ever
passes an id sourced from a live, freshly-loaded picker collection (§B.7.4), never free text, the
same guarantee `AddRecurringExpenseViewModel.SaveAsync` already relies on. No separate
`TransactionPresetCreationService`/FK-validation layer is introduced for this — consistent with
`RecurringExpense` having none either.

### B.7.3 — UI dispatch: the preset-tap handler

§B.4's original text said tapping a preset chip "pre-fills `SelectedCategory`/`SelectedAccount`
from the preset's defaults" without specifying which picker collection `SelectedAccount` resolves
against. That was underspecified even for the plain case; both cases are now precise:

- For an **Expense-based preset** (`BaseType == Expense`), `SelectedAccount` is always resolved
  against `PaymentAccounts` — the same merged FinancialAccount+CreditCard collection
  (`AddTransactionViewModel.cs:385-389`) the Expense block's manual Account picker already uses.
  Concretely, the entire new logic in the preset-tap handler is:

  ```csharp
  var targetId = preset.DefaultAccountId ?? preset.DefaultCreditAccountId;
  SelectedAccount = targetId is null
      ? null
      : PaymentAccounts.FirstOrDefault(o => o.Id == targetId.Value);
  ```

  One lookup, one collection, **no `if`/`else` on card-ness at all** — `PaymentAccounts` already
  carries `IsCreditAccount` per entry (decided when the collection was built in `LoadOptionsAsync`,
  not decided here), so whichever `NamedOption` this resolves to already has the correct flag.
  `SaveAsync`'s existing `if (SelectedAccount.IsCreditAccount)` branch
  (`AddTransactionViewModel.cs:641`) then does the rest, completely unmodified.
- For an **Income- or Transfer-based preset**, `SelectedAccount`/`SelectedDestinationAccount` keep
  resolving against `Accounts`/`TransferDestinationAccounts` exactly as §B.4 already specified —
  those collections never include credit cards (confirmed: both are built only from
  `accounts.Where(...)`, never from `creditAccounts`, per `AddTransactionViewModel.cs:361-370`), so
  `DefaultCreditAccountId` is structurally guaranteed null for these `BaseType`s (enforced by
  §B.7.2's Domain-layer guard) and is never consulted here.
- `SelectedType` is set to `TransactionType.Expense` in **both** the plain and card-backed cases —
  never to `TransactionType.CreditCardPurchase`, which per §0's dispatch table isn't a top-level,
  user-selectable value in this ViewModel at all. The routing to the card branch happens entirely
  inside the Expense case's existing logic, same as it already does for manual entry today.

No new branch is added to `AddTransactionViewModel.SaveAsync`. The only new App-layer code for this
entire follow-up is the two-line lookup above, inside whatever method already handles a preset-chip
tap.

### B.7.4 — Manage Transaction Presets: Add/Edit UI update (supersedes part of §B.4)

Mirrors `AddRecurringExpenseViewModel`'s already-established pattern **exactly** — not a new
toggle/radio-button UI. That screen doesn't ask the user "is this a FinancialAccount or a credit
card target?" as a separate question: it shows one `Picker` bound to one merged collection
(`AddRecurringExpenseViewModel.Accounts`, built with the identical 💳-prefix convention as
`PaymentAccounts`), and `SaveAsync` decides which constructor path to use by checking
`SelectedAccount.IsCreditAccount` on whatever the user picked
(`AddRecurringExpenseViewModel.cs:148-164`). The "Manage Transaction Presets" Add/Edit screen adopts
the identical shape:

- The `BaseType` selector (Expense/Income/Transfer — unchanged from §B.4) drives which
  default-fields section is visible, the same conditional-visibility idiom used throughout this app
  (`IsExpense`/`IsIncome`/`IsTransfer`-style computed bools).
- When `BaseType == Expense` is selected: the "Default account" field is a single `Picker` bound to
  a merged FinancialAccount+CreditCard collection — same shape/construction as `PaymentAccounts` —
  requiring the Add/Edit Preset ViewModel to inject `ICreditAccountRepository` alongside
  `IFinancialAccountRepository` (mirrors `AddRecurringExpenseViewModel`'s constructor exactly). On
  Save, the branch is the same one-liner shape as `AddRecurringExpenseViewModel.SaveAsync`'s:
  `SelectedAccount?.IsCreditAccount == true` sets `DefaultCreditAccountId` (leaving `DefaultAccountId`
  null); otherwise it sets `DefaultAccountId` (leaving `DefaultCreditAccountId` null); leaving the
  picker unset entirely (a preset that pre-fills nothing but category+icon — still a legal, useful
  preset per §B.3's original design) leaves both null.
- When `BaseType == Income` or `BaseType == Transfer` is selected: the "Default account" field stays
  a FinancialAccount-only `Picker` (no cards ever offered) — unchanged from §B.4's original
  description, since neither type can ever legally target a credit card (§B.7.2's guard makes this a
  Domain-enforced invariant, not just a UI convention).
- `DefaultAccountId` does **not** become a single polymorphic field — see §B.7.2's reasoning; the
  two-field, mutually-exclusive shape stays visible at the UI layer too. Whichever the user picked
  determines which of the two properties gets written on Save, never both, never a merged/ambiguous
  one.

### B.7.5 — Additional tests (extends §B.6, does not replace it)

**Domain** (`TransactionPreset` construction/mutators):

- Constructing with both `defaultAccountId` and `defaultCreditAccountId` non-null throws.
- Constructing with `defaultCreditAccountId` set and `BaseType != Expense` throws — one test each
  for `Income` and `Transfer`.
- Constructing with `defaultCreditAccountId` set and `BaseType == Expense` succeeds; reading back
  `DefaultAccountId` returns null.
- Constructing with both `defaultAccountId` and `defaultCreditAccountId` null still succeeds — the
  "no default account at all" case from §B.3 must keep working; this follow-up must not accidentally
  make an account default mandatory.
- `UpdateDefaults` re-validates the identical invariants — a test proving a call with both ids set
  throws, and a test proving a call passing a non-null `creditAccountId` against an
  Income/Transfer-`BaseType` preset throws too. The latter is unreachable through the planned UI per
  §B.7.4, but `RecurringExpense`'s own private-constructor guard
  (`RecurringExpense.cs:106-108`, explicitly commented as "unreachable through the public API...
  guards against a future third caller") establishes the precedent of defending a
  structurally-unreachable-through-the-UI case anyway — mirror that same defensive posture here.

**Application** (`TransactionPresetLifecycleService`):

- `CanHardDeleteAsync` still returns `true` for a card-backed preset even after its chip has been
  tapped and used to post a real `CreditCardPurchase` — pin this down explicitly, don't assume it by
  extension from the plain case. The resulting `CreditCardPurchase` row has no foreign key back to
  the `TransactionPreset` that pre-filled it (per §B.4's "indistinguishable after the fact" design),
  so a card-backed preset's delete guard is exactly as near-trivial as §B.6 already flagged for the
  plain case — worth a dedicated test given how easy it would be to accidentally wire up a spurious
  `CreditAccountId`-based reference check here that shouldn't exist.

**App-layer** (build+manual-review only, per §B.6's established gap):

- Tapping a card-backed preset chip resolves `SelectedAccount` from `PaymentAccounts` (not
  `Accounts`) and ends with `SelectedAccount!.IsCreditAccount == true`.
- Saving after tapping a card-backed preset chip posts a real `CreditCardPurchase` — the target
  card's `AmountOwed` increases by exactly the entered amount, verified live the same way the
  2026-09-19 credit-card-backed Recurring Expense slice verified its own equivalent case — not a
  plain `Expense` against no account, and not a crash.
- Saving after tapping a plain (non-card) preset chip is unaffected by this follow-up — still posts
  a plain `Expense`, confirming the widened `TransactionPreset` shape didn't regress the
  already-specified plain case from §B.3/§B.4.
- Overriding the pre-filled card back to a plain account (or vice versa) after tapping a preset chip
  still saves the overridden value — the same "pre-fill, not lock" guarantee §B.6 already required
  for the plain case, re-confirmed here for the card case.

**Regression test, re-run explicitly for this follow-up**: `TransactionListItem.FromDomain`/
`HistoryEntryItem.FromDomain`/`TransactionLabelFormatter.BuildLabel`'s exhaustive switches still
need **zero changes** — grep confirms no new case was added to any of them, and no new
`TransactionType` enum member exists anywhere. This is the concrete, checkable proof that widening
`TransactionPreset`'s *field set* (rather than widening `TransactionPresetBaseType`, rejected in
§B.7.2) kept this follow-up inside Option (a)'s guarantees.

**resx**: no new keys anticipated beyond what §B.6 already listed — the Add/Edit Preset screen's
"Default account" field reuses the exact same field label and `Picker` component as the plain case
(no separate "Card" vs. "Account" field/label pair is introduced, per §B.7.4's single-merged-picker
decision). Confirm this assumption during implementation and add a key only if the merged picker
turns out to need a label distinct from the existing one.

### B.7.6 — Build order confirmation (updates step 4 below)

Unchanged in shape: step 4 ("Half B, Domain+Infrastructure") is still exactly
`TransactionPreset`/`TransactionPresetBaseType`/migration/configuration/repository — this follow-up
only changes the *field set* captured within that one step, not the number or order of build-order
steps. Concretely:

- The `TransactionPreset` entity gains one more nullable-FK column (`DefaultCreditAccountId`)
  alongside the already-planned `DefaultAccountId`.
- No new EF configuration complexity: `TransactionPreset` is not part of any TPH hierarchy (its own
  table, no sibling subtypes sharing columns), so this does not reopen the
  `CreditCard`/`Loan`/`TermDeposit`/`InvestmentFund` TPH-column-collision bug class this project has
  hit before (per `MEMORY.md`'s EF TPH note) — this is a plain second nullable-FK column, same
  treatment as `DefaultAccountId` itself.
- Because `TransactionPreset` hasn't shipped yet (the whole feature is still pre-implementation),
  there is exactly **one** migration to write for it, not a migration followed by a patch migration
  — step 4 should build the widened shape from §B.7.2 from day one.
- Step 5 ("Half B, Application") is unaffected beyond the lifecycle-service test addition in §B.7.5
  — no new service, no new interface method; `ITransactionPresetLifecycleService`'s shape
  (delete/deactivate/reactivate guards) doesn't change.
- Step 6 ("Half B, App") gains the `ICreditAccountRepository` dependency on the Add/Edit Preset
  ViewModel (§B.7.4) and the two-line lookup in the preset-tap handler (§B.7.3) — both fall within
  the scope step 6 already described ("Manage Presets screen... `AddTransactionPage`'s quick-preset
  chip row"), not new steps.
- **Half A is untouched by this entire follow-up.** `Category` has no relationship to credit cards,
  and nothing in §B.7.1-§B.7.5 references `Category.cs`, `CategoryConfiguration.cs`,
  `ICategoryLifecycleService`, or any other Half-A file. Confirmed, not just asserted.

---

## Build order (both halves independently shippable, Half A first)

1. **Half A, Domain+Infrastructure**: `Category.IsActive`, migration, six new repository
   existence-check methods.
2. **Half A, Application**: `ICategoryLifecycleService`/`CategoryLifecycleService`.
3. **Half A, App**: icon wiring on Add/new Edit page, Deactivate/Reactivate/Delete on
   `CategoriesListPage`.
4. **Half B, Domain+Infrastructure**: `TransactionPreset`/`TransactionPresetBaseType`, migration,
   configuration, repository. **Build with the widened field set from §B.7.2
   (`DefaultCreditAccountId` included) from the start — see §B.7.6.**
5. **Half B, Application**: `ITransactionPresetLifecycleService`/implementation (thin — see §B.6's
   note that the delete-guard is expected to be near-trivial).
6. **Half B, App**: Manage Presets screen (List/Add/Edit, copy Half A's Category pattern
   file-for-file, widened per §B.7.4), `AddTransactionPage`'s quick-preset chip row (dispatch logic
   per §B.7.3).

Half A has zero dependency on Half B and could ship alone if the user wants to see it land first;
Half B's UI (step 6) benefits from Half A's Edit-Category pattern existing as a direct copy
template, so sequencing A before B (as listed) is the natural order, not a hard requirement.

---

## Open questions for the user (escalated, not self-decided)

**B-1 (the big one, flagged above, repeating it here so it's not missed in a long document):**
**RESOLVED, 2026-09-21 — Option (a) confirmed by the user directly.** Kept below for the historical
reasoning trail. Confirm Option (a) — user-defined presets riding on Expense/Income/Transfer's
existing, already-safe behavior — is what "nuevos tipos y personalizados" means, before any
implementation starts. If the user actually wants something closer to Option (b) (genuinely new
accounting semantics, e.g. a new kind of money movement this app doesn't do today), that needs its
own, much more specific conversation about exactly what movement/behavior is missing — not a
general "let me define anything" tool, which this spec recommends against building at any scope.

**A-1: Can a system-defined category be renamed?** Today, `Category.IsSystemDefined` categories
(Food, Housing, etc.) get their display name from `SystemCategoryKeyToLabelConverter` (a resx
lookup keyed on `SystemKey`), not from `Name` directly in the current read path — need to confirm
whether the UI should even show/allow editing `Name` for these, since a renamed `Name` might be
silently ignored by whatever currently drives display (this needs a direct read of
`SystemCategoryKeyToLabelConverter` before implementation, not assumed here). **My recommendation**:
allow icon editing on system categories (harmless, purely additive) but keep `Name`/rename
**read-only for system-defined categories** — renaming "Food" to "Comida" makes sense as a
localization request, not a per-user customization, and this app already handles that via
`SystemCategoryKeyToLabelConverter`'s resx-driven localization, not per-user renaming. A future
"translate the label yourself" ask would be a different, resx-touching feature, not this one.

**Confirmed by the user, 2026-09-21: icon-editing only, no rename/deactivate/delete for
system-defined categories. Not open for reconsideration.**

**A-2: Can a system-defined category be deactivated/deleted?** My recommendation, stated not just
offered: **no** — block Deactivate/Delete entirely for `IsSystemDefined` categories (button
disabled/hidden, `CategoryLifecycleService` throws defensively if called anyway). System categories
are the baseline vocabulary every budget/report screen expects to exist; letting them disappear
risks history that already references them (old transactions, old budgets) losing their category
context with no recovery path, for a benefit (decluttering) a user can already get today by simply
not using a category they don't like. Confirm this is acceptable before implementation, since it's
a real, if minor, restriction the user didn't explicitly ask for.

**Confirmed by the user, 2026-09-21 — see A-1's confirmation line above (same answer covers both).**

**A-3: Category color, beyond icon?** Not building it this slice (icon likely covers the
"visually distinguish categories" need at lower cost, and no evidence in the code that color is
missing/blocking anything). If the user specifically wants color too, say so and it's a small,
additive follow-up (one more nullable string field, same shape as Icon) — not escalated as
blocking, just flagged as easy to add later if wanted.

**A-4: Sub-categories/hierarchy?** Not building it this slice — this is real new modeling
(parent-child, roll-up budgeting semantics, roll-up reporting) with no README backing and no
existing precedent anywhere in this codebase to mirror safely. If the user wants this, it deserves
its own dedicated scoping pass (it's at least as big as this entire Half-A section), not a bullet
squeezed into this slice.

**A-5: Should `TransactionPreset` (Half B) support a default *payment method* too (e.g. "Gasolina"
always via a specific credit card, auto-routing to `CreditCardPurchase`)?**

**RESOLVED, 2026-09-21 — YES, confirmed by the user directly. Full design in §B.7 above.** Kept
below for the historical reasoning trail that motivated the follow-up. Deliberately excluded in
§B.3's original closed `BaseType` set — the moment a preset can target a `CreditAccount`, it
re-enters the "auto-routing a user-facing choice into a different `TransactionType`" territory
`CreditCardPurchase`'s own existing auto-detection already occupies, and the interaction between
"preset default" and "payment-method-driven routing" wasn't obviously safe without more thought
(e.g. does the preset's default account need to be a `CreditCard`, and if so does
`AddTransactionViewModel` need a new branch to route to `RecordCreditCardPurchaseAsync` from a
preset tap instead of the Expense block?). §B.7 answers this precisely: no new branch is needed —
the existing Expense-block branch already handles it once `SelectedAccount` is resolved from
`PaymentAccounts` instead of `Accounts` for a card-backed preset's default id.
