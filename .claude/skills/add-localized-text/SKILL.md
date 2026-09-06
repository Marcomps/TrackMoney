---
name: add-localized-text
description: Add or change user-facing text in TrackTraceMoney the correct way — via AppResources.resx (Spanish, default) and AppResources.en.resx (English) resource files, never hardcoded in XAML or C#. Use whenever adding or editing any string a user will see.
---

# Localizing UI text in TrackTraceMoney

README §5 requires Spanish + English from the start, with no interface text hardcoded directly in views. This app has no resx files yet as of the initial scaffold — creating the first one is part of this skill, not a sign something's missing.

## Resource file layout

```
src/TrackTraceMoney.App/Resources/Strings/AppResources.resx      # Spanish — the neutral/default culture
src/TrackTraceMoney.App/Resources/Strings/AppResources.en.resx   # English
```

If this is the first localized string in the project, create both files (a `.resx` is just XML — a `<data>` element per key with a `<value>`) and set the neutral file's `Access Modifier` to Public in the generated designer (or add `<CustomToolNamespace>`/`<Generator>PublicResXFileCodeGenerator</Generator>` metadata in the csproj if the default generates internal) so `AppResources.<Key>` is usable across the App project.

## Adding a string

1. Add the key to **both** `AppResources.resx` and `AppResources.en.resx` in the same change — never add one without the other; a missing English entry silently falls back to the Spanish text at runtime, which hides the gap instead of failing loudly.
2. Reference it:
   - **XAML**: `{x:Static resources:AppResources.KeyName}` (with an `xmlns:resources` pointing at the `Resources.Strings` namespace).
   - **C# (ViewModel/code-behind)**: `AppResources.KeyName`.
3. Never put literal user-facing text directly in a `Text="..."`/`Label`/`string` in a View or ViewModel — that's the exact pattern README §5 rules out.

## Language switching

The user can change language from settings (README §5) — this is typically done by setting `CultureInfo.CurrentUICulture` (and `AppResources.Culture`) at app startup/on change, then re-rendering bound views. Check how/if this is already wired (`Grep "CurrentUICulture" src/TrackTraceMoney.App`) before assuming it needs to be built from scratch.

## Before finishing

Grep the files you touched for quoted literal strings in `Text=`, `Title=`, `Content=`, or string interpolations shown to the user, to catch anything that slipped through hardcoded instead of going through `AppResources`.
