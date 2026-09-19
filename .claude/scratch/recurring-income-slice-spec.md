# Recurring Income — Slice Spec

**Phase**: 1 (MVP) — this is a variant of the already-shipped Phase 1 "recurring expenses"
capability (README recurring-expenses story), not a new roadmap phase. Nothing here pulls in
Phase 2+ scope (no card/loan linkage, no cloud/sync concerns).

**Origin**: direct user feedback (live testing) — wants salary entered once, editable when it
changes (a raise), viewable as both a biweekly and a monthly figure. A second, independent
example (house rented out: $156/mo expense side already covered by the shipped
`RecurringExpense`; $160/mo rental income is explicitly "just extra" — no netting/linking wanted).

**Explicit non-scope** (user's own words honored literally):
- No link from a `RecurringIncome` to any `RecurringExpense` — they stay fully independent
  entities, same as the user's own $156-out/$160-in example ("just extra").
- No "Property" entity.
- No automatic net-margin calculation between a recurring expense and a recurring income.
- Occasional repair costs the user mentioned are just normal one-off `Expense` entries — nothing
  in this slice models them specially.

---

## 1. Domain entity: `RecurringIncome`

New file `src/TrackTraceMoney.Domain/RecurringIncomes/RecurringIncome.cs`, namespace
`TrackTraceMoney.Domain.RecurringIncomes`. Shape mirrors `RecurringExpense`
(`src/TrackTraceMoney.Domain/RecurringExpenses/RecurringExpense.cs`) with two deliberate
differences (no credit-card duality; a new `UpdateAmount` mutator) explained below.

```csharp
public sealed class RecurringIncome : Entity
{
    public string Name { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public RecurringIncomeFrequency Frequency { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public DateOnly? LastConfirmedDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    private RecurringIncome() { }

    public RecurringIncome(
        string name, decimal amount, Guid categoryId, Guid destinationAccountId,
        RecurringIncomeFrequency frequency, DateOnly startDate, DateOnly? endDate)
    {
        // Same validation as RecurringExpense's private ctor: non-empty trimmed name,
        // amount > 0, endDate >= startDate if present.
        ...
    }

    public DateOnly NextOccurrenceDate =>
        LastConfirmedDate is null ? StartDate : Advance(LastConfirmedDate.Value);

    private DateOnly Advance(DateOnly d) => Frequency switch
    {
        RecurringIncomeFrequency.Weekly => d.AddDays(7),
        RecurringIncomeFrequency.Biweekly => d.AddDays(14),
        RecurringIncomeFrequency.Monthly => d.AddMonths(1),
        RecurringIncomeFrequency.Yearly => d.AddYears(1),
        _ => throw new InvalidOperationException($"Unknown frequency '{Frequency}'.")
    };

    public bool IsDue(DateOnly asOf) => IsActive && NextOccurrenceDate <= asOf
        && (EndDate is null || NextOccurrenceDate <= EndDate.Value);

    public int CountOccurrencesThrough(DateOnly horizonEnd) { /* identical algorithm to
        RecurringExpense.CountOccurrencesThrough, just walking this entity's Advance() */ }

    public void MarkConfirmed(DateOnly occurrenceDate)
    {
        if (occurrenceDate != NextOccurrenceDate)
            throw new InvalidOperationException("Can only confirm the currently-due occurrence.");
        LastConfirmedDate = occurrenceDate;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Changes the recurring amount going forward (e.g. a salary raise) — never touches any
    /// already-posted Income transaction, LastConfirmedDate, or past occurrences. See Decision A
    /// below for why this is prospective-only and why it's a plain mutator, not a history-tracked
    /// "amount schedule."
    /// </summary>
    public void UpdateAmount(decimal newAmount)
    {
        if (newAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(newAmount), "Recurring income amount must be positive.");
        Amount = newAmount;
    }
}
```

### Decision A — `UpdateAmount`, prospective-only, no recreation required

The user explicitly wants to edit the amount later (a raise) without recreating the whole
recurring definition. Mirrors the existing `Budget.UpdateAmount(decimal amount)` convention
(`src/TrackTraceMoney.Domain/Budgets/Budget.cs:41`) verbatim in shape (single positive-amount
validation, direct field set, no side effects). This is the right precedent to copy rather than
`InvestmentValuation.UpdateValuation` or `CreditCardStatement.UpdateAmounts`, because both of
those exist to correct a value tied to a specific dated snapshot (a valuation-as-of-date, a
specific statement), whereas `Budget.Amount` — like `RecurringIncome.Amount` — is a live,
ongoing configuration value with no "as of" dimension.

**Prospective-only is a hard decision, not left open**: `UpdateAmount` only ever changes
`Amount` on the live entity. It never touches `LastConfirmedDate`, never rewrites any
already-posted `Income` transaction, and confirming a *future* due occurrence after the raise
posts the *new* amount (since `RecurringIncomeService.ConfirmOccurrenceAsync` always reads
`recurringIncome.Amount` at confirm-time, not at definition-time — see §2). This matches every
other entity's "past transactions are immutable" convention in this codebase (e.g.
`RecurringExpense.MarkConfirmed`'s occurrence-date immutability, `Reimbursement`'s "never
retroactively edits the original expense" rule in CLAUDE.md). A history-tracked "amount
schedule" (effective-dated amount rows) was considered and rejected as unnecessary complexity —
nothing in the user's ask ("editable... in case my salary changes") requires reconstructing what
the amount *used to be* at a past date; the already-posted `Income` transactions themselves are
the permanent historical record of what was actually received each period.

### Decision B — no credit-card duality (unlike `RecurringExpense`)

`RecurringExpense` has `AccountId`/`CreditAccountId` (exactly one set) because an expense can be
charged to a credit card. Income has no analogous concept — `Income`
(`src/TrackTraceMoney.Domain/Transactions/Income.cs`) only ever has a single
`DestinationAccountId: Guid` (non-nullable) crediting a `FinancialAccount`; there is no "credit
card receives income" case anywhere in the domain. So `RecurringIncome` has a single
non-nullable `DestinationAccountId`, one public constructor, no `ForCreditCard` factory, no
`IsCreditCardBacked` — simpler than `RecurringExpense`, not a parallel-but-padded copy.

### Decision C — no `PersonId` field

`Income` itself supports an optional `PersonId` (who received it). `RecurringExpense`, however,
deliberately does **not** capture any Person field even though `Expense` supports
payer/beneficiary — see `RecurringExpenseService.ConfirmOccurrenceAsync`'s own comment:
*"payerPersonId/beneficiaryPersonId stay null on both branches — RecurringExpense never captured
Person fields."* `RecurringIncome` mirrors that same precedent exactly: no `PersonId` field on
the recurring definition; `RecurringIncomeService` always confirms with `personId: null`. Nothing
in the user's ask needs a person on the recurring definition, and adding it would be scope
creep beyond what `RecurringExpense`'s established shape does for the symmetric case.

### `RecurringIncomeFrequency` — new enum, not a `RecurringExpenseFrequency` extension

New file `src/TrackTraceMoney.Domain/RecurringIncomes/RecurringIncomeFrequency.cs`:

```csharp
/// <summary>
/// Fixed set of cadences a <see cref="RecurringIncome"/> can repeat on. Mirrors
/// RecurringExpenseFrequency's "fixed set, not arbitrary N-day interval" shape, but is its own
/// enum — see the slice spec's "why not reuse RecurringExpenseFrequency" decision.
/// </summary>
public enum RecurringIncomeFrequency
{
    Weekly,
    Biweekly,
    Monthly,
    Yearly
}
```

**Decision D — new enum, do not add `Biweekly` to `RecurringExpenseFrequency`.** The existing
`RecurringExpenseFrequency` (`src/TrackTraceMoney.Domain/RecurringExpenses/RecurringExpenseFrequency.cs`)
has exactly `Weekly/Monthly/Yearly` and is consumed by `RecurringExpense.Advance()`'s exhaustive
switch, `RecurringExpenseFrequencyToLabelConverter`, shipped resx strings in both languages, and
185/185 already-green Domain tests plus the Dashboard real-surplus calculation. Adding a new enum
member is additive and wouldn't break any of that — but nothing in the expense side of this
domain has ever needed a biweekly cadence, and widening a shared, already-shipped enum purely to
serve a second, unrelated entity's need is unnecessary coupling for zero reuse benefit (the two
entities don't share an `Advance()` implementation or any other logic — each has its own private
`Advance` method already). A small, independently-scoped `RecurringIncomeFrequency` costs one
more small file and has zero blast radius on `RecurringExpense`'s existing, tested behavior.

