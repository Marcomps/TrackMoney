# Slice spec: user-managed Financial Institution + Card Network lists

Scoped by `product-owner`, 2026-09-16 (separate task from, and independent of, the already-shipped
Recurring Expense credit-card-payment-source slice — no files from that slice are touched here).
Confirmed with the user directly (via the coordinator's restatement, approved): unlimited,
user-growable lists, same pattern as this app's existing Category/People lists, applies across
CreditCard/Loan/TermDeposit/InvestmentFund for institution, CreditCard-only for network/brand.

**Two independently shippable slices, not one** (per this role's backlog-breakdown responsibility —
this ask is too big for one slice): **Slice A (FinancialInstitution)** is the primary ask (4 existing
free-text fields, real repeated-retyping pain, explicitly justified at length by the user).
**Slice B (CardNetwork)** is a small, clean, additive, CreditCard-only extra, structurally disjoint from
Slice A except for sharing `AddCreditCardPage`/`SettingsPage`. Ship A first (higher value, what the user
actually led with) then B — but B has zero hard dependency on A and could ship first if convenient.

## Decision 1 — two distinct entities, not one generic "named lookup" concept

Checked this codebase for an existing generic/reusable named-list pattern before deciding, per the
task's own instruction: **none exists.** `Category` and `Person` are structurally near-identical
("just a named thing referenced elsewhere") and were still built as two fully distinct entities/tables/
repositories/screens, never unified behind a discriminator. This is the strongest, most direct
precedent available, and it points one way.

**Decision: two distinct entities, `FinancialInstitution` (`src/TrackTraceMoney.Domain/Institutions/FinancialInstitution.cs`)
and `CardNetwork` (`src/TrackTraceMoney.Domain/CardNetworks/CardNetwork.cs`)** — own top-level folder
each, mirroring `People/`/`Categories/`'s placement (not nested inside `CreditAccounts/`, which is
reserved for the actual `CreditAccount` TPH hierarchy + its tightly-coupled `CreditCardStatement`
child — neither new entity has that relationship; both are standalone named lists like Person/Category).

Why not a generic `NamedLookup { Kind }` entity: (1) no precedent for it anywhere in this codebase, and
a strong counter-precedent (Category/Person deliberately weren't unified despite being just as
structurally similar); (2) a shared `Guid NamedLookupId` FK loses the compile-time-by-convention
distinction a dedicated repository type gives you (`IFinancialInstitutionRepository` vs
`ICardNetworkRepository` — a dev can't as easily wire the wrong one into the wrong picker as they could
mix up a `Kind` enum value); (3) the two concepts can diverge later (an institution might plausibly gain
a country/SWIFT field some day; a card network almost certainly never would) without forcing unrelated
optional columns onto a shared table.

**Entity name: `FinancialInstitution`, not `Bank`.** The user's own words explicitly include
credit unions/cooperativas as valid entries ("hasta puede agregar alguna cooperativa") — "Bank" would
under-describe the concept even though README §14 labels the field "Bank / issuer" and 3 of the 4
existing free-text fields are literally named `Institution` already. The exposed FK property on every
consumer is `InstitutionId` (not `BankId`) for the same reason, and because `CreditCard.Issuer` is being
folded into the same shared concept (a card's issuer bank IS its institution — no reason to keep
"issuer" as conceptually distinct from "institution").

Both new entities are the simplest in the codebase — **`Name` only** (even simpler than `Person`, which
has required `RelationshipType`). Nothing in the request or README asks for more; not padding scope
with an unrequested country/icon/notes field.

Shape (mirrors `Person`/`Category` exactly): private parameterless ctor (EF), public ctor validates
non-empty + max 200 chars (matches every existing `Institution`/`Issuer` field's own limit — no reason
to pick a different one now), `Rename(string)` mutator. **No `IsActive`/`Deactivate`, no unique-name
constraint, no delete command, no search/filter on the list screen** — matches `Person`/`Category`
precisely (neither has any of these either; confirmed by reading `PeopleListViewModel.cs`/
`Person.cs`/`Category.cs` directly — no `IsActive` field, no delete command, `PeopleListViewModel`'s
only commands are `LoadPeopleAsync`/`AddPersonAsync`). Not gold-plating past what the two closest
precedents already do.

