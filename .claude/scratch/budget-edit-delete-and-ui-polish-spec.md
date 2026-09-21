# Budget Edit/Delete + Budget-list clarity + CreditCardDetail button compaction

Three small, independent, low-risk App-layer changes closing real gaps found during live
user testing on 2026-09-19/20. None of these touch money-calculation logic (Budget has no
dependent entities and drives no accounting side effects, unlike Accounts/CreditCards/Loans/
Transactions from the previous edit-delete slice), so this does NOT need product-owner
scoping or a lifecycle service — it's UI wiring on top of capabilities that already exist.

## 1. Budget Edit/Delete (closes: "las opciones de presupuestos no deja editar o eliminar")

**What already exists and must be reused, not reinvented:**
- `Budget.UpdateAmount(decimal)` already exists (`src/TrackTraceMoney.Domain/Budgets/Budget.cs`).
- `IRepository<Budget>.Remove(TEntity)` + `GetByIdAsync(Guid)` + `SaveChangesAsync()` already
  exist on the base `IRepository<TEntity>` (`src/TrackTraceMoney.Application/Abstractions/IRepository.cs`)
  and `BudgetRepository` already inherits them from `RepositoryBase<Budget>` — **zero new
  repository/Infrastructure code needed** for either edit or delete.
- `AddBudgetViewModel.SaveAsync` (`src/TrackTraceMoney.App/ViewModels/AddBudgetViewModel.cs`)
  ALREADY upserts: it calls `GetForCategoryAndMonthAsync(category, year, month, currency)` and,
  if a budget already exists for that exact category+month+currency, calls `UpdateAmount` on it
  instead of creating a new one. This is already a working edit path, just with zero UI
  discoverability and no way to change the amount of a PAST month's budget or delete one.

**Changes:**

