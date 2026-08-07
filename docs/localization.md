# Localization

This doc explains how Aetherphone translates its UI into nine languages: where strings are declared, how the nine JSON catalogs stay in lockstep, how lookup works at runtime, and the copy rules you must follow. Read it before you add, change, or delete any user-facing string. Everything here is client-side; the Aethernet backend lives in a separate repo and returns data, not UI copy.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Localization/L.cs | Source of truth: every user-facing string as a typed constant with its English text |
| src/Aetherphone/Core/Localization/Loc.cs | Runtime lookup (`Loc.T`, `Loc.Plural`), language switching, active culture |
| src/Aetherphone/Core/Localization/LocString.cs | The `LocString` and `LocPlural` structs |
| src/Aetherphone/Core/Localization/StringCatalog.cs | Loads one language JSON into a flat key-to-string dictionary |
| src/Aetherphone/Core/Localization/Language.cs | `LanguageInfo`, the `Languages.All` roster, plural kinds, per-language glyph ranges |
| src/Aetherphone/Core/Localization/LocAudit.cs | Debug-build startup audit that reports keys missing from each non-English JSON |
| src/Aetherphone/Core/Localization/TimeText.cs | Culture-aware clock, date, and relative-time formatting |
| src/Aetherphone/Core/Localization/CatalogLabels.cs | Maps stored identifiers (theme, accent, case names) to localized labels |
| src/Aetherphone/Localization/ | The nine language JSON files |

## Source of truth: L.cs

Every string a player can see is declared once in src/Aetherphone/Core/Localization/L.cs as a `LocString`: a readonly struct holding a dot-separated key and the English source text (src/Aetherphone/Core/Localization/LocString.cs).

Keys are grouped by nested static classes, and each group shares a key prefix (`common.*` in `L.Common`, `app.*` in `L.Apps`, `chirper.*` in `L.Chirper`):

```csharp
internal static class L
{
    internal static class Common
    {
        public static readonly LocString Cancel = new("common.cancel", "Cancel");

        public static readonly LocString PhotoLimit =
            new("common.photoLimit", "You can add up to {0} photos");
    }
}
```

English never comes from JSON at runtime. `Loc.T` falls back to `LocString.Source` whenever the active catalog has no entry for the key, and the English catalog is deliberately empty (see below), so the C# declaration is the English string.

Three field shapes exist, and the debug audit in src/Aetherphone/Core/Localization/LocAudit.cs understands all of them:

- `LocString`: one key, one string.
- `LocPlural`: a key base that expands to `.one` and `.other` keys (see Plurals below).
- `LocString[]`: arrays of entries, used for the in-app changelog in `L.Changelog` and the conduct-rules bullet lists in `L.Conduct`.

## The nine languages

The roster lives in the `Languages` class in src/Aetherphone/Core/Localization/Language.cs. Each language is a `LanguageInfo` with a code, native name, .NET culture, plural rule, and optional extra font glyph ranges:

| Code | File | Language | Culture | Notes |
| --- | --- | --- | --- | --- |
| en | src/Aetherphone/Localization/en.json | English | en-US | Reference file, not loaded at runtime |
| de | src/Aetherphone/Localization/de.json | German | de-DE | |
| fr | src/Aetherphone/Localization/fr.json | French | fr-FR | `PluralKind.French`: 0 and 1 are singular |
| ja | src/Aetherphone/Localization/ja.json | Japanese | ja-JP | Extra glyph ranges for kana, CJK punctuation, and fullwidth forms |
| es | src/Aetherphone/Localization/es.json | Spanish | es-ES | |
| pt | src/Aetherphone/Localization/pt.json | Portuguese (Brazilian) | pt-BR | Native name is "Português (Brasil)" |
| ru | src/Aetherphone/Localization/ru.json | Russian | ru-RU | Extra glyph range for Cyrillic |
| tr | src/Aetherphone/Localization/tr.json | Turkish | tr-TR | |
| zh | src/Aetherphone/Localization/zh.json | Chinese | zh-CN | Extra glyph ranges for CJK punctuation and fullwidth forms |

