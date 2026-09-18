# Export (README §41) — Slice 1: CSV, Transactions Only

Scoped by `product-owner`, 2026-09-17. Read against §41's exact wording ("Planned formats: CSV,
Excel, PDF. Export can be filtered by: Period, Account, Category, Transaction type, Report") plus
the already-shipped Local Backup feature (§42, `ILocalBackupService`/`LocalBackupService`) and
History's existing filter UI (`HistoryViewModel`/`HistoryPage.xaml`).

## Explicit boundary — read this before implementing anything else

**This slice is CSV only.** Excel and PDF are explicitly deferred to later slices, not built or
stubbed here — both need a new binary-file-generation dependency this project does not currently
reference (`ClosedXML`/`EPPlus`-class package for Excel, `QuestPDF`/similar for PDF), unlike CSV
which is plain text and needs nothing new. Do not add either package as part of this slice. Do not
build a format-picker UI that implies Excel/PDF exist yet — a single "Export CSV" action, full stop.

"Filtered by ... Report" (the fifth item in §41's filter list) is also out of scope — this codebase
has no Reports screen yet (Phase 3's net-worth slice explicitly deferred one: "no general Reports
screen (separate later Phase 3 item)", per `trackmoney_roadmap_progress.md`), so there is nothing to
filter *by* yet. Revisit when a Reports feature is scoped.

## Decision 1 — What gets exported: transactions only

Scope tightly to transactions, matching History's own scope exactly. §41's filter list (Period,
Account, Category, Transaction type) is entirely transaction-level vocabulary — none of it describes
filtering a budgets/categories/accounts export. Exporting the accounts list, category list, or budget
list as their own CSVs is a plausible *future* possibility (e.g. "export my account list") but §41
never asks for it and nothing in this slice builds toward it. Noted here as an explicitly deferred,
not-yet-scoped idea — not a TODO, not partially built.

## Decision 2 — Filter UI: reuse History's existing filter state, no new screen

Add an "Export CSV" action to the existing History screen (`HistoryPage.xaml` /
`HistoryViewModel.cs`) that exports whatever the user currently has filtered/visible — **not** a new
dedicated Export screen with its own duplicate filter controls.

Justification, against this codebase's own repeatedly-established pattern this session (People,
Institutions, Card Networks, Term Deposits, Investment Funds — every one of those reused an existing
list-screen shape rather than inventing a parallel one): `HistoryViewModel` already implements four of
§41's five filter axes as real, live, two-way-bound state — `SelectedAccountFilter` (Account),
`SelectedCategoryFilter` (Category), `SelectedTypeFilter` (Transaction type), `FromDate`/`ToDate`
(Period) — plus two extra filters (`SelectedPersonFilter`, min/max amount) History already has that
§41 doesn't even ask for. Building a second Export screen would either (a) duplicate all of this filter
UI and risk drifting out of sync the next time a filter is added to History (nothing would force a
second edit), or (b) under-deliver by omitting filters History already has. One button on History that
exports the current `GroupedEntries` result is strictly less code, cannot drift, and reads naturally as
"export what I'm looking at."

**Implementation note for `dev`** (flagging, not fully prescribing): `HistoryViewModel.ApplyFilters()`
currently keeps only the *grouped* result (`GroupedEntries`); the flat filtered `IEnumerable<HistoryEntryItem>`
that groups are built from is a local variable, discarded. This slice needs that flat filtered list
retained (e.g. a new `_filteredEntries` field set alongside `GroupedEntries`) so Export operates on
exactly what's currently on screen, not a fresh unfiltered pass over `_allEntries`.

## Decision 3 — CSV shape

**Columns, in this order**: `Date, Type, Amount, Currency, Account, Category, Person, Description`.

One row per transaction, same uniform shape across every `Transaction` subtype (Expense, Income,
Transfer, CreditCardPurchase, CreditCardPayment, LoanPayment, InvestmentContribution,
InvestmentWithdrawal, InterestIncome, Reimbursement) — inapplicable columns are blank, never omitted
or restructured per type. Field sourcing, all pulled from what `HistoryEntryItem`
(`src/TrackTraceMoney.App/Models/HistoryEntryItem.cs`) already exposes per row — no new query against
the domain model is needed for Date/Type/Amount/Description/Account/Category:

- **Date** — `HistoryEntryItem.Date`, formatted `yyyy-MM-dd` (ISO), not `CultureInfo.CurrentCulture`.
- **Type** — the existing localized label, `TransactionTypeToLabelConverter.GetDisplayName(entry.Type)`.
- **Amount** — `HistoryEntryItem.Amount`, formatted invariant-culture `0.00` (period decimal
  separator), not `CultureInfo.CurrentCulture`.
- **Currency** — resolved via the same `accountNames`-sibling currency map this screen doesn't
  currently build but easily can (`AccountCurrencyMapBuilder.Build`, already used elsewhere in
  Application/Reporting) keyed by `HistoryEntryItem.AccountIds[0]` — the transaction's own currency is
  never stored directly (per `FinancialAccount.Currency`/`CreditAccount.Currency`, confirmed by
  reading `FinancialAccount.cs` — no `Currency` field exists on `Transaction` itself), so it is always
  derived from the primary account involved, same as every other currency-aware screen in this app.
  Rendered as the bare `CurrencyCode` enum name (e.g. `USD`, `MXN`) — already locale-neutral.
- **Account** — reuse `HistoryEntryItem.AccountLabel` verbatim (built by the existing
  `TransactionLabelFormatter.BuildLabel`, e.g. `"Cash → Groceries"`, `"Checking → 💳 Visa"`). Reusing
  the exact on-screen label (including its emoji glyphs, which are valid UTF-8 text, not a CSV
  problem) keeps the exported row recognizably identical to what the user filtered and saw, rather
  than inventing a second, subtly different account-label format.
- **Category** — resolved category name (via the same `categoryNames` dictionary
  `HistoryViewModel.LoadHistoryAsync` already builds), blank when `HistoryEntryItem.CategoryId` is
  `null` (Transfer, CreditCardPayment, LoanPayment, InvestmentContribution/Withdrawal, InterestIncome,
  Reimbursement all have a null `CategoryId` today — confirmed in `HistoryEntryItem.FromDomain`).
- **Person** — **new**: `HistoryEntryItem.PersonIds` already exists (Expense/CreditCardPurchase carry
  payer+beneficiary, Income carries one) but nothing resolves it to a name today — History's UI only
  uses person as a filter, never displays it per row. This slice adds a `personNames` dictionary
  (`people.ToDictionary(p => p.Id, p => p.Name)` — `people` is already loaded in
  `LoadHistoryAsync` for the filter dropdown) and joins multiple resolved names with `"; "`. Blank
  when `PersonIds` is empty.
- **Description** — raw `HistoryEntryItem.Description`, blank when `null`. RFC 4180 quote-escaped by
  the CSV writer (see Decision 4) since free text can legitimately contain commas/quotes/newlines.

**Header row localization — decided yes, with a documented tradeoff.** CLAUDE.md's "no hardcoded UI
strings" rule is written for screens, but a CSV header row is still text the user directly reads
(opened in Excel/Sheets) — and this app already treats other non-XAML surfaces as needing resx-backed
strings (`Share.Default.RequestAsync`'s `Title = AppResources.Settings_ExportButton` in
`SettingsViewModel.cs:339`). Decision: yes, extend the rule here — new resx keys
(`Export_Header_Date`, `Export_Header_Type`, `Export_Header_Amount`, `Export_Header_Currency`,
`Export_Header_Account`, `Export_Header_Category`, `Export_Header_Person`,
`Export_Header_Description`) in both `AppResources.resx`/`AppResources.en.resx`. **Tradeoff accepted,
not hidden**: this means the header row's exact text differs between an ES-language export and an
EN-language export of the same data — acceptable for a personal export meant for the user's own
eyes/Excel, not a fixed data-interchange schema. If a future slice ever adds CSV *re-import*, fixed
(English or key-based) headers would need to be reconsidered then — flagged, not solved now.

Data cells (Date/Amount/Currency) are deliberately **culture-invariant regardless of device
language** — this is a different axis from header localization. Rationale: a CSV is meant to be
re-opened/re-parsed by external tools (Excel, the user's own scripts), and a locale-formatted decimal
(e.g. `1.234,56` under an ES culture) colliding with the comma field separator is a well-known CSV
corruption trap. Only the header text is localized; the data grid stays machine-stable.

## Decision 4 — Application-layer shape: split at the RFC 4180 boundary, not the domain-knowledge boundary

**Application layer** (`TrackTraceMoney.Application.Export`, new folder — sibling to `Reporting`):
a small, pure, static `CsvSerializer.Write(IReadOnlyList<string> headers, IEnumerable<string[]> rows) -> string`.
RFC 4180 field/record mechanics only (quote-escape fields containing a comma/quote/newline, double up
embedded quotes, CRLF line endings) — **zero domain knowledge**, trivially unit-testable with plain
xUnit (`tests/TrackTraceMoney.Application.Tests/Export/CsvSerializerTests.cs`): feed literal strings,
assert exact CSV text, including the escaping edge cases (comma-in-Description, quote-in-Description,
embedded newline). This is genuinely the only part of this slice with logic worth a dedicated test
suite, and it needs no repository/DbContext, matching this codebase's existing precedent for pure
static Application-layer helpers (`AccountCurrencyMapBuilder`) — no injected `I*Service`, no DI
registration needed.

**Deliberately NOT in the Application layer**: per-transaction-type row assembly (deciding which
account/category/person applies to which `Transaction` subtype). `HistoryEntryItem.FromDomain`
already contains exactly that exhaustive switch, and `TransactionListItem.FromDomain` already
contains a near-identical second copy of it — this codebase has already accepted that specific
duplication rather than forcing a shared abstraction (see `trackmoney_roadmap_progress.md`'s Phase-4
checkpoint note: "service/enum duplication ... left as-is per the review's own scope guidance"), and
`HistoryEntryItem` is an **App-layer** model (`TrackTraceMoney.App.Models`) that the Application layer
must not reference (reference direction is one-way: App → Application, never the reverse — see
CLAUDE.md's layering section). Re-deriving the same switch a third time, in Application, against raw
`Transaction` objects, would be a real duplication with no payoff, since `HistoryEntryItem` already
carries every field the CSV needs. Row assembly stays in the **App layer**.

**App layer** (`TrackTraceMoney.App.Models`, alongside `HistoryEntryItem`/`TransactionLabelFormatter`):
a new `TransactionCsvRowBuilder` turns History's already-filtered `IReadOnlyList<HistoryEntryItem>`
(Decision 2) plus the new `personNames`/currency-map dictionaries into ordered `string[]` rows per the
Decision-3 column rules, prepends the localized `Export_Header_*` header row, and calls
`CsvSerializer.Write` for the final text.

**Platform hand-off — reuse §42's exact mechanism, not a new one.** Read
`SettingsViewModel.ExportAsync` (`src/TrackTraceMoney.App/ViewModels/SettingsViewModel.cs:316-353`):
it writes the export to `Path.Combine(FileSystem.CacheDirectory, fileName)` and hands it to the OS via
`Share.Default.RequestAsync(new ShareFileRequest { Title = ..., File = new ShareFile(path) })` — MAUI
Essentials' built-in share sheet, explicitly chosen there over `CommunityToolkit.Maui.Storage`'s
`IFileSaver` because that package isn't referenced by this project. `HistoryViewModel`'s new
`ExportCsvCommand` does the identical thing: write CSV text to
`Path.Combine(FileSystem.CacheDirectory, $"tracktracemoney-transactions-{DateTime.Now:yyyyMMdd-HHmmss}.csv")`,
then `Share.Default.RequestAsync`. No new package, no new "hand the user a file" mechanism introduced.

## Decision 5 — UI entry point

A new "Export CSV" button on `HistoryPage.xaml`, near the existing filter controls, bound to a new
`HistoryViewModel.ExportCsvCommand`. New resx keys needed: `History_ExportCsvButton`,
`History_ExportCsvSuccess`, `History_ExportCsvError`, `History_ExportCsvEmpty` (shown instead of
running an export when `IsEmpty` is true — no point sharing a header-only file) — mirrors the
existing `Settings_ExportButton`/`Settings_ExportSuccess`/`Settings_ExportError` naming pattern
without reusing those exact keys (that label specifically means "back up my whole database," reusing
it here would mislabel a transactions-only CSV). All of History's existing filter-label resx keys
(`History_AccountFilterLabel`, `History_CategoryFilterLabel`, etc.) are reused unchanged — Decision 2
adds no new filter controls.

## Out of scope for this slice (explicit, not silent)

- Excel, PDF (Decision boundary above).
- Export of budgets/categories/accounts as their own CSVs (Decision 1).
- Filtering "by Report" (no Reports screen exists).
- Any format picker / multi-format UI.
- CSV re-import.

## Acceptance criteria

1. Given the user is on History with a set of filters applied (Decision 2's reused filter state),
   when they tap "Export CSV," then a `.csv` file containing exactly the rows currently shown in
   `GroupedEntries` (no more, no fewer) is generated and handed to the Android share sheet via
   `Share.Default.RequestAsync`, matching §42's already-shipped share mechanism.
2. The CSV's header row is `Date, Type, Amount, Currency, Account, Category, Person, Description` in
   that order, localized to the device's current language via new `Export_Header_*` resx keys.
3. Every transaction subtype (Expense, Income, Transfer, CreditCardPurchase, CreditCardPayment,
   LoanPayment, InvestmentContribution, InvestmentWithdrawal, InterestIncome, Reimbursement) produces
   exactly one row, with inapplicable columns blank (never a missing column, never a restructured row).
4. Date is `yyyy-MM-dd`; Amount is invariant-culture `0.00`; both are culture-invariant regardless of
   device language, independent of the header row's localization.
5. Description values containing a comma, a double quote, or an embedded newline round-trip correctly
   under RFC 4180 quoting (verified by `CsvSerializerTests`, not just by inspection).
6. Tapping "Export CSV" when History's current filter yields zero results shows
   `History_ExportCsvEmpty` and does not invoke the share sheet.
7. `CsvSerializer.Write` is covered by `TrackTraceMoney.Application.Tests` with no App/MAUI project
   reference required to run those tests.

## Phase

Phase 1 (MVP) — §41 sits alongside §42 (Local backup, already shipped) in the MVP feature list per
README §51; this is a same-phase feature completion, not a phase pull-forward.