New empty marker repositories, mirroring `IPersonRepository`/`ICategoryRepository` exactly:
`IFinancialInstitutionRepository : IRepository<FinancialInstitution>`,
`ICardNetworkRepository : IRepository<CardNetwork>`. New `FinancialInstitutionConfiguration.cs`/
`CardNetworkConfiguration.cs`, each a straight copy of `PersonConfiguration.cs`'s shape
(`ToTable`, `HasKey`, `Property(x => x.Name).IsRequired().HasMaxLength(200)`).

**Seed data: zero, for both.** Considered seeding 1-2 near-universal card networks (Visa/Mastercard) the
way `PersonSeeder` seeds exactly one universal default ("Yo") — rejected: the user's own words extended
the "don't limit to what we know" framing to cards too ("...sobre el mismo banco o tarjetas"), and
seeding even 2 networks risks the same "who picked these and why" question the user is explicitly
trying to avoid for institutions. Starting both lists empty is the most literal, safest reading of what
was asked, and costs nothing (the first Add-flow populates either list in seconds). No new seeder class
needed for either entity.

## Decision 2 — migration path for existing data: nullable FK replaces the free-text field, backfilled by a new C# startup routine, old columns kept (not dropped) this slice

Ruled out immediately: **keeping the free-text field alongside the new FK forever** (one option the task
raised). The task's own instruction says "replace," and a permanent dual-source-of-truth (which one is
authoritative for display? for future export/reports?) is exactly the kind of ambiguity CLAUDE.md's
domain-rules section warns against creating. Not compatible with "replace."