---

## 2. Confirming a due occurrence

New `src/TrackTraceMoney.Application/RecurringIncomes/IRecurringIncomeService.cs` +
`RecurringIncomeService.cs`, structurally identical to
`src/TrackTraceMoney.Application/RecurringExpenses/RecurringExpenseService.cs`:

```csharp
public interface IRecurringIncomeService
{
    Task ConfirmOccurrenceAsync(Guid recurringIncomeId, CancellationToken ct = default);
}

public sealed class RecurringIncomeService : IRecurringIncomeService
{
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;
    private readonly ITransactionEntryService _transactionEntryService;
    private readonly IUnitOfWork _unitOfWork;

    public async Task ConfirmOccurrenceAsync(Guid recurringIncomeId, CancellationToken ct = default)
    {
        var recurringIncome = await _recurringIncomeRepository.GetByIdAsync(recurringIncomeId, ct)
            ?? throw new InvalidOperationException($"Recurring income '{recurringIncomeId}' was not found.");

        // Same reasoning as RecurringExpenseService: capture before RecordIncomeAsync/MarkConfirmed
        // run, since NextOccurrenceDate shifts once LastConfirmedDate changes.
        var occurrenceDate = recurringIncome.NextOccurrenceDate;

        // Same atomicity requirement as RecurringExpenseService: posting the Income (which credits
        // the account inside RecordIncomeAsync's own SaveChangesAsync) and marking this recurring
        // income confirmed (a second SaveChangesAsync) must commit or roll back together, or a retry
        // after a partial failure double-posts the same real-world deposit.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        using var ambientScope = transaction.EnterAmbientScope();
        try
        {
            await _transactionEntryService.RecordIncomeAsync(
                date: occurrenceDate,
                amount: recurringIncome.Amount,   // always the CURRENT amount — see Decision A
                destinationAccountId: recurringIncome.DestinationAccountId,
                categoryId: recurringIncome.CategoryId,
                personId: null,                    // see Decision C
                description: recurringIncome.Name,
                notes: null,
                ct: ct);

            recurringIncome.MarkConfirmed(occurrenceDate);
            await _recurringIncomeRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

Calls `ITransactionEntryService.RecordIncomeAsync(DateOnly date, decimal amount, Guid
destinationAccountId, Guid categoryId, Guid? personId, string? description, string? notes,
CancellationToken ct = default)` — its exact existing signature
(`src/TrackTraceMoney.Application/Transactions/TransactionEntryService.cs:81-100`), unchanged.
`RecordIncomeAsync` already does the right thing with no modification needed: constructs a real
`Income`, credits the destination `FinancialAccount` via `destinationAccount.Credit(amount)`,
persists it. No budget-crossing check exists on the income path today (correct — budgets are an
expense-side concept, README §34) and this slice doesn't add one.

New `src/TrackTraceMoney.Application/Abstractions/IRecurringIncomeRepository.cs`:

```csharp
public interface IRecurringIncomeRepository : IRepository<RecurringIncome>
{
    Task<IReadOnlyList<RecurringIncome>> GetActiveAsync(CancellationToken ct = default);
}
```

— identical shape to `IRecurringExpenseRepository`.

---

## 3. "Enter once, see biweekly and monthly breakdowns"

**Decision E — this is a projection off the entered `Amount`/`Frequency`, not a new report
screen, and not a change to `IncomeVsExpensesReportViewModel`.**

`IncomeVsExpensesReportViewModel` (`src/TrackTraceMoney.App/ViewModels/IncomeVsExpensesReportViewModel.cs`)
sums *actually-posted* `Income`/`Expense` transactions for a selected month — it's a historical
report, not a projection surface. Once the user confirms the first due occurrence of a
`RecurringIncome`, a real `Income` transaction is posted and that report already picks it up for
that month with zero changes needed there. Reusing/extending it to also show a *projected*
biweekly/monthly figure for a not-yet-confirmed recurring definition would blur "what has
actually happened" with "what's scheduled" inside the same report tile — the same category of
conflation CLAUDE.md flags for "saldo disponible" vs. "sobrante real." Keep them separate:
`IncomeVsExpensesReportViewModel` is untouched by this slice.

Instead, the biweekly/monthly ask is answered directly where the user enters/views the
definition: a small, pure, no-I/O calculator converts the entered `Amount` + `Frequency` into the
two equivalent figures, displayed on the recurring-income list row (and the Add screen, live, as
the user types). New `src/TrackTraceMoney.Application/RecurringIncomes/RecurringIncomeEquivalentCalculator.cs`:

```csharp
/// <summary>
/// Converts a recurring income's entered Amount+Frequency into biweekly/monthly equivalent
/// figures for display (README recurring-income story) — a pure projection off the recurring
/// definition itself, not derived from posted transaction history. Static, not an injected
/// interface+implementation like ISpendingCalculator/IIncomeCalculator: this takes no
/// repository/I/O dependency at all (just a decimal and an enum), mirroring the existing
/// static-pure-helper convention already used in this codebase for the same reason (e.g.
/// ReportBarWidth.Compute, FinancialHealthEvaluator.Evaluate) rather than the DI-calculator
/// convention reserved for calculators that aggregate across repositories/currencies.
/// </summary>
public static class RecurringIncomeEquivalentCalculator
{
    // Standard periods-per-year used to convert between cadences.
    private const int WeeksPerYear = 52;
    private const int BiweeklyPeriodsPerYear = 26;
    private const int MonthsPerYear = 12;

