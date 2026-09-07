# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

The solution builds clean (`dotnet build`, 0 warnings/0 errors as of this writing). Phase 1 domain/application/infrastructure code exists (accounts, transactions, budgets, categories, people; repositories and EF Core configurations; an initial migration) and is wired into the MAUI app's DI via `MauiProgram.cs`. The App project's Views/ViewModels are still MAUI template defaults — no real screens have been built yet, and no tests exist despite the three xUnit projects being scaffolded.

Read `README.md` before implementing anything; it is the authoritative spec (52 sections covering domain model, business rules, UX, and roadmap), and it is the **English** version — `README.es.md` is a parallel Spanish translation. Keep both in sync when the spec changes; do not edit one without mirroring the change in the other. Do not restate the spec's contents here — this file only captures what a future Claude instance needs that isn't obvious from re-reading that spec once code exists.

## Tech stack

- **Frontend**: .NET MAUI (net10.0-android — Android-only for now; iOS/Mac Catalyst/Windows TFMs intentionally omitted from `TrackTraceMoney.App.csproj`, add back when those platforms are in scope), C#, XAML, MVVM, `CommunityToolkit.Mvvm` 8.4.2, `CommunityToolkit.Maui` 15.0.1
- **Persistence**: SQLite via `Microsoft.EntityFrameworkCore.Sqlite` 10.0.11 (+ `.Design` for migrations) in `TrackTraceMoney.Infrastructure` — SQLite is the on-device source of truth, not a cache
- **Future backend** (not part of MVP): ASP.NET Core Web API, PostgreSQL, Docker
- **SDK**: .NET 10 (10.0.400). `Microsoft.Maui.Controls` is pinned explicitly to `10.0.100` in `TrackTraceMoney.App.csproj` rather than left on `$(MauiVersion)`, because the installed `maui-windows` workload manifest still resolves that MSBuild property to `10.0.20`, which is older than what `CommunityToolkit.Maui` 15.x requires (`>=10.0.60`). If workload updates ever bump `$(MauiVersion)` past `10.0.100`, it's safe to switch back to `$(MauiVersion)`.
- `MauiProgram.cs` chains `.UseMauiCommunityToolkit()` after `.UseMauiApp<App>()` — required by CommunityToolkit.Maui's `MCT001` analyzer; don't drop it when touching that file.

## Solution layout

```
TrackTraceMoney.slnx               # .NET 10's XML solution format — not a classic .sln
src/
  TrackTraceMoney.App/             # MAUI Views, ViewModels, Resources, Navigation, Styles — net10.0-android. Also the only project
                                    #   that can host Android-specific code (e.g. local notifications via NotificationCompat),
                                    #   since Infrastructure is net10.0-only and has no Android SDK reference.
  TrackTraceMoney.Domain/          # Entities, Enums, business rules — net10.0, no dependencies
  TrackTraceMoney.Application/     # Services, Interfaces, DTOs, Use Cases — net10.0, refs Domain
  TrackTraceMoney.Infrastructure/  # SQLite, EF Core, Repositories, Migrations — net10.0, refs Application + Domain
tests/
  TrackTraceMoney.Domain.Tests/         # xUnit, refs Domain
  TrackTraceMoney.Application.Tests/    # xUnit, refs Application + Domain
  TrackTraceMoney.Infrastructure.Tests/ # xUnit, refs Infrastructure + Application + Domain
```

`TrackTraceMoney.Api` (future backend, roadmap Phase 4) does not exist yet — do not add it before that phase is actually scoped.

Reference direction: `App` → `Infrastructure`/`Application`/`Domain`; `Infrastructure` → `Application`/`Domain`; `Application` → `Domain`. Domain has zero project references. Keep it that way — business rules and entities must not depend on EF Core, MAUI, or any I/O concern.

## Commands

```
dotnet build                        # build everything (App + Domain + Application + Infrastructure + all test projects)
dotnet test                         # run all test projects (currently 0 tests — none written yet)
dotnet test tests/TrackTraceMoney.Domain.Tests           # run one test project (dotnet test rejects multiple project paths in one invocation — run separately or use the .sln)
dotnet test --filter FullyQualifiedName~ClassName.MethodName  # run a single test
```

Building `TrackTraceMoney.App` produces an Android build; there's no emulator/device wiring set up yet, so `dotnet build` verifies compilation only, not that the app runs.

## Non-obvious domain rules (easy to get wrong)

These are the accounting semantics from README sections 9, 16, 20, 24, 28, 46 that are easy to implement incorrectly if you only skim the entity list:

- **A transaction is not automatically an expense.** `Transfer`, `CreditCardPayment`, `LoanPayment`, `InvestmentContribution`/`Withdrawal` all move money between accounts/liabilities but must **not** be counted as spend in reports/dashboards. Only `Expense` (and the originating `CreditCardPurchase`) count as spend.
- **Credit card purchase vs. payment are separate events.** The purchase is the expense (and increases card debt); the later payment reduces bank balance and reduces debt — it is not a new expense. Double-counting here is the #1 correctness risk in this domain.
- **"Pago mínimo" (minimum payment) and "pago para evitar intereses" (pay-in-full amount) are distinct, user-entered fields** — never derive one from the other or hardcode bank-specific interest rules. The card health "semáforo" (traffic light) logic depends on comparing actual payments against both, separately.
- **Snowball ("Bola de Nieve") strategy**: minimums/obligatory payments are covered first; any surplus goes to the debt with the *smallest balance*, not highest interest (that's the future "avalanche" variant, not MVP). This is a configurable strategy, not an enforced behavior.
- **Net worth** = sum of asset accounts (cash, bank, savings, term deposits, investments) minus liability accounts (cards, loans). Must be computed from account balances, not from transaction sums, to stay correct as new account types are added.
- **Reimbursements are income/recovery entries linked to the original expense, not negative expenses**, and must not retroactively delete/modify the original expense record. An *expected* reimbursement is not available balance until actually received.
- **"Sobrante real" (real surplus)** ≠ current account balance — it's balance minus known upcoming expenses and debt payments. Don't conflate the two in dashboard/report code.

## Cross-cutting requirements that affect implementation from day one

- **Offline-first**: every core flow (CRUD on transactions/accounts, history, budgets, local reports, dashboards, local reminders) must work with zero network access. Don't introduce a dependency on network availability for any MVP (Phase 1–3) feature.
- **Localization from the start**: no hardcoded UI strings — use `.resx` resources (`AppResources.resx` / `AppResources.en.resx`) for ES/EN from the first screen, not retrofitted later.
- **Currency is explicit per account/transaction**, not a single global setting baked into calculations — accounts and operations store their own currency.
- **Person vs. payer distinction**: expenses track both who paid and who the expense was for (see README §7, §26) — don't collapse these into a single field.

## Roadmap phase boundaries

Per README §51 — respect these boundaries when deciding what a change should include:

- **Phase 1 (MVP)**: MAUI + SQLite + MVVM, ES/EN, accounts, income/expense/transfers, categories, history, dashboard, budgets, real surplus, recurring expenses, local notifications, local backup.
- **Phase 2**: Credit cards, billing cycles/statements, min-payment vs. pay-in-full, purchased-vs-paid analysis, card semáforo, loans, snowball.
- **Phase 3**: Savings, term deposits, investment funds, net worth, medical expenses, insurance, reimbursements.
- **Phase 4**: Cloud (user accounts, API, PostgreSQL, cloud backup/restore, sync) — do not add cloud/sync code before this phase is actually scoped.
- **Phase 5**: Multi-device, web dashboard, financial AI.

Don't pull forward Phase 4/5 concerns (sync IDs, conflict resolution, API auth) into MVP entity/schema design unless explicitly asked — but keep entities free of assumptions that would make later sync impossible (e.g., avoid non-unique local-only identity schemes for core entities).

## Project agents and skills

This repo defines role-specific subagents in `.claude/agents/` — invoke them (via the Agent tool, or by name) instead of doing all roles as the general assistant when the task fits one squarely:

- **`product-owner`** — scopes work against the roadmap phases above, writes user stories/acceptance criteria, resolves requirement ambiguity. Read-only on code.
- **`dev`** — implements features across the four layers per this file's architecture and domain-rules sections.
- **`qa`** — reviews/tests changes against the accounting rules above (double-counting, snowball, net worth, reimbursements, offline behavior, localization).
- **`infra-architect`** — owns EF Core/SQLite schema, migrations, project/workload/package version issues, backup/restore.

Project skills in `.claude/skills/` encode procedures specific to this codebase — `ef-core-migration` (the MAUI-app-can't-be-the-EF-startup-project workaround), `add-domain-feature` (the layer-by-layer build order plus the recheck list), and `add-localized-text` (the ES/EN `.resx` workflow). Prefer them over ad hoc approaches for those tasks — they exist because those tasks have non-obvious gotchas already discovered once.