**Decision: `Guid? InstitutionId` replaces the string field on all 4 entities** (mirrors this slice's own
requested precedent — `RecurringExpense.AccountId` going nullable — same shape: old field's C# type
loosens to accommodate rows that don't yet have the new value). Public constructors still take a
**non-nullable** `Guid institutionId` parameter (every newly-created record must have one, preserving
the field's existing required-ness) — the stored property is nullable only because EF materializes
existing rows through the private parameterless constructor (bypassing the public one entirely, same as
every entity in this codebase already works), so pre-backfill legacy rows can transiently be `null`
without that ever being reachable through the public API. `CreditCard.NetworkId` (Slice B) is
genuinely optional (nullable constructor param, no requiredness check) — see Decision 3 for why.

**Backfill mechanism — a new C# routine, not raw SQL inside the EF migration.** Considered raw
`migrationBuilder.Sql(...)` (grouping/inserting/rewiring in one migration `Up()`) — rejected: this would
be the first genuine *data* migration in this project's history (every prior migration has been pure
schema), it's meaningfully harder to get right and impossible to unit-test the way this codebase tests
everything else (real-SQLite `Infrastructure.Tests`, confirmed throughout this project's history), and a
subtly-wrong one-shot SQL script is not safely re-runnable if it's ever found buggy after the fact.
A plain C# routine using normal `DbContext`/LINQ calls is: unit-testable against real SQLite (matching
this codebase's established testing culture), and safely re-runnable/idempotent by construction (see
below) — much lower risk for a project's *first* data-transform step.

New `FinancialInstitutionBackfillService` (`src/TrackTraceMoney.Infrastructure/Seeding/` — same folder as
`CategorySeeder`/`PersonSeeder`, called from the same `IFinanceDatabaseInitializer` hook, immediately
after those two — this hook already runs on every app startup AND on every new-profile creation
(Local Profiles feature), so it's the correct, single, already-established place). Per entity type
(CreditCard/Loan/TermDeposit/InvestmentFund), for every row where **the new `InstitutionId` is still
null AND the old free-text column is not null**: group by the *exact trimmed string* (case-sensitive,
no fuzzy/normalized matching), create one `FinancialInstitution` per distinct value, set `InstitutionId`
on every matching row.

**Exact-match dedup, deliberately not fuzzy/case-insensitive merging** — rejected fuzzy matching (e.g.
folding "Banco Agrícola" and "banco agricola" into one row) because a wrong merge is not safely
reversible (the information "these were originally two different strings" is destroyed) and is a much
worse failure mode than the accepted trade-off: a pre-existing near-duplicate becomes two distinct
`FinancialInstitution` rows post-backfill, which the user can trivially rename/consolidate by hand
afterward via the new list screen (cheap, safe, user-correctable) — not silently conflating two
*possibly-actually-different* institutions that happen to normalize the same way.

**Idempotency**: the "new FK still null AND old field not null" guard is naturally safe to run on every
app startup forever — already-backfilled rows are skipped (their `InstitutionId` is set), and any
*brand-new* row created after this slice ships never has the old free-text field populated at all (new
Add-screens only ever write `InstitutionId`), so it's permanently skipped too. No table-level "already
ran" flag needed — same idiom `CategorySeeder`/`PersonSeeder` already use, adapted to a per-row guard
since this is a backfill, not a one-time table seed.

**Old free-text columns (`Issuer`, 3×`Institution`) are NOT dropped this slice** — made nullable in the
same schema migration that adds the new columns (mirrors the `RecurringExpense.AccountId` precedent:
old field goes nullable, new field added alongside, in one clean migration), then simply never read or
written by any new code path going forward. A dead, unused, nullable column costs nothing functionally
and stays trivially recoverable if the backfill logic is ever found to have a bug on real data; dropping
it destroys that recovery option for a genuinely new, unproven (in this codebase) kind of migration step.
**This call is specifically because this app has zero production users** (confirmed repeatedly elsewhere
in this project's history) — the only data at risk is a developer's own local test/dev data, not a real
end user's; this reasoning should NOT be copy-pasted into a future migration once the app has real users
and a real "drop the now-migrated column" cleanup slice becomes the more normal next step.

**Migration review gate**: confirm the generated migration's `Up()` shows `CreateTable("FinancialInstitutions")`,
`CreateTable("CardNetworks")` (Slice B), `AddColumn<Guid>("InstitutionId")` ×4 (nullable) +
`AddColumn<Guid>("NetworkId")` (Slice B, nullable) + `AlterColumn` making `Issuer`/`Institution` nullable
on all 4 tables — no `DropColumn` anywhere this slice.

**TPH shared-column gotcha — now a bigger blast radius than before, must not be missed**: per this
project's own [[ef-tph-shared-column-gotcha]] memory (previously only `TermDeposit`/`InvestmentFund`'s
`Institution` needed `.HasColumnName("Institution")`, since `CreditCard.Issuer`/`Loan.Institution` had
*different* property names and never collided). Renaming everything to one consistent `InstitutionId`
property name **introduces a NEW collision that didn't exist before**: `CreditCard.InstitutionId`/
`Loan.InstitutionId` now share a property name on the same `CreditAccount` TPH table too. **Two separate
`.HasColumnName("InstitutionId")` pairs are needed, not one**: `TermDepositConfiguration`+
`InvestmentFundConfiguration` (existing pair, just rename the pinned column from `"Institution"` to
`"InstitutionId"`) AND `CreditCardConfiguration`+`LoanConfiguration` (new pair, doesn't exist today).
`CardNetwork`'s `NetworkId` (Slice B) is CreditCard-only, no sibling collision, no pin needed.

## Decision 3 — CardNetwork scope: CreditCard only, confirmed no other consumer

Read `Loan.cs`/`TermDeposit.cs`/`InvestmentFund.cs` directly — none has any card/network concept, and
none plausibly should (a loan, term deposit, or investment fund is never "a network"). `CreditCard.cs`
currently has **no** network/brand field at all (checked: `Issuer`, `LastFourDigits`, `CreditLimit`,
interest rates, cutoff/due days — nothing else). So `NetworkId` is a wholly **new** field with no prior
data to convert — zero backfill needed for it (unlike `InstitutionId` ×4, which each replace real
existing data). Confirmed scope: Slice B touches `CreditCard` only.

**`NetworkId` is optional** (`Guid? networkId = null`, no requiredness validation in the constructor or
`AddCreditCardViewModel.SaveAsync`) — unlike `InstitutionId`, which preserves `Issuer`'s existing
required-ness. Justification: this is a brand-new enrichment field (not a required-field replacement),
nothing in the accounting rules anywhere in this app (semáforo, purchased-vs-paid, snowball) ever
references card network, and `CreditCard` already has several genuinely-optional nullable fields
(`LastFourDigits`, `AnnualInterestRate`, `MonthlyInterestRate`) this matches exactly.

## Decision 4 — UI: reachable from Settings, not a new TabBar tab; navigate-away Add flow, not inline-add

**Not a new TabBar tab.** Read `AppShell.xaml` directly: it's a flat 9-tab `TabBar` today (Dashboard,
Accounts, Credit, Transactions, Categories, Budgets, RecurringExpenses, People, Settings) — **there is
no existing "More" overflow tab in this app** (that's an iOS `UITabBarController` behavior; this app is
Android-only per `CLAUDE.md`, and MAUI's Android Shell renderer doesn't auto-collapse tabs). Adding 2
more tabs would push this to 11 in an already-full, Android-only bottom bar — a real UX regression, not
a neutral choice. Both `Category`/`Person` earned top-level tabs because they're touched on *every*
transaction; `FinancialInstitution`/`CardNetwork` are set once per account/card at creation and rarely
revisited — much closer in usage frequency to Local Profiles/Cloud Account (both reached via buttons
*inside* Settings, not tabs) than to Category/Person.

**Decision: both reached via buttons in a new Settings section**, mirroring the existing
`Profiles_SectionTitle`/`Profiles_ManageButton` pattern in `SettingsPage.xaml` exactly (bold 18pt section
title Label, then one `Button` per list, `Command="{Binding GoToManage...Command}"`). Not scattered as
buttons on `AccountsListPage`/`CreditCardsListPage` instead — both lists are shared across entities that
live on *different* tabs (Accounts owns TermDeposit/InvestmentFund, Credit owns CreditCard/Loan), so
Settings is the one neutral, already-established hub, avoiding two duplicate entry points for the same
list.

**Not inline-add-while-picking on the Add-account screens.** Checked whether this codebase has any
existing "add a new X inline while picking X elsewhere" affordance for Category or Person (both have the
exact same "user might need a new one mid-flow" scenario `AddTransactionViewModel` faces on every
Expense) — **it does not, anywhere**, confirmed by reading `AddPersonPage.xaml`/`AddTransactionViewModel.cs`
directly. Building a new interaction pattern here that doesn't exist for the two closest precedents
would be inventing scope, not mirroring it, and would directly contradict the explicit instruction to
follow "the same pattern as Categories/People." Decision: navigating to the dedicated Add screen first
(then back) is the established, consistent, correct choice — a user without their bank listed yet
leaves the Add-CreditCard/Loan/TermDeposit/InvestmentFund flow, adds it via the new list screen, comes
back and picks it, exactly like adding a new Category or Person today.

**New screens** (mirror `PeopleListPage`/`AddPersonPage`/`PeopleListViewModel`/`AddPersonViewModel`/
`PersonListItem` file-for-file): `FinancialInstitutionsListPage`, `AddFinancialInstitutionPage`,
`FinancialInstitutionsListViewModel`, `AddFinancialInstitutionViewModel`, `FinancialInstitutionListItem`
(Slice A); `CardNetworksListPage`, `AddCardNetworkPage`, `CardNetworksListViewModel`,
`AddCardNetworkViewModel`, `CardNetworkListItem` (Slice B). Each Add-ViewModel: a single `Name` `Entry` +
`SaveCommand`/`CancelCommand`, validation mirrors `AddPersonViewModel`'s name-required check exactly.

**Existing Add-screens' Entry→Picker swap** (Slice A, all 4; `NetworkId` Picker only on Slice B's
`AddCreditCardPage`): `AddCreditCardViewModel.cs`/`AddLoanViewModel.cs`/`AddTermDepositViewModel.cs`/
`AddInvestmentFundViewModel.cs` each inject `IFinancialInstitutionRepository`, replace the free-text
`Issuer`/`Institution` `string` property + its validation with a `NamedOption`-backed `Picker`
(`SelectedInstitution`/`InstitutionOptions`, same idiom as every other picker in this app), sourced from
`GetAllAsync()` (no active/inactive concept on `FinancialInstitution`, so no `GetActiveAsync()` filter
question even arises). `AddCreditCardViewModel.cs` additionally injects `ICardNetworkRepository` for an
**optional** `SelectedNetwork`/`NetworkOptions` Picker (Slice B) — no "required" validation, matching
Decision 3. `AddCreditCardPage.xaml`/`AddLoanPage.xaml`/`AddTermDepositPage.xaml`/
`AddInvestmentFundPage.xaml` each get one `Entry` replaced by one `Picker` (Slice A) +
`AddCreditCardPage.xaml` gets one new optional `Picker` (Slice B) — small, mechanical XAML edits, no new
layout pattern.

## Read-side consumers — every place `.Issuer`/`.Institution` is read today, all need the same FK→name-at-read-time treatment already established by the Recurring-Expense slice

Grepped `.Issuer`/`.Institution` across `App`+`Application` directly — this is the complete list, not a
partial one:

- `CreditCardListItem.cs`, `LoanListItem.cs`, `TermDepositListItem.cs`, `InvestmentFundListItem.cs` —
  each `FromDomain(...)` factory currently takes the entity and reads `.Issuer`/`.Institution` straight
  off it; each gains an extra `string institutionName` parameter instead (record property renamed
  `InstitutionName`), resolved by the caller.
- `CreditCardsListViewModel.cs`, `LoansListViewModel.cs`, `TermDepositsListViewModel.cs`,
  `InvestmentFundsListViewModel.cs` — each injects `IFinancialInstitutionRepository`, builds a
  `Guid → Name` dictionary from `GetAllAsync()` (mirrors `RecurringExpensesListViewModel`'s
  `accountNames` pattern from the prior slice), passes the resolved name into `FromDomain`.
- `InvestmentFundDetailViewModel.cs:101` (`Institution = fund.Institution`) — same resolve-by-FK swap,
  this ViewModel already has (or gains) `IFinancialInstitutionRepository`.
- `TermDepositRenewalService.cs:58` (`Application` layer, **not App**) — populates a renewal-result DTO's
  `Institution` string field today by copying `TermDeposit.Institution` directly; needs
  `IFinancialInstitutionRepository` injected and a lookup instead. The DTO itself stays string-typed —
  `DashboardViewModel.cs:233`, which only *consumes* that already-resolved string for its renewal
  notification text, needs **no change**.
- `TermDeposit.RenewAtMaturity`'s own internal call (constructs a new `TermDeposit`, currently copies
  `Institution` verbatim) — becomes `InstitutionId` verbatim, a one-line change within the entity itself.
  Add a defensive guard (clear exception, not a silent `Guid.Empty`/NRE) for the edge case of renewing a
  deposit whose `InstitutionId` is somehow still null — should be unreachable in practice (the backfill
  runs at every startup, before the Dashboard load that triggers renewals ever fires), but cheap to guard.