    public static decimal ToMonthlyEquivalent(decimal amount, RecurringIncomeFrequency frequency) =>
        frequency switch
        {
            RecurringIncomeFrequency.Weekly => amount * WeeksPerYear / MonthsPerYear,
            RecurringIncomeFrequency.Biweekly => amount * BiweeklyPeriodsPerYear / MonthsPerYear,
            RecurringIncomeFrequency.Monthly => amount,
            RecurringIncomeFrequency.Yearly => amount / MonthsPerYear,
            _ => throw new InvalidOperationException($"Unknown frequency '{frequency}'.")
        };

    public static decimal ToBiweeklyEquivalent(decimal amount, RecurringIncomeFrequency frequency) =>
        frequency switch
        {
            RecurringIncomeFrequency.Weekly => amount * WeeksPerYear / BiweeklyPeriodsPerYear,
            RecurringIncomeFrequency.Biweekly => amount,
            RecurringIncomeFrequency.Monthly => amount * MonthsPerYear / BiweeklyPeriodsPerYear,
            RecurringIncomeFrequency.Yearly => amount / BiweeklyPeriodsPerYear,
            _ => throw new InvalidOperationException($"Unknown frequency '{frequency}'.")
        };
}
```

This directly satisfies "enter it once and see biweekly and monthly breakdowns": a salary entered
as `Biweekly $X` shows its `Monthly` equivalent alongside it (and vice versa for a salary entered
`Monthly`) — both figures always visible, computed from the one entry, no duplicate data entry,
no report screen, no new persisted fields (both are derived, never stored — same "don't persist
what you can compute" posture as `RecurringExpense.NextOccurrenceDate` being a computed property
rather than a stored column).

---

## 4. UI

### List + Add screens (mirror `RecurringExpensesListPage`/`AddRecurringExpensePage` exactly)

New files, structurally identical to their `RecurringExpense` counterparts:

- `src/TrackTraceMoney.App/Models/RecurringIncomeListItem.cs` — same shape as
  `RecurringExpenseListItem` (`Id, Name, Amount, CategoryName, AccountName, Frequency,
  NextDueDate, IsActive`) plus two derived display fields, `MonthlyEquivalent` and
  `BiweeklyEquivalent`, computed via `RecurringIncomeEquivalentCalculator` in `FromDomain(...)`.
- `src/TrackTraceMoney.App/Models/DueRecurringIncomeListItem.cs` — same shape as
  `DueRecurringExpenseListItem` (`Id, Name, Amount, CategoryName, AccountName, OccurrenceDate`),
  built via `FromListItem(RecurringIncomeListItem)` the same way.
- `src/TrackTraceMoney.App/ViewModels/RecurringIncomesListViewModel.cs` — same structure as
  `RecurringExpensesListViewModel`: `LoadRecurringIncomesCommand` (builds `DueRecurringIncomes` +
  `RecurringIncomes` collections, resolving category/account display names the same
  `NameOf(dictionary, id)` way), `ConfirmCommand` (calls
  `IRecurringIncomeService.ConfirmOccurrenceAsync`, same try/catch → `ErrorMessage` pattern, same
  reload-both-lists-after-confirm comment about a confirmed occurrence immediately becoming due
  again), `DeactivateCommand` (loads entity, calls `Deactivate()`, saves), `AddRecurringIncomeCommand`
  (navigates to `AddRecurringIncomePage`).
- `src/TrackTraceMoney.App/ViewModels/AddRecurringIncomeViewModel.cs` — same structure as
  `AddRecurringExpenseViewModel`: `LoadOptionsAsync` populates `Categories` (all categories,
  unfiltered — mirrors `AddTransactionViewModel`'s single unfiltered category picker used for
  both Income and Expense transaction types; this codebase's `Category` entity has no
  Income/Expense `Kind` split) and `Accounts` (active `FinancialAccount`s only — **no** credit
  card entries this time, since `RecurringIncome` has no card-backed variant; also excludes
  `TermDeposit`/`InvestmentFund` the same way `AddRecurringExpenseViewModel.Accounts` does, for
  the same reason: a term deposit can't be credited directly outside its own maturity/contribution
  flow, and an investment fund must go through `RecordInvestmentContributionAsync`, not a raw
  credit). `SaveAsync` validates name/amount/category/account/end-date the same way, then
  constructs `new RecurringIncome(...)` and saves.
- `src/TrackTraceMoney.App/Views/RecurringIncomesListPage.xaml`(`.cs`) — same layout as
  `RecurringExpensesListPage.xaml`: error label, "due now" section with Confirm buttons, full
  `CollectionView` with Deactivate buttons, Add button pinned at the bottom. Row template adds a
  third line under the existing Category/Account/Amount line showing the two equivalents, e.g.
  `"≈ {BiweeklyEquivalent:N2}/biweekly · ≈ {MonthlyEquivalent:N2}/monthly"` (localized label
  strings, not hardcoded — see resx note below).
- `src/TrackTraceMoney.App/Views/AddRecurringIncomePage.xaml`(`.cs`) — same layout as
  `AddRecurringExpensePage.xaml`, plus a live-updating read-only label under the Amount entry
  showing the two equivalents as the user types/changes frequency (recomputed in
  `OnAmountTextChanged`/`OnSelectedFrequencyChanged` partial property-changed hooks — same
  `partial void On...Changed` idiom `IncomeVsExpensesReportViewModel.OnSelectedMonthChanged`
  already uses elsewhere in this codebase).
- `src/TrackTraceMoney.App/Converters/RecurringIncomeFrequencyToLabelConverter.cs` — same shape
  as `RecurringExpenseFrequencyToLabelConverter`, extending `EnumToLabelConverter<RecurringIncomeFrequency>`.

### Amount-edit ("raise") UI — Decision F

A single small dedicated page, not a reuse of `AddRecurringIncomePage`. Reusing the Add page
would require turning `AddRecurringIncomeViewModel.SaveAsync` into an insert-or-update flow
keyed on an optional Id (touching every field's validation/load path for a change that only ever
needs to touch one field) — more invasive than the ask. Instead:

- `src/TrackTraceMoney.App/Views/EditRecurringIncomeAmountPage.xaml`(`.cs`) — minimal page: current
  `Name` shown read-only for context, a single `Amount` `Entry` pre-filled with the current value,
  Save/Cancel buttons. Same MVVM shape as every other page in this codebase (Entry two-way-bound
  to an `AmountText` string, parsed with `decimal.TryParse(..., NumberStyles.Number,
  CultureInfo.CurrentCulture, ...)` exactly like `AddRecurringExpenseViewModel.SaveAsync` already
  does, `[RelayCommand] SaveAsync` calling `RecurringIncome.UpdateAmount(...)` then
  `SaveChangesAsync()` then `GoToAsync("..")`).
- `src/TrackTraceMoney.App/ViewModels/EditRecurringIncomeAmountViewModel.cs` — `[QueryProperty]`
  bound to a **string** id (not a raw `Guid`) — this codebase has a documented, previously-fixed
  P0 bug class here (see the "Add People/FinancialInstitution/CardNetwork" commit history:
  `[QueryProperty]` bound directly to a non-nullable `Guid` crashes on every navigation because
  `Guid` has no `IConvertible` and Shell's query-string binding uses `Convert.ChangeType`
  internally) — parse defensively in `LoadAsync`, same idiom already used everywhere else for
  optional Guid query properties in this app.
- Row template on `RecurringIncomesListPage` gains a second small button next to Deactivate —
  "Edit amount" — visible only while `IsActive`, navigating to
  `EditRecurringIncomeAmountPage?id={Id}`.
- New route registration in `AppShell.xaml.cs`:
  `Routing.RegisterRoute(nameof(EditRecurringIncomeAmountPage), typeof(EditRecurringIncomeAmountPage));`
  alongside the existing `RecurringExpensesListPage`/`AddRecurringExpensePage` registrations
  (`src/TrackTraceMoney.App/AppShell.xaml.cs:26-27`), plus
  `Routing.RegisterRoute(nameof(RecurringIncomesListPage), typeof(RecurringIncomesListPage));` and
  `Routing.RegisterRoute(nameof(AddRecurringIncomePage), typeof(AddRecurringIncomePage));`.

### Nav entry point — Decision G

Reached from Settings, same section as "Manage Recurring Expenses"
(`Settings_ExpenseManagementSectionTitle`, `src/TrackTraceMoney.App/Views/SettingsPage.xaml:52-67`),
as a new button immediately below the existing "Manage Recurring Expenses" button:

```xml
<Button Text="{x:Static resources:AppResources.Settings_ManageRecurringIncomeButton}"
        Command="{Binding GoToManageRecurringIncomeCommand}"
        AutomationId="Settings_ManageRecurringIncomeButton"
        HorizontalOptions="Fill" />