The JSON files are flat objects: one `"group.key": "value"` pair per line. `StringCatalog.Flatten` can walk nested objects, but the shipped files are flat and should stay that way. The files are copied next to the plugin binary by the `Localization\*.json` content entry in src/Aetherphone/Aetherphone.csproj, and `Plugin.InitializeLocalization` (src/Aetherphone/Plugin.cs) points `Loc` at that folder.

About en.json: `Loc.Apply` (src/Aetherphone/Core/Localization/Loc.cs) gives English `StringCatalog.Empty` instead of loading en.json, so every English string resolves through the `Source` field in L.cs. The file still exists as the reference copy that translators and reviewers diff against, and it must stay in lockstep with L.cs like every other file. Note that `LocAudit` skips en.json entirely, so nothing warns you when it drifts.

## The sync rule

This is the iron rule of the pipeline:

**Every new, renamed, or deleted key changes L.cs plus all nine JSON files in the same commit.**

All nine files carry exactly the same keys, and almost everywhere in the same order: the same key generally sits on the same line number in every file (for example `"common.loading"` is line 198 in en.json, de.json, and ja.json alike). One historical wrinkle breaks perfect alignment inside the `changelog.r0980.*` block, where en.json and pt.json slot `changelog.r0980.33` after `changelog.r0980.12` while the other seven keep numeric order. Keep the property everywhere else: when you add a key, add it at the same position in all nine files.

Two safety nets exist, and neither replaces the rule:

- In DEBUG builds, `LocAudit.Run` executes on startup (called from `Loc.Initialize`) and logs a warning for every non-English file with missing keys, listing the first 20 of them plus a count. English is skipped.
- At runtime, a missing key silently falls back to the English `Source`. Players on release builds see untranslated text, not an error.

## Runtime lookup

`Loc` (src/Aetherphone/Core/Localization/Loc.cs) is a static class holding the active `LanguageInfo`, its `CultureInfo`, and the loaded `StringCatalog`. The API surface is small:

```csharp
string label = Loc.T(L.Common.Cancel);
string counted = Loc.T(L.Common.PhotoLimit, maximumPhotos);
string plural = Loc.Plural(L.Chirper.Posts, postCount);
```

- `Loc.T(LocString)` looks the key up in the active catalog and falls back to `Source`.
- `Loc.T(LocString, params object[])` runs the resolved template through `string.Format` with the active culture, so number formatting follows the language.
- `Loc.Culture` is the active `CultureInfo`; date and number formatting should go through it, usually via `TimeText`.

Aetherphone draws with Dear ImGui, an immediate-mode UI library: nothing is retained between frames, every widget is re-drawn every frame. That is why almost all call sites invoke `Loc.T` inside a `Draw` method. The lookup is a single dictionary hit, cheap enough to run per frame, and it means a language switch takes effect on the very next frame with no rebuild of UI objects.

### Language selection and switching

- First boot: `Plugin.DetectLanguage` (src/Aetherphone/Plugin.cs) maps the FFXIV client language (German, French, Japanese) to a code, then tries the OS UI language against `Languages.All`, then falls back to English. The result persists in `Configuration.Language`.
- Manual switch: the Settings app's language page (src/Aetherphone/Apps/Settings/Pages/LanguagePage.cs) saves the new code, calls `Loc.SetLanguage`, then `Plugin.Fonts.OnLanguageChanged()` (font atlas rebuild, see below) and `Plugin.OnLanguageChanged()` (re-resolves the few strings that live outside the frame loop, such as chat command help messages).
- `Languages.Resolve` returns English for any unknown code, so a stale or corrupt config value cannot crash localization.

## LocString versus resolved strings

Long-lived objects must store `LocString`, never the result of `Loc.T`.

`Loc.T` resolves against whatever language is active at the moment of the call. If a constructor calls `Loc.T` and stores the resulting `string`, that value freezes at construction time: when the player later switches language in Settings, nothing re-runs the constructor, and the stored text stays in the old language forever.

The codebase-wide pattern is that constructors and data records accept `LocString` and translate at draw time. A real example, the Control Center toggle tile (src/Aetherphone/Core/ControlCenter/Modules/ToggleModule.cs):