### `AddBudgetViewModel.cs` — add edit mode
Mirror `AddAccountViewModel`'s exact edit-mode idiom (same file for reference:
`src/TrackTraceMoney.App/ViewModels/AddAccountViewModel.cs` lines ~30-117, ~209):
- `[QueryProperty(nameof(BudgetIdText), "budgetId")]` on the class, backed by a `string?
  BudgetIdText` property whose setter parses into `Guid? EditingBudgetId` via
  `Guid.TryParse` (defensive — never bind `[QueryProperty]` directly to a non-nullable `Guid`,
  that's a known crash-on-every-navigation bug class in this codebase).
- `public bool IsEditMode => EditingBudgetId is not null;`
- `public bool IsCategoryPickerEnabled => !IsEditMode;` and a matching
  `IsCurrencyPickerEnabled => !IsEditMode` — **lock both Category and Currency in edit mode**.
  This is deliberate, not a placeholder: the existing upsert logic keys off
  category+month+currency, so locking both means `SaveAsync`'s existing
  `GetForCategoryAndMonthAsync` + `UpdateAmount` path is reused completely unmodified — no new
  branching needed in `SaveAsync` at all, edit mode only changes what gets pre-loaded and what's
  editable.
- New `LoadForEditAsync()` (called the same way `AddAccountViewModel` calls its own load-for-edit
  — check that file's pattern, likely invoked from the page's `OnAppearing` when `IsEditMode` is
  true): `GetByIdAsync(EditingBudgetId.Value)`, then set `SelectedCategory` (look up by
  `budget.CategoryId` against the already-loaded `Categories` collection — load categories
  first), `AmountText = budget.Amount.ToString(...)`, `SelectedCurrency = budget.Currency`.
- `PageTitle` computed property (`IsEditMode ? AppResources.AddBudget_EditTitle :
  AppResources.AddBudget_Title`) — add the new resx key in both languages.
- New `DeleteAsync` `[RelayCommand]`, visible only in edit mode: `GetByIdAsync`, `Remove`,
  `SaveChangesAsync`, then `GoToAsync("..")`. No confirmation dialog needed if the existing
  Account/CreditCard/Loan delete commands in this codebase don't use one either — check
  `AccountsListViewModel.DeleteAccountAsync` for the established precedent and match it
  (if that one shows a confirm dialog via `Shell.Current.DisplayAlert`, mirror that here too).

### `AddBudgetPage.xaml` — wire up edit mode
- Bind `Title="{Binding PageTitle}"` instead of the static resx.
- Bind `Picker` for Category and Currency to `IsEnabled="{Binding IsCategoryPickerEnabled}"` /
  `IsEnabled="{Binding IsCurrencyPickerEnabled}"` respectively.
- Add a `Button` for Delete in the bottom `VerticalStackLayout`, `IsVisible="{Binding
  IsEditMode}"`, `BackgroundColor` matching the DarkRed used for delete elsewhere in this
  codebase (see `AccountsListPage.xaml`'s delete `SwipeItem`), `Command="{Binding
  DeleteCommand}"`.

### `AddBudgetPage.xaml.cs` — trigger the edit-mode load
Check how `AddAccountPage.xaml.cs`'s `OnAppearing` triggers its own load-for-edit call and
mirror it exactly (same file, already read this session — it calls something on `OnAppearing`
after `base.OnAppearing()`).

### `BudgetsListPage.xaml` — Edit/Delete entry points
Replace the current plain `Grid` per-row template with a `SwipeView` mirroring
`AccountsListPage.xaml`'s exact pattern (same file, already read this session) but with only
two `SwipeItem`s (Edit, Delete — Budget has no Deactivate/Reactivate concept, there's no
`IsActive` on `Budget`):
- Edit: `Command="{Binding Source={x:Reference Root}, Path=BindingContext.EditBudgetCommand}"
  CommandParameter="{Binding Id}"`.
- Delete: same pattern as Accounts' delete `SwipeItem` (DarkRed background), routes to
  `DeleteBudgetCommand`.
- Add `x:Name="Root"` to the `ContentPage` root element (currently missing — needed for the
  `{x:Reference Root}` bindings, same as `AccountsListPage.xaml` already has).

### `BudgetsListViewModel.cs` — new commands
- `EditBudgetCommand(Guid budgetId)`: `await Shell.Current.GoToAsync($"{nameof(AddBudgetPage)}?budgetId={budgetId}");`
- `DeleteBudgetCommand(Guid budgetId)`: fetch via `_budgetRepository.GetByIdAsync`, `Remove`,
  `SaveChangesAsync`, then `await LoadBudgetsAsync()` to refresh the list. Match whatever
  confirm-dialog convention `AccountsListViewModel.DeleteAccountAsync` uses (see above).

## 2. Budget list clarity (closes: "no se entiende mucho porque agregué vivienda y me sale 0.00/156.50")

`BudgetListItem` (`src/TrackTraceMoney.App/Models/BudgetListItem.cs`) already computes
`Remaining` — it's just never displayed. The current `BudgetsListPage.xaml` row template shows
only a bare `{Spent} / {BudgetAmount} {Currency}` with no labels, which is genuinely opaque
(the user correctly could not tell which number was which, or what "0.00" even meant — no
expenses recorded yet this month, not an error).

Change the row template (`src/TrackTraceMoney.App/Views/BudgetsListPage.xaml`, inside
`CollectionView.ItemTemplate`) to show labeled text, e.g. a second line under the category
name: `"{Budgets_SpentLabel}: {Spent:N2} {Currency}   {Budgets_OfLabel} {BudgetAmount:N2}"` and
a third line `"{Budgets_RemainingLabel}: {Remaining:N2} {Currency}"` (color it red/orange when
`Remaining < 0` or the semáforo is 🔴/🟡 — reuse the existing `SemaforoEmoji` prefix already
shown, don't remove it). Add new resx keys in both `AppResources.resx`/`.en.resx`:
`Budgets_SpentLabel` ("Gastado"/"Spent"), `Budgets_OfLabel` ("de"/"of"),
`Budgets_RemainingLabel` ("Restante"/"Remaining").

## 3. CreditCardDetailPage button compaction (closes: "en credito me muestran los 3 botones... ocupan mucho espacio")

`src/TrackTraceMoney.App/Views/CreditCardDetailPage.xaml` currently stacks 4 full-width
`Button`s vertically right after the title (Edit, Deactivate, Reactivate, Delete — only 3
visible at once since Deactivate/Reactivate are mutually exclusive via `IsVisible`, but they
still claim 3 full-width rows of vertical space before any real card content is visible).

Replace the 4 stacked full-width `Button`s with a single `HorizontalStackLayout` (or `Grid`
with `ColumnDefinitions="*,*,*"`) so the 3 visible actions sit side-by-side in one row instead
of three. Keep every existing `Command`, `IsVisible`, and `AutomationId` binding completely
unchanged (no E2E test currently references these AutomationIds — confirmed via grep — but
keep them anyway, cheap and future-proof). Shrink button text if needed (shorter resx labels
are NOT required — MAUI `Button` wraps/shrinks reasonably at this width) or reduce `Padding`/
`FontSize` slightly so three fit comfortably on a standard phone width without wrapping badly.
Visually this should look like a compact action bar, not three prominent CTAs — this page's
main content (card status, cycle, purchased-vs-paid) is what the user actually came here for.

## Build order / verification

1. Domain: none needed (Budget.UpdateAmount already exists).
2. Application/Infrastructure: none needed (Remove/GetByIdAsync already exist on the base
   repository).
3. App layer: AddBudgetViewModel + AddBudgetPage.xaml + AddBudgetPage.xaml.cs (edit mode +
   delete), BudgetsListPage.xaml + BudgetsListViewModel.cs (SwipeView + Edit/Delete commands),
   resx additions (both languages — this repo enforces ES/EN key parity, see
   `add-localized-text` skill), CreditCardDetailPage.xaml (button layout only, no ViewModel
   changes needed).
4. Full solution build must stay 0 warnings/0 errors. Run Domain/Application/Infrastructure
   test suites (should be unaffected — no Domain/Application/Infrastructure code changed) to
   confirm no regression: 243/201/46 passing is the known-good baseline as of 2026-09-19.
5. Do NOT attempt live device verification yourself — the coordinating session will handle
   that after this lands (no emulator/device is assumed available to you).