```

Deliberately **not** a new section/renamed section header: that section already mixes
Categories/Budgets/RecurringExpenses (not purely "expense management" in the strictest sense even
today), and renaming it purely for this addition would be unrelated resx churn/scope creep beyond
what this slice needs. `SettingsViewModel` gains `GoToManageRecurringIncomeCommand`, mirroring
`GoToManageRecurringExpensesCommand`'s single-line `Shell.Current.GoToAsync(nameof(RecurringIncomesListPage))`
body exactly.

### Localization

New `.resx` keys, both `AppResources.resx` (ES) and `AppResources.en.resx` (EN), per this
project's `add-localized-text` skill — no hardcoded strings. Needed keys (illustrative, not
exhaustive — implementer fills in the parallel set the Add/List/Edit pages actually reference,
mirroring every `RecurringExpenses_*`/`AddRecurringExpense_*` key 1:1):
`RecurringIncomes_Title`, `RecurringIncomes_DueSectionTitle`, `RecurringIncomes_ConfirmButton`,
`RecurringIncomes_DeactivateButton`, `RecurringIncomes_EditAmountButton`,
`RecurringIncomes_NextDueLabel`, `RecurringIncomes_BiweeklyEquivalentLabel`,
`RecurringIncomes_MonthlyEquivalentLabel`, `RecurringIncomes_Empty`,
`RecurringIncomes_AddButton`, `RecurringIncomes_ConfirmError`, `RecurringIncomes_DeactivateError`,
`AddRecurringIncome_*` (validation messages, mirroring `AddRecurringExpense_Validation*` 1:1),
`EditRecurringIncomeAmount_Title`, `EditRecurringIncomeAmount_SaveButton`,
`EditRecurringIncomeAmount_ValidationAmountInvalid`,
`RecurringIncomeFrequency_Weekly/Biweekly/Monthly/Yearly`,
`Settings_ManageRecurringIncomeButton`.

---

## 5. Infrastructure layer

- `src/TrackTraceMoney.Infrastructure/Persistence/Configurations/RecurringIncomeConfiguration.cs`
  — mirrors `RecurringExpenseConfiguration.cs`'s shape (key, required/max-length on `Name`,
  `decimal` precision on `Amount`, FK relationships to `Category`/`FinancialAccount`). Simpler
  than `RecurringExpenseConfiguration` since there's no `AccountId`/`CreditAccountId` XOR to
  configure — just a single required `DestinationAccountId` FK.
- `src/TrackTraceMoney.Infrastructure/Repositories/RecurringIncomeRepository.cs` — mirrors
  `RecurringExpenseRepository.cs`: implements `IRecurringIncomeRepository`, `GetActiveAsync`
  filters `IsActive`.
- `DbSet<RecurringIncome> RecurringIncomes` added to `TrackTraceMoneyDbContext`.
- New EF Core migration (`AddRecurringIncome`) via the `ef-core-migration` skill's
  MAUI-app-can't-be-startup-project workaround — adds one new table, no changes to any existing
  table (no TPH collision risk here, unlike the `CreditCard`/`Loan` shared-table case in
  `MEMORY.md`, since `RecurringIncome` isn't part of any TPH hierarchy).

## 6. DI wiring (`MauiProgram.cs`)

Mirrors the existing `RecurringExpense` block
(`src/TrackTraceMoney.App/MauiProgram.cs:165-169`) exactly:

```csharp
builder.Services.AddScoped<IRecurringIncomeRepository, RecurringIncomeRepository>();
builder.Services.AddScoped<IRecurringIncomeService, RecurringIncomeService>();
builder.Services.AddTransient<RecurringIncomesListViewModel>();
builder.Services.AddTransient<RecurringIncomesListPage>();
builder.Services.AddTransient<AddRecurringIncomeViewModel>();
builder.Services.AddTransient<AddRecurringIncomePage>();
builder.Services.AddTransient<EditRecurringIncomeAmountViewModel>();
builder.Services.AddTransient<EditRecurringIncomeAmountPage>();
```

(`IRecurringExpenseRepository`/`RecurringExpenseRepository` registration wasn't shown in the
grepped snippet above but exists alongside it per the same pattern used by every other
repository/service pair in that file — implementer should register `IRecurringIncomeRepository`
the same way.)

---

## 7. Tests (per this project's existing coverage pattern for `RecurringExpense`)

- `tests/TrackTraceMoney.Domain.Tests/RecurringIncomes/RecurringIncomeTests.cs` — mirrors
  `RecurringExpenseTests.cs`: construction validation, `NextOccurrenceDate`/`IsDue`/`Advance` per
  frequency (including the new `Biweekly` case), `MarkConfirmed` occurrence-mismatch guard,
  `CountOccurrencesThrough`, `Deactivate`/`Reactivate`, plus new cases for `UpdateAmount`
  (rejects <= 0, applies immediately, doesn't touch `LastConfirmedDate`/`NextOccurrenceDate`).
- `tests/TrackTraceMoney.Application.Tests/RecurringIncomes/RecurringIncomeServiceTests.cs` —
  mirrors the equivalent `RecurringExpenseService` coverage: confirms posts a real `Income` via a
  mocked `ITransactionEntryService.RecordIncomeAsync` with the exact expected arguments
  (`personId: null` in particular — asserts Decision C), marks confirmed, and that a failure
  mid-transaction rolls back both sides (no orphaned `Income` with a still-due recurring
  definition, or vice versa).
- `tests/TrackTraceMoney.Application.Tests/RecurringIncomes/RecurringIncomeEquivalentCalculatorTests.cs`
  — table-driven: every `RecurringIncomeFrequency` × a known amount produces the expected
  biweekly/monthly figure (e.g. Monthly $2000 → Biweekly ≈ $923.08; Biweekly $160 → Monthly ≈
  $346.67), confirming the conversion factors above.
- `tests/TrackTraceMoney.Infrastructure.Tests` — a `RecurringIncomeRepository` round-trip test
  mirroring the existing `RecurringExpenseRepository` coverage (add, `GetActiveAsync` filters
  inactive, `GetByIdAsync`).

---

## Summary of every decision made (nothing left open for the implementer)

| # | Question | Decision |
|---|----------|----------|
| A | How does the amount become editable (raise)? | `RecurringIncome.UpdateAmount(decimal)`, mirroring `Budget.UpdateAmount`; prospective-only, never rewrites posted `Income` transactions or `LastConfirmedDate`. |
| B | Credit-card duality like `RecurringExpense`? | No — `Income` has no credit-card destination concept in this domain; single non-nullable `DestinationAccountId`. |
| C | Capture a `PersonId` like `Income` supports? | No — mirrors `RecurringExpense`'s own precedent of deliberately not capturing Person fields on the recurring definition. |
| D | Reuse/extend `RecurringExpenseFrequency` for `Biweekly`? | No — new, independently-scoped `RecurringIncomeFrequency` enum; avoids coupling an unrelated, already-shipped/tested enum to a second entity's needs. |
| E | How to satisfy "see biweekly and monthly breakdowns"? | Pure derived-projection calculator (`RecurringIncomeEquivalentCalculator`) shown on the List/Add screens — not a new report, not a change to `IncomeVsExpensesReportViewModel` (which stays a strictly historical/actual-transactions report). |
| F | How does amount-editing surface in the UI? | New minimal dedicated `EditRecurringIncomeAmountPage`, not a reuse of the Add page's insert flow. |
| G | Where does this live in nav? | Settings, same section as "Manage Recurring Expenses", one new button below it; section not renamed. |
| — | Link to a specific `RecurringExpense` (e.g. the $156 rental payment)? | Explicitly out of scope — the two stay fully independent, per the user's own "just extra" framing. |
| — | "Property" entity? | Out of scope. |
| — | Automatic net-margin calc between an expense and an income? | Out of scope. |
