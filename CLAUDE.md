# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

The solution builds clean (`dotnet build`, 0 warnings/0 errors as of this writing). Phase 1 domain/application/infrastructure code exists (accounts, transactions, budgets, categories, people; repositories and EF Core configurations; an initial migration) and is wired into the MAUI app's DI via `MauiProgram.cs`. The App project's Views/ViewModels are still MAUI template defaults — no real screens have been built yet, and no tests exist despite the three xUnit projects being scaffolded.

Read `README.md` before implementing anything; it is the authoritative spec (52 sections covering domain model, business rules, UX, and roadmap), and it is the **English** version — `README.es.md` is a parallel Spanish translation. Keep both in sync when the spec changes; do not edit one without mirroring the change in the other. Do not restate the spec's contents here — this file only captures what a future Claude instance needs that isn't obvious from re-reading that spec once code exists.

## Tech stack

- **Frontend**: .NET MAUI (net10.0-android — Android-only for now; iOS/Mac Catalyst/Windows TFMs intentionally omitted from `TrackTraceMoney.App.csproj`, add back when those platforms are in scope), C#, XAML, MVVM, `CommunityToolkit.Mvvm` 8.4.2, `CommunityToolkit.Maui` 15.0.1
- **Persistence**: SQLite via `Microsoft.EntityFrameworkCore.Sqlite` 10.0.11 (+ `.Design` for migrations) in `TrackTraceMoney.Infrastructure` — SQLite is the on-device source of truth, not a cache
- **Cloud backend** (Phase 4, scaffolding only as of this writing — no auth/backup/sync endpoints yet): `TrackTraceMoney.Api`, an ASP.NET Core Web API (net10.0) with its own PostgreSQL persistence via `Npgsql.EntityFrameworkCore.PostgreSQL` (`TrackTraceMoneyCloudDbContext`, entirely separate from the on-device SQLite `TrackTraceMoneyDbContext`). Local dev Postgres runs via `docker-compose.yml` at the repo root — see the Commands section below.
- **SDK**: .NET 10 (10.0.400). `Microsoft.Maui.Controls` is pinned explicitly to `10.0.100` in `TrackTraceMoney.App.csproj` rather than left on `$(MauiVersion)`, because the installed `maui-windows` workload manifest still resolves that MSBuild property to `10.0.20`, which is older than what `CommunityToolkit.Maui` 15.x requires (`>=10.0.60`). If workload updates ever bump `$(MauiVersion)` past `10.0.100`, it's safe to switch back to `$(MauiVersion)`.
- `MauiProgram.cs` chains `.UseMauiCommunityToolkit()` after `.UseMauiApp<App>()` — required by CommunityToolkit.Maui's `MCT001` analyzer; don't drop it when touching that file.

## Solution layout

```
TrackTraceMoney.slnx               # .NET 10's XML solution format — not a classic .sln
docker-compose.yml                 # local dev PostgreSQL for TrackTraceMoney.Api only — dev-only credentials, not for any real deployment
src/
  TrackTraceMoney.Api/             # ASP.NET Core Web API — net10.0, refs Application + Domain (NOT Infrastructure/App).
                                    #   Own PostgreSQL persistence (Persistence/TrackTraceMoneyCloudDbContext) and own
                                    #   Migrations/ folder — separate from Infrastructure's on-device SQLite context.
                                    #   Phase 4 scaffolding only as of this writing: one unauthenticated /health endpoint,
                                    #   an empty placeholder "InitialCreate" migration. No auth/backup/sync endpoints yet.
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
  TrackTraceMoney.E2E.Tests/            # xUnit + Appium.WebDriver, no project refs (drives the built APK
                                         #   over Appium/HTTP, not in-process) — see Commands section below
```

