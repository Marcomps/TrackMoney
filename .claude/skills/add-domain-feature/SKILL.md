---
name: add-domain-feature
description: Scaffold a new financial concept end-to-end across TrackTraceMoney's layered architecture — Domain entity, Application interfaces/DTOs/use case, Infrastructure EF configuration/repository, and App ViewModel/View — in the correct reference direction, with the project's accounting rules respected. Use when adding a new transaction type, account type, or report described in README.md.
---

# Adding a feature across TrackTraceMoney's layers

Before writing code, confirm the feature belongs in the current roadmap phase (README §51 / CLAUDE.md's "Roadmap phase boundaries") — if unsure, that's a question for the `product-owner` agent, not a judgment call to make silently.

## Build in this order (matches the reference direction — never skip ahead)

1. **`src/TrackTraceMoney.Domain`** — the entity/enum/value object and any business rule that doesn't need I/O. Zero project references allowed here; if the rule needs a database read, it doesn't belong in Domain.
2. **`src/TrackTraceMoney.Application`** — the interface(s) a use case needs (e.g. `IAccountRepository`), the DTOs crossing into the UI, and the use-case/service class orchestrating Domain logic. References Domain only.
3. **`src/TrackTraceMoney.Infrastructure`** — EF Core entity configuration (`IEntityTypeConfiguration<T>` or `OnModelCreating`), the repository implementation, and a migration if the schema changed (see the `ef-core-migration` skill). References Application + Domain.
4. **`src/TrackTraceMoney.App`** — ViewModel (`CommunityToolkit.Mvvm` `[ObservableProperty]`/`[RelayCommand]`) consuming the Application-layer interfaces via DI, then the View (XAML). Never let a View or ViewModel reference `TrackTraceMoney.Infrastructure` types directly — only Application abstractions.

## Re-check these before calling it done (most bugs in this codebase come from skipping these)

- Does this transaction type count as "gasto" in reports/budgets, or is it a transfer/payment/investment move that must be excluded? (See CLAUDE.md's "Non-obvious domain rules.")
- If this touches credit cards: is the purchase vs. payment distinction preserved, and are "pago mínimo"/"pago para evitar intereses" kept as independent, user-entered fields?
- If this touches debt payoff: does the snowball calculation target the smallest-balance debt after minimums, and re-target when a debt reaches zero?
- If this touches net worth or "sobrante real": are these computed from current balances/known upcoming obligations, not from summing transaction history?
- If this touches reimbursements: is the original expense left untouched, and is an unreceived reimbursement excluded from available balance?
- Does every new account/transaction carry an explicit currency, and every expense carry both payer and beneficiary (person) where relevant?
- Does every new UI-visible string go into both `AppResources.resx` and `AppResources.en.resx`? (See the `add-localized-text` skill.)
- Does the feature work fully offline? No new flow should assume network reachability in Phase 1–3.

## Before finishing

Run `dotnet build` for the whole solution, then hand off to (or invoke) the `qa` agent for the accounting-correctness checks above — implementing and verifying are separate passes, don't skip the second one for anything touching money math.
