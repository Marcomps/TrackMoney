# Edit/Delete Accounts, Credit Cards, Loans, and Transactions — Unified Slice Spec

**Phase**: 1/2 bugfix-shaped gap closure, not a new roadmap phase. Closes a systemic, app-wide gap:
every "list + add" screen in this app (`AccountsListPage`, `CreditCardsListPage`, `LoansListPage`,
`HistoryPage`/`TransactionsListPage`) only ever supported Add. Nothing here pulls in Phase 4/5
scope (no sync ids, no conflict resolution, no API auth).

**Origin**: live user feedback (translated): "In Accounts, it doesn't let me edit the accounts I
already have... I can't edit or delete it. The same thing happens with credit cards and with
transactions. Same with loans... it's important to edit or delete in case I made a mistake in
something." Verified via grep: zero edit/delete capability anywhere for `FinancialAccount`,
`CreditCard`, `Loan`, or `Transaction`.

**Explicit non-scope** (stated up front, expanded in §6):
- "Account as budget envelope" framing (user described their paycheck account as their "biweekly
  budget"). Heard, not built here — Budgets are already a separate, category-based concept in this
  codebase; conflating it with a single account's balance is new complexity for a different slice.
- `TermDeposit`/`InvestmentFund` edit/delete (justified in §1.4).
- `InvestmentContribution`/`InvestmentWithdrawal`/`InterestIncome`/`Reimbursement`/
  `CreditCardPurchase`/`CreditCardPayment`/`LoanPayment` edit/delete (justified in §4.3).
- `TransactionsListPage` (quick-summary tab) tap-to-detail (justified in §5).
- Any bulk-edit, any audit-trail/history-of-edits feature, any cloud/sync concern.

---

## 0. One shared mechanism, applied per entity family

Four entity families, but the same three underlying moves, chosen per family based on whether the
entity's fields have side effects on other aggregates:

| Move | When used | Mechanism |
|---|---|---|
| **True in-place edit** | Fields with **no** side effect on any other aggregate (names, notes, institution, credit limit, rates, due-day config, goal amount, etc.) | New small domain mutators (mirroring existing `Rename`/`UpdateNotes`/`SetGoal`/`AdvanceSchedule` precedent), called directly — no reversal needed because nothing was ever "applied" outward. |
| **Deactivate/Reactivate** (already exists, unwired to any UI button) | The entity has *any* history pointing at it (transactions, recurring definitions, statements) | `FinancialAccount.Deactivate()`/`CreditAccount.Deactivate()` — already fully wired everywhere *except* a UI button: `AccountsListViewModel`/`LoansListViewModel`/`CreditCardsListViewModel`/`AddTransactionViewModel` already call `GetActiveAsync`, and `DashboardViewModel`/`NetWorthCalculator` already correctly use `GetAllAsync` for Net Worth regardless of `IsActive` (confirmed by reading both — no changes needed there). This slice only adds the button, a confirmation dialog, and a "show inactive / reactivate" affordance (see §1.3 — without it, Deactivate is a one-way trap). |
| **Hard delete** | The entity has **zero** history pointing at it | `IRepository<T>.Remove` (already exists) + `SaveChangesAsync`, behind a destructive-confirm dialog and a fresh (not UI-cached) re-check of "zero references" immediately before deleting. |
| **Reverse-then-recreate** | `Transaction` subtypes only, because every transaction's whole reason to exist *is* its side effect on an account/card | New `ReverseXAsync` methods (small, symmetric) + the **already-existing, unchanged** `RecordXAsync` methods for the forward half. See §4. |

No EF-level FK constraints exist anywhere in this codebase (every cross-entity reference is a plain
`Guid` property, no `HasOne`/navigation, confirmed by grep — zero `OnDelete`/`DeleteBehavior`
configured). **All deletion guards must therefore be Application-layer existence checks, not
database constraints.** This is the one new piece of infrastructure every family shares:

```csharp
// ITransactionRepository — new methods, needed by every "hard-delete a FinancialAccount/CreditAccount" check
Task<bool> HasAnyTransactionReferencingFinancialAccountAsync(Guid accountId, CancellationToken ct = default);
    // true if accountId appears as Expense.AccountId, Income.DestinationAccountId,
    // Transfer.Source/DestinationAccountId, CreditCardPayment.SourceAccountId,
    // LoanPayment.SourceAccountId, InvestmentContribution.Source/DestinationAccountId,
    // InvestmentWithdrawal.Source/DestinationAccountId, InterestIncome.DestinationAccountId,
    // or Reimbursement.DestinationAccountId -- i.e. every FinancialAccount-shaped FK column across
    // every transaction subtype, not just Expense/Income.

Task<bool> HasAnyTransactionReferencingCreditAccountAsync(Guid creditAccountId, CancellationToken ct = default);
    // true if creditAccountId appears as CreditCardPurchase.CreditAccountId,
    // CreditCardPayment.CreditAccountId, or LoanPayment.CreditAccountId.

// IRecurringExpenseRepository — new method
Task<bool> HasAnyReferencingAccountAsync(Guid accountOrCreditAccountId, CancellationToken ct = default);
    // checks BOTH AccountId and CreditAccountId (exactly one is set per row, per RecurringExpense's
    // own XOR invariant) — checks ALL rows regardless of IsActive: a deactivated recurring
    // expense's AccountId would still dangle if the account were hard-deleted.

// IRecurringIncomeRepository — new method
Task<bool> HasAnyReferencingAccountAsync(Guid accountId, CancellationToken ct = default);
    // checks DestinationAccountId, all rows regardless of IsActive.
```

`ICreditCardStatementRepository.GetForCardAsync` already exists (used by `CreditCardDetailViewModel`)
and is reused as-is (`.Any()`  the result) — no new method needed for the statement check.

Two new, thin Application-layer orchestrators tie the checks together (one per hierarchy, mirroring
how `ICreditAccountRepository` already polymorphically covers both `CreditCard` and `Loan`):

```csharp
namespace TrackTraceMoney.Application.Accounts;

public interface IFinancialAccountLifecycleService
{
    Task<bool> CanHardDeleteAsync(Guid accountId, CancellationToken ct = default);
    Task DeleteAsync(Guid accountId, CancellationToken ct = default);   // re-checks CanHardDeleteAsync itself; throws if false -- never trusts a UI-cached "yes"
    Task DeactivateAsync(Guid accountId, CancellationToken ct = default);
    Task ReactivateAsync(Guid accountId, CancellationToken ct = default);
    Task<bool> CanChangeCurrencyAsync(Guid accountId, CancellationToken ct = default); // same underlying check, reused (see §1.2)
}

namespace TrackTraceMoney.Application.CreditAccounts;

public interface ICreditAccountLifecycleService
{
    Task<bool> CanHardDeleteAsync(Guid creditAccountId, CancellationToken ct = default);
    Task DeleteAsync(Guid creditAccountId, CancellationToken ct = default);
    Task DeactivateAsync(Guid creditAccountId, CancellationToken ct = default);
    Task ReactivateAsync(Guid creditAccountId, CancellationToken ct = default);
    Task<bool> CanChangeCurrencyAsync(Guid creditAccountId, CancellationToken ct = default);
}
```

**UI idiom**: two different, both-already-established MAUI patterns, chosen per what each screen
already has, not one forced visual paradigm:
- `CreditCardDetailPage` already exists and is already tap-reachable from `CreditCardsListPage`
  (`CreditCardsListViewModel` already navigates there on row tap) — Edit/Deactivate/Delete become
  buttons on that existing page.
- `AccountsListPage`/`LoansListPage` have no detail page — rather than inventing two new ones,
  use `SwipeView`/`SwipeItem` per row (already an established, already-themed control in this
  codebase — explicitly listed among the control types covered by the dark-theme pass) for
  Edit/Deactivate/Delete swipe actions.

---

## 1. Accounts (`FinancialAccount` — Cash/Bank/Savings in scope; TermDeposit/InvestmentFund deferred)

### 1.1 What's safely editable in place (confirmed by reading `FinancialAccount.cs` and each subtype)

`Balance` has no direct setter anywhere except `Credit`/`Debit`, both called only from
`TransactionEntryService` when posting a transaction. **Balance is never directly editable** —
confirmed, not assumed. There is also no separately-stored "opening balance" field to correct
after the fact (the constructor writes `openingBalance` straight into `Balance`); if a user's
starting balance was wrong, the decided fix is the same one a real bank statement correction uses:
record an adjusting `Income`/`Expense`/`Transfer`, not a direct balance edit. Stated explicitly so
this isn't silently missing — it's a deliberate exclusion.

| Field | Editable in place? | Mechanism |
|---|---|---|
| `Name` | Yes | `Rename` (exists) |
| `Notes` | Yes | `UpdateNotes` (exists) |
| `Currency` | **Only if zero transactions reference the account** (see below) | New `UpdateCurrency`, gated by `IFinancialAccountLifecycleService.CanChangeCurrencyAsync` |
| `Balance` | **No** | N/A — derived from ledger, correct via adjusting transaction |
| `BankAccount.InstitutionId` | Yes | New `UpdateInstitution(Guid? institutionId)` — deliberately allows `null` (BankAccount's institution is optional, unlike CreditCard/Loan), so the existing `SetInstitutionId` (throws on `Guid.Empty`, backfill-only) can't be reused as-is |
| `BankAccount.AccountNumberLast4` | Yes | New `UpdateAccountNumber(string? last4)` |
| `SavingsAccount.GoalAmount` | Yes | `SetGoal` (exists, already used at construction — reused verbatim) |

**Currency decision**: mutable only when `HasAnyTransactionReferencingFinancialAccountAsync` is
false for that account — reusing the exact same check built for the deletion guard (one check,
two call sites: "can I delete this" and "can I change its currency"). Rationale: every `Balance`
figure accumulated so far is implicitly denominated in the *current* currency; changing currency
after even one transaction would silently relabel real money without converting it (no FX feature
exists per CLAUDE.md's "explicit currency, no global conversion" rule) — this mirrors
`RecordTransferAsync`'s existing same-currency guard, just applied at edit-time instead of
transfer-time.

### 1.2 Deletion guard

Hard-delete allowed only if **all** of:
1. `HasAnyTransactionReferencingFinancialAccountAsync(accountId)` is false.
2. `IRecurringExpenseRepository.HasAnyReferencingAccountAsync(accountId)` is false.
3. `IRecurringIncomeRepository.HasAnyReferencingAccountAsync(accountId)` is false.

If any is true → hard delete is blocked; only **Deactivate** is offered (with an explanatory
message, not a disabled button with no explanation).

### 1.3 Deactivate needs a Reactivate path, or it's a one-way trap

Every account list ViewModel already calls `GetActiveAsync` exclusively — there is currently *no*
UI path that shows a deactivated account at all. Shipping a Deactivate button without also adding
a "show inactive accounts" toggle (or a separate section) plus a Reactivate action would mean an
accidental tap silently and irreversibly hides an account from the user's own view, with the only
recovery path being direct database editing. **In scope, not optional**: `AccountsListPage` gets a
"Show inactive" toggle; inactive rows get a Reactivate swipe action instead of Edit/Delete.

### 1.4 `TermDeposit`/`InvestmentFund` — deferred, justified

Both are excluded from this slice's account-edit/delete support:
- `TermDeposit.InterestReceived` is a running total fed only by `InterestIncome` transactions,
  and `RenewAtMaturity` derives a brand-new deposit from `Balance`/`MaturityDate` — editing or
  deleting a term deposit mid-life interacts with auto-renewal eligibility and an
  `InterestReceived` figure that (like `Loan`'s schedule, see §4.3) has no reversible history once
  `RecordInterestReceived` has run, because `InterestIncome` edit/delete is *also* deferred (§4.3).
- `InvestmentFund.Contributions`/`Withdrawals` are running totals fed only by
  `InvestmentContribution`/`InvestmentWithdrawal` (also deferred, §4.3), and it additionally has a
  child `InvestmentValuation` history whose *absolute-value* snapshots make "delete the fund"
  especially fraught (orphaned valuations, `Gain`/`ReturnPercentage` computed from a suddenly
  partial history).
- Shipping "edit the fund's Name but never let the user fix a wrong `Contributions` figure" would
  be a half-capability — the two deferrals (account-side, transaction-side) are kept consistent
  rather than shipping one without the other.

---

## 2. `CreditCard`

### 2.1 Editable in place (confirmed by reading `CreditCard.cs`)

`AmountOwed` (inherited from `CreditAccount`) has no direct setter — only `RegisterCharge`/
`RegisterPayment`. **Never directly editable**, same reasoning as `FinancialAccount.Balance`.

| Field | Editable? | Mechanism |
|---|---|---|
| `Name`, `Notes` | Yes | `Rename`/`UpdateNotes` (exist, on `CreditAccount` base) |
| `Currency` | Only if zero transactions reference the card | New `UpdateCurrency`, same gated pattern as §1.1 |
| `InstitutionId`, `NetworkId`, `LastFourDigits`, `CreditLimit`, `StatementCutOffDay`, `PaymentDueDay`, `AnnualInterestRate`, `MonthlyInterestRate` | Yes — none of these feed any derived invariant (`AvailableCredit` is computed live from `CreditLimit`/`AmountOwed`, never cached) | New `UpdateDetails(...)` mutator, validation mirrors the constructor's |
| `AmountOwed` | **No** | N/A |

### 2.2 Deletion guard

Hard-delete allowed only if **all** of:
1. `HasAnyTransactionReferencingCreditAccountAsync(creditAccountId)` is false.
2. `IRecurringExpenseRepository.HasAnyReferencingAccountAsync(creditAccountId)` is false.
3. `ICreditCardStatementRepository.GetForCardAsync(creditAccountId)` returns empty.

Otherwise → Deactivate only, with the same Reactivate-visibility requirement as §1.3 applied to
`CreditCardsListPage`.

### 2.3 UI

Buttons on the existing `CreditCardDetailPage` (already tap-reachable): Edit (opens
`AddCreditCardPage` in edit mode, see §0/§1.1's mutator, prefilled from the card), Deactivate,
Delete (only enabled/offered when `CanHardDeleteAsync` is true — shown with an explanation when
it's not, not just hidden, so the user understands *why*).

---

## 3. `Loan`

### 3.1 Editable in place (confirmed by reading `Loan.cs`)

`AmountOwed` — same as above, never directly editable (only `RegisterPayment`).
`OriginalAmount` — the entity's own doc comment states it is "Fixed for the life of the loan in
this model — never mutated after construction." Honoring that stated invariant: **no mutator is
added for it**, deliberately, not an oversight.

| Field | Editable? | Mechanism |
|---|---|---|
| `Name`, `Notes` | Yes | Existing base mutators |
| `Currency` | Only if zero transactions reference the loan | Same gated pattern |
| `InstitutionId`, `Kind`, `InterestRate`, `RateType`, `MonthlyInstallment`, `Fees` | Yes — all informational or independent of any derived value (`RemainingPayments` is computed live from `AmountOwed`/`MonthlyInstallment`, never cached) | New `UpdateDetails(...)` mutator |
| `NextPaymentDate`, `RequiredPayment` | Yes | **Reuses the existing `AdvanceSchedule(DateOnly, decimal)`** verbatim — it already does exactly "set these two fields," with no side effect beyond that; no new mutator needed |
| `AmountOwed`, `OriginalAmount` | **No** | N/A |

### 3.2 Deletion guard

Same shape as §2.2, minus the statement check (loans have no `CreditCardStatement` concept):
1. `HasAnyTransactionReferencingCreditAccountAsync(loanId)` false.
2. `IRecurringExpenseRepository.HasAnyReferencingAccountAsync(loanId)` false — loans aren't
   currently wired as a `RecurringExpense` target in this codebase, but the check costs nothing
   and future-proofs against that changing.

### 3.3 UI

No `LoanDetailPage` exists today — mirrors §1's decision: `SwipeView`/`SwipeItem` rows on
`LoansListPage` for Edit/Deactivate/Delete, same Reactivate-visibility requirement as §1.3.

---

## 4. `Transaction`

### 4.1 UX paradigm — refined from "delete + prefilled re-add" to "edit-in-place-of-the-list, reverse-and-repost-on-Save"

The coordinator's suggested paradigm (delete first, prefill a new Add screen, let the user re-save)
was reconsidered and **tightened by one step**, because the naive version has a real data-loss
window: if the user backs out of the prefilled Add screen after the delete already happened, the
original transaction is genuinely, silently gone.

**Decided design**: `AddTransactionPage`/`AddTransactionViewModel` gains an optional
`editingTransactionId` query parameter (same `[QueryProperty]`-as-string idiom already used
everywhere else in this codebase, e.g. `CreditCardDetailViewModel.CreditAccountIdText`).
- **On load**, if present: fetch the transaction **read-only** (no mutation), prefill every field
  (date, amount, account(s), category, payer/beneficiary, description, notes), lock the
  transaction-type selector to the original type (can't turn an edited `Expense` into a
  `Transfer`), and retitle the page "Edit".
- **On Save**: call a new `ReverseXAsync(transactionId)` (reverses the *original* side effect,
  fetched fresh from the repository — never trusts in-memory/stale state) immediately followed by
  the **existing, unchanged** `RecordXAsync(...)` with the edited field values.
- **If the user cancels/backs out at any point before Save**, nothing has happened — the original
  transaction is untouched. This is strictly safer than the delete-first version while still
  reusing 100% of the existing, already-tested forward-posting logic unchanged, confining every
  new line of logic to the three small `ReverseXAsync` methods below.

**Delete** (standalone, no editing involved) is simpler: confirm dialog → `ReverseXAsync` →
navigate back. Same three methods serve both Delete and Edit's first half.

```csharp
// ITransactionEntryService — three new methods, one per in-scope type
Task ReverseExpenseAsync(Guid transactionId, CancellationToken ct = default);
Task ReverseIncomeAsync(Guid transactionId, CancellationToken ct = default);
Task ReverseTransferAsync(Guid transactionId, CancellationToken ct = default);
```

```csharp
public async Task ReverseExpenseAsync(Guid transactionId, CancellationToken ct = default)
{
    var transaction = await _transactionRepository.GetByIdAsync(transactionId, ct)
        ?? throw new InvalidOperationException($"Transaction '{transactionId}' was not found.");
    if (transaction is not Expense expense)
        throw new InvalidOperationException($"Transaction '{transactionId}' is not an expense.");

    var account = await _accountRepository.GetByIdAsync(expense.AccountId, ct)
        ?? throw new InvalidOperationException($"Account '{expense.AccountId}' was not found.");

    account.Credit(expense.Amount);          // exact inverse of RecordExpenseAsync's account.Debit(amount)
    _transactionRepository.Remove(expense);
    await _transactionRepository.SaveChangesAsync(ct);
}
// ReverseIncomeAsync: destinationAccount.Debit(income.Amount); Remove; Save.
// ReverseTransferAsync: re-fetch BOTH accounts fresh; sourceAccount.Credit(amount);
//   destinationAccount.Debit(amount); Remove; Save. (Mirrors RecordTransferAsync's own
//   fetch-both-fresh pattern -- never trusts a caller-supplied account object.)
```

No overdraft/negative-balance guard is added to the reversal — `FinancialAccount.Debit`/`Credit`
have none today (confirmed by reading `FinancialAccount.cs`), so reversal can't newly fail where
the original posting couldn't; this keeps the reversal symmetric with forward-posting rather than
inventing a new validation rule mid-slice.

**Known, accepted tradeoff**: Edit is two separate `SaveChangesAsync` commits (reverse, then
record), not one atomic transaction. A crash/write failure in the narrow window between them would
leave the transaction genuinely deleted with no replacement. Accepted because (a) SQLite commits
here are fast, local, single-process writes, not network calls — the window is microseconds, not
seconds, and (b) building a bespoke single-transaction orchestrator that duplicates
`RecordExpenseAsync`'s body would add more new, unreviewed code than it removes risk. Flagged
explicitly rather than silently ignored.

**Budget-crossing notifications**: deleting/editing away the `Expense` that originally crossed a
budget doesn't "un-notify" (no notification-log entity exists to retract) — accepted as-is, no
action needed, because the crossing check (`GetBudgetCrossingContextAsync`) is always computed
fresh from currently-persisted transactions at the *next* posting, never cached — so the very next
expense in that category will correctly reflect the lower total. Confirmed self-correcting by
design, not by omission.

### 4.2 In scope for this slice: `Expense`, `Income`, `Transfer`

Chosen because their reversal is structurally simple (one or two `Credit`/`Debit` calls, no
secondary entity state) and together they cover the overwhelming majority of day-to-day entries —
and because deferring every other type is independently justified below, not just "the easy ones
first."

### 4.3 Deferred, with reasons (not silently dropped)

| Type | Why deferred |
|---|---|
| `CreditCardPurchase` | Reversal must undo `CreditAccount.AmountOwed` **and** must be blocked once a `CreditCardStatement` already covers its date (i.e. `transaction.Date <= card's latest statement's CycleEndDate`) — that statement's `MinimumPayment`/`PayInFullAmount` were fixed against the pre-edit purchase total; silently letting a purchase inside an already-statemented cycle change would desync numbers CLAUDE.md calls out as user-entered and never re-derived. Real, distinct guard logic not yet built. |
| `CreditCardPayment` | Reversal must undo `AmountOwed` on the liability side; same statement-cycle guard risk as above, on the payment side. |
| `LoanPayment` | **Not just riskier — genuinely not safely reversible today.** `AdvanceSchedule` *overwrites* `NextPaymentDate`/`RequiredPayment` with no history retained anywhere. Reversing a `LoanPayment` would need to restore the loan's *prior* `NextPaymentDate`/`RequiredPayment`, which no longer exist once `AdvanceSchedule` has run — true even for the most recent payment. Fixing this needs a schedule-history mechanism that doesn't exist; out of scope here, not hand-waved. |
| `InvestmentContribution` / `InvestmentWithdrawal` / `InterestIncome` / `Reimbursement` | Consistent with §1.4's account-side deferral of `TermDeposit`/`InvestmentFund` — shipping transaction-level reversal for these while their target account type has no edit/delete support at all would be a mismatched half-capability. `Reimbursement` additionally couples to `MedicalExpenseDetail.Status` (Pending→Reimbursed) and interacts with the existing `MarkRejected` flow — a second, independent complication on top of the Phase-3 alignment reason. |

### 4.4 Interaction with Account/CreditCard/Loan deletion (the cross-cutting guard rail)

This is exactly why §1.2/§2.2/§3.2's deletion guards check **every** transaction-shaped FK column,
not just the in-scope types' columns: even though `CreditCardPurchase`/`LoanPayment`/etc. can't be
*edited or deleted* in this slice, an account/card/loan referenced by one of them must still be
blocked from hard-delete — otherwise deleting the account would orphan a transaction this slice
can't repair. The deletion-order rule across all four entity types, stated once: **a
FinancialAccount/CreditCard/Loan can never be hard-deleted while any Transaction (of any type,
in-scope or not) still references it.** Deactivate is always available as the fallback regardless.

---

## 5. UI entry point (`HistoryPage`)

`HistoryViewModel.OpenMedicalExpenseDetailCommand` gets a general-purpose sibling,
`OpenTransactionDetailCommand`, with this exact routing (medical takes priority, unchanged):

```csharp
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

    if (entry.Type is TransactionType.Expense or TransactionType.Income or TransactionType.Transfer)
    {
        await Shell.Current.GoToAsync($"{nameof(TransactionDetailPage)}?transactionId={entry.Id}");
        return;
    }

    // Every other type (CreditCardPurchase, CreditCardPayment, LoanPayment, InvestmentContribution/
    // Withdrawal, InterestIncome, Reimbursement): intentionally still a no-op, same as today's
    // behavior -- but now by deliberate exclusion (§4.3), not accident. No toast/disabled-row
    // treatment added this slice; revisit once/if those types get their own edit/delete support.
}
```

New `TransactionDetailPage`/`TransactionDetailViewModel`: shows the transaction's fields read-only
plus Edit (→ `AddTransactionPage?editingTransactionId=...`, §4.1) and Delete (→ confirm dialog →
`ReverseXAsync`, §4.1) buttons.

**`TransactionsListPage` (the quick-summary tab) — deferred, not silently dropped.** `HistoryPage`
is this app's authoritative, full-featured transaction browser (richer per-row data via
`HistoryEntryItem`, already has the `MedicalStatusBadge` plumbing this routing depends on);
`TransactionsListPage` is a thinner "current month at a glance" widget whose item model wasn't
verified to expose the same `Id`/`Type` fields. Wiring the identical command there is a near-zero-
cost fast-follow once this path is proven on History, not a meaningfully different feature —
deferred to keep this already-large unified slice bounded, stated explicitly rather than assumed
in scope.

---

## 6. Explicit non-scope (full list)

- **"Account as budget envelope"** — heard from the user's own framing of their paycheck account,
  deliberately not built here; Budgets already exist as a separate category-based concept and
  conflating the two is new complexity for its own future slice.
- `TermDeposit`/`InvestmentFund` edit/delete (§1.4).
- `CreditCardPurchase`/`CreditCardPayment`/`LoanPayment`/`InvestmentContribution`/
  `InvestmentWithdrawal`/`InterestIncome`/`Reimbursement` edit/delete (§4.3).
- `TransactionsListPage` tap-to-detail (§5).
- Any bulk-edit / multi-select delete.
- Any audit trail or "edit history" of a transaction/account (this slice's edit is
  reverse-and-repost or true in-place mutation, not a tracked-diff model).
- Loan/CreditCard schedule-history (needed to ever safely reverse a `LoanPayment` — noted as a
  prerequisite for lifting that deferral, not designed here).
- Currency conversion / FX (referenced only as the reason `Currency` edits are gated to
  zero-transaction accounts — no conversion feature is being added).

---

## 7. Decision summary table

| Question | Decision |
|---|---|
| Overall UX paradigm | Mixed by entity family: true in-place edit for Account/CreditCard/Loan (no side effects to reverse); reverse-then-repost for Transaction (side effects are the whole point) |
| Is Balance/AmountOwed ever directly editable? | No, for any entity — always derived from the ledger; corrected via an adjusting transaction, confirmed by reading every mutator |
| Currency edit | Allowed only when zero transactions reference the account/card/loan; reuses the same existence check built for the deletion guard |
| Deletion guard mechanism | Application-layer existence checks across every FK-shaped column (no DB constraints exist); block hard-delete, offer Deactivate, if any reference exists |
| Deactivate needs Reactivate UI | Yes, non-optional — otherwise a one-way trap given every list VM already filters to `GetActiveAsync` only |
| Net Worth / available-balance impact of Deactivate | Already correct as shipped — `NetWorthCalculator` consumers already use `GetAllAsync`, "available balance"/new-transaction pickers already use `GetActiveAsync`. Confirmed by reading `DashboardViewModel`, no changes needed |
| Transaction types in scope | `Expense`, `Income`, `Transfer` only |
| Transaction types deferred | `CreditCardPurchase`/`CreditCardPayment` (statement-cycle desync risk), `LoanPayment` (schedule history is unrecoverable once advanced — hard blocker, not just risk), `InvestmentContribution`/`Withdrawal`/`InterestIncome`/`Reimbursement` (Phase-3 alignment with deferred account types + Reimbursement's extra `MedicalExpenseDetail` coupling) |
| Delete-only vs Edit-only vs both | Both, for Account/CreditCard/Loan (same mechanism serves both) and for Transaction (Delete = half of Edit) |
| Edit implementation risk containment | Every new Transaction-side method is a small, symmetric reversal; every existing `RecordXAsync` forward-posting method is reused completely unchanged |
| HistoryPage routing | Medical detail takes priority (unchanged); Expense/Income/Transfer → new `TransactionDetailPage`; everything else stays a silent no-op, now by documented exclusion |
| TransactionsListPage | Deferred fast-follow, not in this slice |

---

## 8. Suggested build order (independently shippable slices)

1. Shared repository existence-check methods (§0) — no UI, fully unit-testable in isolation.
2. `IFinancialAccountLifecycleService`/`ICreditAccountLifecycleService` + new domain mutators
   (§1.1/§2.1/§3.1) — Application/Domain only, unit-testable without any screen.
3. Account family UI: `AddAccountPage` edit mode, `AccountsListPage` swipe actions + inactive
   toggle (§1).
4. CreditCard family UI: `AddCreditCardPage` edit mode, `CreditCardDetailPage` buttons (§2).
5. Loan family UI: `AddLoanPage` edit mode, `LoansListPage` swipe actions + inactive toggle (§3).
6. Transaction reversal methods (`ReverseExpenseAsync`/`ReverseIncomeAsync`/`ReverseTransferAsync`)
   — Application layer only, unit-testable against the same fixtures the forward `RecordXAsync`
   tests already use.
7. `TransactionDetailPage` + `AddTransactionPage` edit-mode + `HistoryViewModel` routing (§4/§5).

Each slice is independently completable end-to-end through Domain→Application→Infrastructure→App
without leaving any layer half-built, per this repo's own architecture rule.