## Tests

- **Domain**: `FinancialInstitution`/`CardNetwork` construction validation (empty/too-long name throws).
  `CreditCard`/`Loan`/`TermDeposit`/`InvestmentFund` constructor tests updated for the `InstitutionId`
  param shape (mechanical). `TermDeposit.RenewAtMaturity` regression test confirms `InstitutionId`
  (not the old string) carries forward to the renewal.
- **Infrastructure** (this is the one genuinely new test category this slice needs, given the backfill
  is the first real data-migration in this project): a real-SQLite test seeding pre-migration-shaped rows
  (old free-text populated, new FK null) across all 4 entity types — including one deliberately
  *near-duplicate* pair (e.g. "Banco Agrícola" / "banco agrícola") — then running
  `FinancialInstitutionBackfillService`, asserting: one `FinancialInstitution` row per *distinct exact
  string* (the near-duplicate pair produces 2 rows, proving the deliberate no-fuzzy-matching choice),
  every row's `InstitutionId` set correctly, and a second run is a true no-op (idempotency).
- **App-layer**: none — matches this codebase's established gap, not a new exception.
- **resx**: new keys for both new screens (List+Add ×2) + the new Settings section/buttons, ES+EN parity
  confirmed by diff, matching every prior slice's own closing check.

## Build order

