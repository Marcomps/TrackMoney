# App Lock (README §43 "Security") — Slice Spec

Scoped by `product-owner`, 2026-09-17. Source: README §43 (verbatim, it's short):

> The app can optionally be protected using: PIN, Biometrics, Automatic lock. Local data must be
> stored securely whenever technically possible.

## Phase tagging — gap noted explicitly

CLAUDE.md's Phase 1 list names "local backup," notifications, budgets, etc. by name but does not
mention security/lock anywhere in the 5-phase roadmap. §43 itself is silent on phase too — it's
just one more numbered section in the spec with no roadmap cross-reference. Given (a) it has zero
network/cloud dependency (works fully offline, consistent with every genuine Phase 1 feature),
(b) it protects data that has existed since Phase 1 (accounts/transactions) and Phase 2/3 data
that exists today, and (c) the user has flagged it as the top-priority gap in a live app with real
financial data and zero protection — **treat this as a standalone, phase-independent, cross-cutting
slice, buildable now**, not blocked on/deferred to any specific numbered phase. Recommend recording
it in `trackmoney_roadmap_progress.md` as its own tracked item once scoped, separate from the
Phase 1-5 feature backlog, exactly as the (also out-of-roadmap) Local Profiles feature was.

## Decisions

### 1. Scope: PIN-only in slice 1; biometric is a fast-follow slice 2, not bundled in

§43 lists PIN/Biometrics/Automatic-lock as three independent options, not a sequence, but they are
not equally cheap to build. Biometric unlock on Android (`BiometricPrompt`) needs its own
Android-specific implementation (this app's `App` project is the only one with Android SDK access
per CLAUDE.md's layout table — same reason `AndroidLocalNotifier` lives there), enrollment-state
detection, and a defined fallback when no biometric is enrolled or hardware is absent (which still
requires a working PIN underneath it regardless). Shipping PIN-only first gives a complete,
independently-valuable, testable vertical slice (Settings toggle → set PIN → lock screen → unlock →
auto-lock) with zero Android-platform-specific crypto/biometric API surface, then layers biometric
on top of an *already-set* PIN as pure convenience in slice 2 — mirrors this codebase's own
established "ship the base mechanism first, wire the convenience layer later" precedent (e.g.
`InvestmentFund.RecordContribution` shipped unwired in its own slice, wired in the next one).
Biometric-only-no-PIN is explicitly rejected: Android's `BiometricPrompt` typically requires a
device-level credential (PIN/pattern/biometric) already enrolled as its own fallback, and this
app's PIN is a separate, app-level secret, not a proxy for the device lock — conflating the two
would mean a phone with no device lock configured could never protect the app at all.

### 2. Per-device, not per-profile

**Decision: one device-wide lock, set once, gates entry to the whole app — including the profile
list/switcher itself — before any profile becomes visible or selectable.** Not an independent
optional PIN per `LocalProfile`.

Reasoning, grounded in the actual code:
- `AddProfileViewModel.Name` and `CreateFirstProfileViewModel.Name` (read directly) are plain
  free-text strings with no content constraint beyond non-empty — a profile is very plausibly named
  after a real person ("Marco", "Ana's finances") or something else identifying, not just a role
  label. The Local Profiles feature exists specifically for multiple people sharing one device, so
  profile *names* are themselves data worth protecting under the "someone picks up the phone" threat
  model, not just what's inside each profile.
- A per-profile PIN still leaves `ProfilesListPage` — the profile picker itself — reachable
  pre-authentication, which defeats the point above. To truly hide names too, per-profile locking
  would need a *second*, separate device-wide gate in front of the list anyway, at which point the
  per-profile PIN adds complexity (N independent PIN states, N independent set/verify/reset flows)
  without closing any additional gap a single device-wide PIN doesn't already close.
- `App.xaml.cs`'s `CreateWindow` already makes exactly one root-page decision today
  (`CreateFirstProfilePage` vs `AppShell`) off one piece of device-local state
  (`IActiveProfileStore`, itself `Preferences`-backed, i.e. per-device already, not per-profile).
  A device-wide lock composes with that existing branch as a single prior gate; a per-profile lock
  would need to re-derive "is *this* profile's PIN set and verified" per switch, a materially bigger
  change to `ProfilesListViewModel.SwitchProfileAsync`-style flows.

**Explicit non-goal, not silently dropped**: two people sharing one device who each want their own
independent PIN on their own profile can't get that from slice 1. If that's a real requirement,
it's a deliberate later enhancement, not an oversight — flag to the user only if raised.

### 3. Storage — SecureStorage, PBKDF2-HMACSHA256 hash, never plaintext

Mirrors the one precedent this codebase already has for a device-local secret:
`CloudAuthService` stores its access token via `ISecureStorage` (`SecureStorage.Default`,
already registered as a singleton in `MauiProgram.cs`), while purely non-secret app state
(`ActiveProfileId`) uses `Preferences` via `PreferencesActiveProfileStore`. The PIN hash (and its
salt) is a secret by the same standard `CloudAuthService` already applies to its token — store it
in `ISecureStorage` under new keys (e.g. `applock.pin_hash`, `applock.pin_salt`,
`applock.enabled`), never in `Preferences`, and never the raw PIN itself, anywhere, ever (not even
transiently longer than the verification call needs it in memory).

Hashing algorithm: mirror the **algorithm choice**, not the code, of `TrackTraceMoney.Api`'s
`AuthEndpoints`/`CloudUser`, which use ASP.NET Core Identity's `IPasswordHasher<CloudUser>` — that
class's default implementation is PBKDF2 with HMAC-SHA256, a random per-hash salt, and a high
iteration count. The `App` project (MAUI/Android) has no reason to reference
`Microsoft.AspNetCore.Identity` (an ASP.NET-hosting package, not appropriate to pull into a mobile
client) — implement the same algorithm directly with `System.Security.Cryptography.Rfc2898DeriveBytes`
(PBKDF2-HMACSHA256), a plain BCL primitive, no new NuGet dependency. Because a PIN is numeric and
low-entropy by design (see below), the iteration count should be set at least as high as, ideally
higher than, the Api's own password iteration count to compensate — exact number is an
infra/dev-level implementation call, not a product decision, but the requirement ("PBKDF2-HMACSHA256,
random salt, iteration count high enough that offline brute-force of a 6-digit space is
impractical") belongs in this spec.

PIN shape: **6 numeric digits**, entered twice on set (confirmation, standard pattern) — matches
common phone-unlock precedent (e.g. iOS's own default since iOS 9), more entropy than 4 digits at
negligible extra friction.

### 4. Auto-lock trigger — background/foreground with a 30-second grace period, not instant

MAUI's `Window` (the type `App.CreateWindow` already constructs and returns) exposes
`Activated`/`Deactivated` events — no platform-specific `OnPause`/`OnResume` override is needed
(confirmed no existing lifecycle hook of that kind exists anywhere under
`src/TrackTraceMoney.App/Platforms`, this would be new). Hooking those two events is enough to
detect background/foreground transitions from the same `App.xaml.cs` that already owns root-page
selection.

**Instant lock-on-any-backgrounding is explicitly rejected**, not just "a possible choice" — it's
demonstrably wrong given code that already exists in this app: `SettingsViewModel.ExportAsync` calls
`Share.Default.RequestAsync(...)` and `SettingsViewModel.RestoreAsync` calls
`FilePicker.Default.PickAsync(...)`, both of which hand off to a system sheet/activity that
necessarily backgrounds-then-reactivates this app as a normal part of an already-legitimate in-app
action (exporting/restoring a local backup). An instant-lock policy would force a fresh PIN entry
after every single backup export or restore — disproportionate friction for the app's own existing
Settings flows, not a hypothetical edge case.

**Decision: lock again only if the app was backgrounded for more than 30 seconds** (record a
"deactivated at" timestamp in memory on `Window.Deactivated`; on `Window.Activated`, compare against
now and swap to the lock screen only if the gap exceeds the threshold). If the OS kills the process
entirely while backgrounded (Android can, under memory pressure), the in-memory timestamp is moot —
a fresh cold start already re-applies "is a PIN set → show lock screen first" via the same check
`CreateWindow` does today, so process death is covered without extra state. 30 seconds is a specific,
justified default (long enough to survive a Share-sheet/FilePicker round trip, short enough that a
phone picked up minutes later by someone else is still locked) — a real precedent worth citing:
consumer password managers commonly ship a short (seconds-to-low-minutes) grace window rather than
zero, for exactly this "quick legitimate app-switch" reason. This is a fixed default for slice 1,
not user-configurable yet — a future "auto-lock timeout" setting could expose it later with no
architecture change.

### 5. Forgotten PIN — no in-app reset; disclosed once, at PIN-set time; recovery = reinstall + restore an existing backup

Walked through the actual constraint: any in-app "Forgot PIN?" flow that resets or bypasses the PIN
from the lock screen itself would make the entire feature trivially defeatable by exactly the
threat model it exists to stop (anyone who picks up the phone just taps "forgot PIN" and is in) —
this is not a usability nice-to-have, it's a direct contradiction of the feature's purpose, so it's
rejected outright rather than left as an option.

The alternative isn't "invent a recovery secret" (real added scope/complexity disproportionate to
a personal-finance app's threat model, not a bank) — it's recognizing this app **already has** a
recovery path, with zero new mechanism: local data for any Android app is destroyed on
uninstall regardless of this feature. A user who has previously exported a local backup (Settings'
existing `ExportAsync`, already shipped) can uninstall the app (destroying the locked, inaccessible
local database along with the forgotten PIN), reinstall, go through the existing first-run
`CreateFirstProfilePage` flow (which requires no PIN — nothing is locked on a fresh install with no
PIN set yet), and restore their exported backup into the new profile via the existing `RestoreAsync`
flow. **Decision: this composition of two already-shipped mechanisms is the sole recovery path.
No new "forgot PIN" code is built.**

This must be **disclosed explicitly, once, during the "Set PIN" flow** — not left as a silent
trap: a warning shown before the PIN is confirmed and saved (e.g. "If you forget this PIN, the
only way back into the app is to uninstall and reinstall it, which deletes all local data unless
you've exported a backup first — see Settings → Export Backup"). This disclosure is an acceptance
criterion of the "Set PIN" story below, not an afterthought.

### 6. Explicitly out of scope for this slice (do not silently absorb)

- **At-rest SQLite encryption** (§43's second sentence, "local data must be stored securely
  whenever technically possible"). That's a materially different, `infra-architect`-owned concern
  (e.g. SQLCipher or similar for `TrackTraceMoneyDbContext`'s on-disk `.db3` files) — a PIN gate on
  the *app UI* does nothing to protect the raw database file from someone who pulls it off the
  device by other means (adb backup, file-manager copy on a rooted device, etc.). Flagging this as
  a distinct, separate future slice so it isn't assumed "done" once app-lock ships.
- **Android `FLAG_SECURE` / recent-apps thumbnail blanking.** Related but separate: without it, a
  locked (or even unlocked) app's dashboard balance can still appear as a plaintext screenshot in
  Android's recent-apps switcher. Cheap (one Android-specific window flag, lives in the `App`
  project alongside `AndroidLocalNotifier`) and directly reinforces this slice's intent — recommend
  including it as a small additive requirement in slice 1 rather than a separate slice (see Story 6
  below), but flagging it as my own call, not mandatory if deprioritized.
- **PIN brute-force throttling beyond a minimal cooldown.** A 6-digit PIN is only 1,000,000
  combinations — worth *some* protection, but a full lockout/wipe policy is disproportionate scope
  for this app's threat model. Minimal default: after 5 consecutive failed attempts, impose a short
  cooldown before the next attempt is accepted (e.g., 30 seconds), no data wipe, no permanent
  lockout. Anything stronger is explicitly deferred, not decided here.
- **Per-profile locks** — see Decision 2.
- **Configurable auto-lock timeout** — see Decision 4.

No OPEN items requiring user escalation remain — every ambiguity above had a reasoned, code-grounded
default. If the user disagrees with the per-device-not-per-profile call (Decision 2) or the
no-in-app-reset call (Decision 5), those are the two most consequential and worth a second look, but
neither is left undecided.

---

## Slice 1 stories

### Story 1 — Enable app lock and set a PIN (Settings)

New Settings section, same pattern as the existing Profiles/Institutions/Networks/People sections
in `SettingsViewModel`/`SettingsPage.xaml` (a labeled block with its own commands).

Acceptance criteria:
- Settings shows an "App Lock" section with a toggle/switch, initial state off (no PIN set today,
  for every existing install).
- Turning the toggle on navigates to a "Set PIN" screen: two 6-digit numeric entry fields
  (PIN, Confirm PIN), the forgot-PIN disclosure text (Decision 5) shown before submission, and a
  Save action.
- Save is rejected (inline validation, no crash) if: either field isn't exactly 6 digits, or the
  two fields don't match.
- On successful save: PIN hash + random salt written to `ISecureStorage` (never the raw PIN,
  anywhere); the "App Lock" toggle reflects enabled; a confirmation message shown (mirrors existing
  `StatusMessage`/`ErrorMessage` pattern already used throughout `SettingsViewModel`).
- Turning the toggle off (when currently enabled) requires re-entering the current PIN first (not a
  bare switch flip — otherwise anyone with the phone unlocked could silently disable protection),
  then clears the stored hash/salt from `ISecureStorage`.
- All new UI strings localized via `.resx` (ES/EN), per the `add-localized-text` skill — no
  hardcoded strings.

### Story 2 — Lock screen gates app entry when a PIN is set

Acceptance criteria:
- On cold start, if an app-lock PIN is set (`ISecureStorage` has a stored hash), the root page is a
  new Lock screen — constructed directly, not reached via a Shell route, mirroring
  `CreateFirstProfilePage`'s existing precedent for a pre-Shell root page.
- The Lock screen shows a 6-digit PIN entry (numeric keypad), no other app content or navigation
  reachable from it (no back button to bypass into `AppShell`).
- A correct PIN swaps the root page live, matching `CreateFirstProfileViewModel.SaveAsync`'s
  existing swap pattern (`Application.Current.Windows[0].Page = ...`) — **and must re-derive
  the destination exactly as `App.CreateWindow` does today** (`CreateFirstProfilePage` if somehow no
  active profile exists — e.g. the last profile was deleted while locked — otherwise `AppShell`),
  not hardcode `AppShell`, since app-lock is a prior gate layered in front of that existing branch,
  not a replacement for it.
- An incorrect PIN shows an inline error and clears the entry; after 5 consecutive incorrect
  attempts, a short cooldown (per Decision 6) blocks further attempts for a fixed interval, shown
  to the user (e.g. a disabled keypad + countdown), then resets.
- If no PIN is set (the common case for every install before Story 1 is used, and for anyone who
  never enables it), behavior is byte-for-byte unchanged from today — `App.CreateWindow`'s existing
  branch runs exactly as it does now. This must not regress any existing flow.

### Story 3 — Auto-lock on background beyond the grace period

Acceptance criteria:
- While an app-lock PIN is set, backgrounding the app for more than 30 seconds (Decision 4) and
  returning to it shows the Lock screen again, not the page the user was previously on.
- Backgrounding for 30 seconds or less (e.g. a Share-sheet or FilePicker round trip from Settings'
  existing Export/Restore actions) does **not** re-lock — verify explicitly against
  `SettingsViewModel.ExportAsync`/`RestoreAsync`'s existing `Share.Default.RequestAsync`/
  `FilePicker.Default.PickAsync` calls as the concrete regression case.
- If the OS terminates the process while backgrounded, the next cold start re-applies Story 2's
  check (no separate handling needed — covered by design, verify it isn't accidentally bypassed).

### Story 4 — Recent-apps thumbnail does not leak data (Decision 6, recommended inclusion)

Acceptance criteria:
- With app-lock enabled, the Android recent-apps switcher shows a blank/generic thumbnail instead
  of a live screenshot of the last-viewed screen (e.g. Dashboard balances).
- If deprioritized out of slice 1, must be explicitly deferred (not silently dropped) with a note
  in the roadmap-progress memory file.

### Story 5 — Regression guard: profiles feature is unaffected when app-lock is off

Acceptance criteria:
- With no PIN ever set, `ProfilesListPage`, profile switching, and profile creation behave exactly
  as they do today — this slice adds a prior gate, it does not change anything about
  `IActiveProfileStore`, `ProfileManagementService`, or their consumers.

---

## Technical insertion points (context for `dev`, informational — not binding on implementation choices)

- `App.xaml.cs`'s `CreateWindow` is the single natural insertion point for Story 2's initial branch
  — add an "is app-lock PIN set" check ahead of the existing `activeProfileId is null` check, and
  hold a reference to the constructed `Window` to subscribe `Activated`/`Deactivated` for Story 3.
- New `IAppLockService` (or similarly named) in `Application.Abstractions`, implemented in `App`
  (needs `ISecureStorage`, already available there) — mirrors how `IActiveProfileStore` is an
  `Application`-defined interface with an `App`-side `Preferences`-backed implementation; app-lock's
  equivalent should be `SecureStorage`-backed instead, per Decision 3.
- `MauiProgram.cs` already registers `SecureStorage.Default` as a singleton (line 63) — the new
  service can take a constructor dependency on it exactly like `CloudAuthService` does.
- New Lock screen page/viewmodel registered in `MauiProgram.cs` alongside the existing
  `CreateFirstProfilePage`/`CreateFirstProfileViewModel` pair (same "constructed directly, not a
  Shell route" category).

## Fast-follow candidate: Slice 2 — Biometric unlock

Not scoped in detail here (deliberately deferred per Decision 1) — once Slice 1's PIN mechanism is
shipped and verified, a follow-up slice can add `BiometricPrompt`-backed unlock as a convenience
layered on top of an already-set PIN (PIN remains the fallback / the only way to *set up* lock in
the first place), gated behind the same 5-attempt-cooldown behavior on the biometric path too.