No `TrackTraceMoney.Api.Tests` project yet — deliberately deferred until the Api project has real logic worth testing (the skeleton's only endpoint is a built-in health check).

Reference direction: `App` → `Infrastructure`/`Application`/`Domain`; `Infrastructure` → `Application`/`Domain`; `Api` → `Application`/`Domain`; `Application` → `Domain`. Domain has zero project references. Keep it that way — business rules and entities must not depend on EF Core, MAUI, or any I/O concern. `Api` and `Infrastructure` are siblings, not layered on each other — the Api project must never reference `Infrastructure` (on-device SQLite store) or `App` (MAUI client).

## Commands

```
dotnet build                        # build everything (App + Domain + Application + Infrastructure + all test projects)
dotnet test                         # run all test projects -- INCLUDES TrackTraceMoney.E2E.Tests, which needs a live
                                     #   emulator + Appium server (see that project's own Commands subsection below)
                                     #   and fails loudly (not silently skips) without one; prefer the per-project form
                                     #   below unless you actually want E2E in the mix
dotnet test tests/TrackTraceMoney.Domain.Tests           # run one test project (dotnet test rejects multiple project paths in one invocation — run separately or use the .sln)
dotnet test --filter FullyQualifiedName~ClassName.MethodName  # run a single test
```

Building `TrackTraceMoney.App` produces an Android build; there's no emulator/device wiring set up yet, so `dotnet build` verifies compilation only, not that the app runs.

### Local dev PostgreSQL (`TrackTraceMoney.Api`, Phase 4)

Local dev only — not used for production/staging, and D4 (production hosting target) is still an open decision.

```
docker compose up -d                # start local Postgres (named volume persists data across restarts)
dotnet ef database update --project src/TrackTraceMoney.Api --startup-project src/TrackTraceMoney.Api
docker compose down                 # stop (data survives); add -v to also wipe the local dev volume
```

Unlike `TrackTraceMoney.Infrastructure` (see the `ef-core-migration` skill), `TrackTraceMoney.Api` is a normal runnable ASP.NET Core host, so it can be its own EF `--project`/`--startup-project` with no `IDesignTimeDbContextFactory` workaround needed — `dotnet-ef` discovers `TrackTraceMoneyCloudDbContext` straight from `Program.cs`'s `builder.Services.AddDbContext<...>()` registration. Connection string comes from `appsettings.Development.json`'s `ConnectionStrings:CloudDatabase` (dev-only throwaway credentials, matching `docker-compose.yml`) — never hardcode real credentials into committed source.

### End-to-end UI tests (`TrackTraceMoney.E2E.Tests`, Appium)

Drives the real, installed Android app through Appium's UiAutomator2 driver — not a ViewModel-in-isolation unit test. Local-only, like the Postgres section above: nothing here runs in CI yet. Full "how do I run this" narrative lives in `E2ETestBase`'s own doc comment (single source of truth, so it can't drift out of sync with this file); short version:

```
# One-time setup (already done on the dev machine this suite was authored on):
#   - Android SDK with `emulator` + platform-tools at C:\Users\PC\android-sdk-local
#   - AVD `TrackMoneyTest` (Pixel 5, API 34, google_apis, x86_64)
#   - npm install -g appium && appium driver install uiautomator2

C:\Users\PC\android-sdk-local\emulator\emulator.exe -avd TrackMoneyTest -port 5556 -gpu swiftshader_indirect -no-boot-anim   # start the emulator (serial emulator-5556, the E2E default; override with TTM_DEVICE_SERIAL)
dotnet build src/TrackTraceMoney.App/TrackTraceMoney.App.csproj -f net10.0-android -p:RuntimeIdentifier=android-x64 -p:AndroidPackageFormat=apk -p:EmbedAssembliesIntoApk=true
"C:\Users\PC\android-sdk-local\platform-tools\adb.exe" -s emulator-5556 install -r src\TrackTraceMoney.App\bin\Debug\net10.0-android\android-x64\com.tracktracemoney.mobile-Signed.apk
set ANDROID_HOME=C:\Users\PC\android-sdk-local & set ANDROID_SDK_ROOT=C:\Users\PC\android-sdk-local & appium --address 127.0.0.1 --port 4723   # Appium server needs these env vars to find adb/uiautomator2, not just adb itself
dotnet test tests/TrackTraceMoney.E2E.Tests
```

Every `[Fact]` clears app data (`adb shell pm clear`) and relaunches as its own first step (`E2ETestBase.ResetApp`), so tests are safe to run in any order/subset and the whole suite is safe to rerun repeatedly without manually resetting device state between runs. Screenshots land in `tests/TrackTraceMoney.E2E.Tests/Evidence/<ScenarioName>/<step>.png` (gitignored — regenerated every run, not source). Two non-obvious Appium/MAUI gotchas discovered building this suite (both documented at their point of use in `E2ETestBase.cs`, repeated here since they're easy to rediscover the hard way otherwise): (1) MAUI's Android renderer maps `AutomationId` to the native view's `resource-id`, not `content-desc` — the usual "accessibility id" Appium strategy silently finds nothing on this app; locate by resource-id via a `-android uiautomator`/`UiSelector().resourceId(...)` query instead. (2) Plain `By.Id` doesn't reach the server as an "id" strategy lookup either (Selenium's .NET client rewrites it to a CSS selector, which UiAutomator2 can't resolve against a native app) — same fix applies.

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