Domain (2 new entities + 4 existing entities' field swap) → Infrastructure (2 new configs/repos + 4
existing configs' `HasColumnName` fix + migration + backfill service, wired into
`FinanceDatabaseInitializer`) → Application (`TermDepositRenewalService`'s one lookup swap; the 2 new
repository interfaces are technically Application-layer abstractions, sequence them with Domain since
nothing in Application *uses* them yet besides that one swap) → App (2 new screen pairs, 4 existing
Add-screens' Picker swap, 4 List-Item + List-ViewModel pairs, 1 Detail ViewModel, Settings section).

## Explicitly not in scope / not escalated

No delete/deactivate for either new entity (matches Person/Category precedent exactly). No uniqueness
constraint on `Name` (same precedent — neither Person nor Category enforces one; also sidesteps a
genuinely hard case-insensitive/diacritic-aware uniqueness problem this slice doesn't need to solve). No
search/filter on either new list screen (neither precedent has one; trivial to add later if a list grows
unwieldy). No inline-add affordance (Decision 4). No dropping of the old free-text columns this slice
(Decision 2). No seed data for either entity (Decision 1). No country/SWIFT/icon fields on
`FinancialInstitution` (not requested, not in README). Zero questions escalated to the user this
slice — the user already confirmed the core intent directly; every implementation-detail call above
either cites a direct codebase precedent (Person/Category/RecurringExpense.AccountId/the TPH memory
skill) or is justified inline the way this role's own conventions require.