```csharp
public ToggleModule(string id, FontAwesomeIcon icon, LocString label, Func<bool> isActive, Action onActivate)
{
    Id = id;
    this.icon = icon;
    this.label = label;
    this.isActive = isActive;
    this.onActivate = onActivate;
}

public void Draw(in ControlModuleContext context)
{
    if (ControlTile.Toggle(context.DrawList, context.Rect, icon, Loc.T(label), isActive(),
            context.Theme.Accent, context.Theme, context.Opacity, context.Interactive,
            context.Span != ControlSpan.Small))
    {
        onActivate();
    }
}
```

The same pattern appears in `NotificationChannel` (src/Aetherphone/Core/Notifications/NotificationChannels.cs), `GuideStep` (src/Aetherphone/Core/Onboarding/GuideStep.cs), and many other types. `LocString` is a two-field readonly struct, so passing it around costs nothing.

If you genuinely must cache a resolved string (Dalamud command help text is registered once with the game, for example), add the re-resolution to `Plugin.OnLanguageChanged`, as src/Aetherphone/Plugin.cs does for `L.Plugin.CommandHelp`.

## Plurals and formatting

Count-dependent strings use `LocPlural` (src/Aetherphone/Core/Localization/LocString.cs): a key base plus English templates for the singular and plural forms.

```csharp
public static readonly LocPlural Posts = new("chirper.posts", "{0} post", "{0} posts");
```

In every JSON the base expands to two keys:

```json
"chirper.posts.one": "{0} post",
"chirper.posts.other": "{0} posts",
```

`Loc.Plural(entry, count)` picks the form and formats the count in. The choice honors `LanguageInfo.PluralKind`: `PluralKind.French` treats magnitudes 0 and 1 as singular, everything else is singular only at exactly 1. Only these two forms exist; languages with richer plural systems (Russian, for example) use the `.other` form for everything that is not singular.

Placeholders are positional `{0}`, `{1}` and must survive translation unchanged. Date and time strings never get hand-built: `TimeText` (src/Aetherphone/Core/Localization/TimeText.cs) provides `Clock`, `Ago`, `Short`, and `DayLabel`, all formatted through `Loc.Culture`; `Clock` also honors the player's 12/24-hour preference (`Configuration.Use24HourClock`).

When a stored identifier (a theme name, an accent color, a phone case) needs a display label, do not localize the identifier itself. Map it in src/Aetherphone/Core/Localization/CatalogLabels.cs so the stored value stays stable across languages.

## Fonts and glyph coverage

Switching language can require glyphs the current font atlas does not contain, so `LanguagePage` calls `FontService.OnLanguageChanged` (src/Aetherphone/Core/FontService.cs) right after `Loc.SetLanguage`. That method composes the new range set from a Latin base, the codepoints of every language's native name (so the language list itself always renders), and the language's `ExtraGlyphRanges` from src/Aetherphone/Core/Localization/Language.cs: Cyrillic for Russian, kana plus CJK punctuation and fullwidth forms for Japanese, CJK punctuation and fullwidth forms for Chinese. CJK ideographs are far too numerous to pre-bake; instead, text-drawing widgets report what they render through `Plugin.Fonts.NoticeText`, uncovered codepoints accumulate in a ledger, and the atlas rebuilds after a short debounce, merging glyphs from Dalamud's bundled Noto Sans CJK asset. Details of the atlas, weights, and rebuild mechanics are in [Assets and media](assets-and-media.md).

## Copy style rules

These apply to every string in L.cs and all nine JSON files:

- **No em dashes, anywhere.** Not in English source, not in any translation. Use commas, colons, or parentheses. L.cs and all nine JSONs currently contain zero em dashes; keep it that way.
- Use the ellipsis character `…`, never three periods. Example: `common.loading` is "Loading…".
- Speak to the player in plain second person: "You can add up to {0} photos".
- Refer to features by their in-app names: Chirper, Aethergram, Linkpearl, Yellow Pages, Control Center.
- Changelog entries (the `L.Changelog` arrays and their `changelog.*` keys) credit outside contributors by name in the string itself, as `changelog.r0990.72` does ("contributed by BluntEXE").
- Keep every `{0}`-style placeholder from the source in each translation, in whatever order the language needs.

## Names that never change

- **Aetherphone** stays in Latin script in every language, including Japanese, Chinese, and Russian.
- **Linkpearl** (the in-game chat app, `L.Apps.Linkpearl`, key `app.linkpearl`) is deliberately never translated. The key exists in all nine files and the value is "Linkpearl" in every one, including ja.json and zh.json. Keep it that way.
- **Velvet** and **Muster** likewise keep their English names in all nine files today.

App names as a category are not exempt: Japanese and Chinese transliterate Chirper (チャーパー, 叽叽) and Aethergram (エーテルグラム, 以太图集). The names above are specific decisions, not a blanket rule.

## Worked example: adding one string

Suppose the Photos app needs a "Copy link" action.

1. Declare the constant in the right group in src/Aetherphone/Core/Localization/L.cs:

```csharp
internal static class Common
{
    public static readonly LocString CopyLink = new("common.copyLink", "Copy link");
}
```

2. Add `"common.copyLink"` to all nine JSON files in src/Aetherphone/Localization/, at the same position among the other `common.*` keys in each file:

```json
"common.copyLink": "Copy link",
```

in en.json, and the translated value in de.json, fr.json, ja.json, es.json, pt.json, ru.json, tr.json, and zh.json.

3. Use it at the call site. Inside a `Draw` method, resolve per frame:

```csharp
if (TextButton.Draw(buttonCenter, Loc.T(L.Common.CopyLink), accentColor, scale))
{
    CopyLinkToClipboard();
}
```

This mirrors real call sites such as the retry button in src/Aetherphone/Apps/News/NewsApp.cs.

If the string goes into a long-lived object, pass `L.Common.CopyLink` itself as a `LocString` and resolve it in that object's `Draw`.

4. Build in Debug and load the plugin. Watch the Dalamud log: `LocAudit` prints a `[Loc] 'de.json' complete` line per language, or a warning listing the keys you forgot (the first 20, plus a count).

5. If the string carries a count, declare a `LocPlural` instead and add both `.one` and `.other` keys to all nine files.

## Gotchas

- **`Loc.T` in a constructor freezes the value.** The resolved string never updates on language switch because nothing reconstructs the object. Store the `LocString` and resolve in `Draw`. This is the most common localization review comment.
- **Editing en.json changes nothing visible.** English resolves from the `Source` field in L.cs; en.json is a shipped reference copy that `Loc.Apply` never loads. Change both together or they drift.
- **Missing keys fail silently in Release.** The fallback to English source means a forgotten translation ships without an error. Only Debug builds run `LocAudit`.
- **One malformed JSON file mutes the whole language.** `StringCatalog.Load` catches the parse exception, logs through `AepLog.Error`, and returns the empty catalog, so every string in that language falls back to English.
- **Placeholder mismatches throw at draw time.** `Loc.T(entry, args)` runs `string.Format`; a translation that references `{1}` when the call site supplies one argument throws a `FormatException` every frame. Placeholder indices in translations must stay within what the English source uses.
- **Keys are case-sensitive.** `StringCatalog` compares with `StringComparer.Ordinal`; `Common.Cancel` and `common.cancel` are different keys.
- **Only two plural forms exist.** `.one` and `.other`, with a special singular rule for French zero. Do not invent `.few` or `.many` keys; nothing reads them.
- **Unknown language codes resolve to English.** `Languages.Resolve` never throws, so a bad `Configuration.Language` value degrades gracefully instead of failing loudly.

## Related docs

- [UI toolkit](ui-toolkit.md): the widgets and typography that draw these strings every frame
- [Assets and media](assets-and-media.md): fonts, glyph ranges, and the atlas rebuild pipeline
- [State and persistence](state-and-persistence.md): where `Configuration.Language` and the clock preference live
- [Conventions](conventions.md): code style and the repo-wide copy rules
- [Testing and release](testing-and-release.md): the release flow that adds each version's `L.Changelog` entries
