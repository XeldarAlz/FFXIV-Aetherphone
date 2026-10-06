# Localization

This doc explains how Aetherphone translates its UI into nine languages: where strings are declared, how the nine JSON catalogs stay in lockstep, how lookup works at runtime, and the copy rules you must follow. Read it before you add, change, or delete any user-facing string. Everything here is client-side. One exception: badges, frames, polls, announcements, and coin shop items carry their own per-language text from the Aethernet backend, which is edited there and never in these catalogs (see [What is not in the language files](#what-is-not-in-the-language-files)).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Localization/L.cs | Source of truth: every user-facing string as a typed constant with its English text |
| src/Aetherphone/Core/Localization/Loc.cs | Runtime lookup (`Loc.T`, `Loc.Plural`, `Loc.Upper`), language switching, active culture |
| src/Aetherphone/Core/Localization/LocString.cs | The `LocString` and `LocPlural` structs |
| src/Aetherphone/Core/Localization/StringCatalog.cs | Loads one language JSON into a flat key-to-string dictionary, and scans a file's characters for the font atlas |
| src/Aetherphone/Core/Localization/Language.cs | `LanguageInfo`, the `Languages.All` roster, plural kinds, per-language glyph ranges |
| src/Aetherphone/Core/Localization/GlyphPlan.cs | Composes the native and shared glyph range sets the font atlas bakes |
| src/Aetherphone/Core/Localization/LocAudit.cs | Debug-build startup log that reports keys missing from each non-English JSON |
| src/Aetherphone/Core/Localization/TimeText.cs | Culture-aware clock, date, relative-time, and duration formatting |
| src/Aetherphone/Core/Localization/NumberText.cs | Culture-aware digit grouping (`Group`) and K/M compaction (`Compact`) for `long` values |
| src/Aetherphone/Core/Localization/CountText.cs | The same two jobs for `int` counts (`Exact`, `Compact`) |
| src/Aetherphone/Core/Localization/FailureText.cs | Maps network failures and server error codes to `L.Failure` strings; `FailureSlot` caches the result per language |
| src/Aetherphone/Core/Localization/CatalogLabels.cs | Maps stored identifiers (theme, accent, case names) to localized labels |
| src/Aetherphone/Core/Localization/SpokenLanguages.cs | Profile language flags (the languages a player says they speak, used by Velvet), a wider list than the nine UI languages |
| src/Aetherphone/Localization/ | The nine language JSON files |
| src/Aetherphone.Tests/LocalizationParityTests.cs | CI test: every catalog carries exactly the keys L.cs declares |
| src/Aetherphone.Tests/LocCompositeFormatTests.cs | CI test: every catalog value parses as a format string |

## Source of truth: L.cs

Every string a player can see is declared once in src/Aetherphone/Core/Localization/L.cs as a `LocString`: a readonly struct holding a dot-separated key and the English source text (src/Aetherphone/Core/Localization/LocString.cs).

Keys are grouped by nested static classes, and each group shares a key prefix (`common.*` in `L.Common`, `app.*` in `L.Apps`, `chirper.*` in `L.Chirper`). Groups can nest further: `L.Music.Live` holds the `music.live.*` keys.

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

Three field shapes exist, and both the CI parity test and the debug audit understand all of them, at any nesting depth:

- `LocString`: one key, one string.
- `LocPlural`: a key base that expands to `.one` and `.other` keys (see Plurals below).
- `LocString[]`: arrays of entries, used for the in-app changelog in `L.Changelog` and the conduct-rules bullet lists in `L.Conduct`.

## The nine languages

The roster lives in the `Languages` class in src/Aetherphone/Core/Localization/Language.cs. Each language is a `LanguageInfo` with a code, native name, English name, .NET culture, plural rule, and optional extra font glyph ranges:

| Code | File | Language | Culture | Notes |
| --- | --- | --- | --- | --- |
| en | src/Aetherphone/Localization/en.json | English | en-US | Reference file; strings come from L.cs, never from this file |
| de | src/Aetherphone/Localization/de.json | German | de-DE | |
| fr | src/Aetherphone/Localization/fr.json | French | fr-FR | `PluralKind.French`: 0 and 1 are singular |
| ja | src/Aetherphone/Localization/ja.json | Japanese | ja-JP | Extra glyph ranges for hiragana, katakana, and katakana phonetic extensions |
| es | src/Aetherphone/Localization/es.json | Spanish | es-ES | |
| pt | src/Aetherphone/Localization/pt.json | Portuguese (Brazilian) | pt-BR | Native name is "Português (Brasil)" |
| ru | src/Aetherphone/Localization/ru.json | Russian | ru-RU | Extra glyph range for Cyrillic |
| tr | src/Aetherphone/Localization/tr.json | Turkish | tr-TR | |
| zh | src/Aetherphone/Localization/zh.json | Chinese | zh-CN | No extra ranges: CJK punctuation and fullwidth forms come from the shared set, ideographs from the catalog scan (see Fonts) |

The JSON files are flat objects: one `"group.key": "value"` pair per line. `StringCatalog.Flatten` can walk nested objects, but the shipped files are flat and should stay that way. The files are copied next to the plugin binary by the `Localization\*.json` content entry in src/Aetherphone/Aetherphone.csproj, and `Plugin.InitializeLocalization` (src/Aetherphone/Plugin.cs) points `Loc` at that folder.

About en.json: English strings never come from it. `Loc.Apply` (src/Aetherphone/Core/Localization/Loc.cs) gives English `StringCatalog.Empty` as its catalog, so every English string resolves through the `Source` field in L.cs. `Loc.Apply` still reads the active language file, en.json included, but only to scan its characters for the font atlas (`StringCatalog.ScanGlyphs`, see Fonts). The file is the reference copy that translators and reviewers diff against, and it must stay in lockstep with L.cs like every other file. CI checks its key set; nothing checks that its values match the L.cs `Source` text. When the two disagree, L.cs is authoritative: players see the L.cs text, and the fix is to bring en.json back in line, never the reverse.

## The sync rule

This is the iron rule of the pipeline:

**Every new, renamed, or deleted key changes L.cs plus all nine JSON files in the same commit.**

All nine files carry exactly the same keys in exactly the same order, so the same key sits on the same line in every file. Keep that property: when you add a key, add it at the same position in all nine files. CI checks the key set, not the order, so keeping the order is on you.

Several safety nets exist, and none of them replaces the rule:

- **CI key parity.** `LocalizationParityTests` (src/Aetherphone.Tests) fails when any of the nine files, en.json included, lacks a key that L.cs declares or carries a key that L.cs does not declare. It walks every nested group, and the failure message names the file and the keys (the first 20, plus a count).
- **CI format check.** `LocCompositeFormatTests` fails when any value in any catalog does not parse as a .NET composite format string, for example a stray `{` or `}`, even in a string that is never formatted.
- **Debug audit.** In DEBUG builds, `LocAudit.Run` executes on startup (called from `Loc.Initialize`) and logs, for every non-English file, either `[Loc] 'xx.json' complete (N keys).` or a warning listing the first 20 missing keys plus a count. It recurses into nested groups such as `L.Music.*`. English is skipped, and keys that L.cs no longer declares are not reported; the CI test covers both.
- **Runtime fallback.** A missing key silently falls back to the English `Source`. Players on release builds see untranslated text, not an error.

Both CI tests run in the "Run tests" step of the "Build (Windows)" job in .github/workflows/ci.yml, on every push and pull request to `dev` and `master`, so a catalog that is out of step cannot merge through a green pull request. None of these nets checks that a value is actually translated, that en.json values match L.cs, or that a placeholder index stays within what the call site passes.

## Runtime lookup

`Loc` (src/Aetherphone/Core/Localization/Loc.cs) is a static class holding the active `LanguageInfo`, its `CultureInfo`, and the loaded `StringCatalog`. The API surface is small:

```csharp
var label = Loc.T(L.Common.Cancel);
var counted = Loc.T(L.Common.PhotoLimit, maximumPhotos);
var plural = Loc.Plural(L.Chirper.Posts, postCount);
```

- `Loc.T(LocString)` looks the key up in the active catalog and falls back to `Source`.
- `Loc.T(entry, arg0)` up to `Loc.T(entry, arg0, arg1, arg2)` are generic overloads. They parse the resolved template once into a cached `CompositeFormat` and format it with the active culture without boxing the arguments. The cache is cleared on every language switch.
- `Loc.T(LocString, params object[])` handles four or more arguments through plain `string.Format` with the active culture.
- `Loc.Plural(LocPlural, int)` picks the plural form and formats the count through the same cached path.
- `Loc.Upper(string)` uppercases with the active culture's rules (Turkish dotted I, for example) and caches the result until the next language switch. Use it for fixed labels.
- `Loc.Current` is the active `LanguageInfo`, `Loc.Culture` its `CultureInfo`, and `Loc.CatalogGlyphs` the characters found in the active language file.
- Dates and times go through `TimeText`, and integers through `NumberText` or `CountText`, so digit grouping and separators follow the language.

Aetherphone draws with Dear ImGui, an immediate-mode UI library: nothing is retained between frames, every widget is re-drawn every frame. That is why almost all call sites invoke `Loc.T` inside a `Draw` method. The lookup is a single dictionary hit, cheap enough to run per frame, and it means a language switch takes effect on the very next frame with no rebuild of UI objects.

If you cache resolved text for performance, key the cache on `Loc.Current` (and on `TimeText.FormatVersion` for clock text) and rebuild it when either changes. `FailureSlot` (src/Aetherphone/Core/Localization/FailureText.cs), `NumberText.Group`, and `TimeText` itself follow this pattern. A cache that ignores them keeps showing the old language or the old clock format after a switch.

### Language selection and switching

- First boot: `Plugin.DetectLanguage` (src/Aetherphone/Plugin.cs) maps the FFXIV client language to a code: the Chinese Simplified client (matched via `GameData.ChineseSimplifiedClientLanguage`) maps to `zh` first, then German, French, and Japanese map to theirs. Failing that, it tries the OS UI language against `Languages.All`, then falls back to English. The result persists in `Configuration.Language`.
- Manual switch: the Settings app's language page (src/Aetherphone/Apps/Settings/Pages/LanguagePage.cs) saves the new code, calls `Loc.SetLanguage`, then `Plugin.Fonts.OnLanguageChanged()` (font atlas rebuild, see below) and `Plugin.OnLanguageChanged()`. The latter re-applies the clock preference (an unset preference follows the new language's convention), re-resolves the chat command help text for the main command and its alias, and refreshes the server info bar entry.
- The same page has a "Translate into" section, shown when machine translation is available. It picks the language that other players' posts, comments, and messages are translated into, stored in `Configuration.TranslationTargetLanguage`. Empty means the phone language (`TranslationService.TargetLanguage`).
- `Languages.Resolve` returns English for any unknown code, so a stale or corrupt config value cannot crash localization.

## LocString versus resolved strings

Long-lived objects must store `LocString`, never the result of `Loc.T`.

`Loc.T` resolves against whatever language is active at the moment of the call. If a constructor calls `Loc.T` and stores the resulting `string`, that value freezes at construction time: when the player later switches language in Settings, nothing re-runs the constructor, and the stored text stays in the old language forever.

The codebase-wide pattern is that constructors and data records accept `LocString` and translate at draw time. The Control Center toggle tile (src/Aetherphone/Core/ControlCenter/Modules/ToggleModule.cs) stores `private readonly LocString label;` and passes `Loc.T(label)` to `ControlTile.Toggle` inside `Draw`. The shape, reduced to its essentials:

```csharp
internal sealed class RetryPrompt
{
    private readonly LocString label;

    public RetryPrompt(LocString label)
    {
        this.label = label;
    }

    public bool Draw(Vector2 center, Vector4 accentColor, float scale)
    {
        return TextButton.Draw(center, Loc.T(label), accentColor, scale);
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

Placeholders are positional `{0}`, `{1}` and must survive translation unchanged. Some carry a format specifier after a colon, such as `{0:N0}` (grouped digits) or `{1:00}` (two-digit padding); translations copy the whole placeholder.

Date and time strings never get hand-built. `TimeText` (src/Aetherphone/Core/Localization/TimeText.cs) covers clocks (`Clock`, `HourClock`, `HourLabel`, `MinuteLabel`, `MeridiemLabel`), past moments (`Ago`, `AgoPrecise`, `Short`, `DayLabel`, `MonthDay`, `Stamp`), future moments (`FutureDayLabel`, `FutureMoment`, `Until`), and spans (`Duration`, `MinutesSeconds`), all formatted through `Loc.Culture`. The 12/24-hour choice is `Configuration.Use24HourClock`, a `bool?`: unset follows the active culture's short time pattern, so English defaults to 12-hour and switching language can flip the clock. The "Verify clock format seam" CI guard rejects any `"HH:mm"`-style literal outside TimeText.cs.

Integers go through `NumberText.Group` or `CountText.Exact` for grouped digits, and `NumberText.Compact` or `CountText.Compact` for short forms such as "12K".

When a stored identifier (a theme name, an accent color, a phone case) needs a display label, do not localize the identifier itself. Map it in src/Aetherphone/Core/Localization/CatalogLabels.cs so the stored value stays stable across languages.

### Server error text

Server errors never show raw server text. `FailureText.Resolve` (src/Aetherphone/Core/Localization/FailureText.cs) maps each `AepFailure` kind (offline, timeout, signed out, and so on) and each server error code in `FailureCodes` to an `L.Failure` string. An unknown code falls back to text for its HTTP status, with a reference id the player can quote. A new server error code needs a `FailureCodes` constant, an `L.Failure` entry, and a case in `FailureText.FromServer`. Screens that show an error every frame hold a `FailureSlot`, which caches the resolved text and re-resolves it after a language switch.

## Fonts and glyph coverage

Switching language can require glyphs the current font atlas does not contain, so `LanguagePage` calls `FontService.OnLanguageChanged` (src/Aetherphone/Core/FontService.cs) right after `Loc.SetLanguage`. Glyphs come from three layers:

1. **Native ranges**, composed by `GlyphPlan.Native` (src/Aetherphone/Core/Localization/GlyphPlan.cs): a Latin base, the characters of every UI language's native name and every `SpokenLanguages` name (so language lists always render), and the language's `ExtraGlyphRanges` from Language.cs: Cyrillic for Russian; hiragana, katakana, and katakana phonetic extensions for Japanese; nothing extra for the other seven.
2. **Shared ranges**, baked once in a shared font built from Dalamud's bundled Noto Sans CJK assets: `GlyphPlan.SharedBase` (Latin Extended-B, Greek, Cyrillic, CJK punctuation, kana, fullwidth forms, and a few symbol blocks), plus every character found in the active language file (`Loc.CatalogGlyphs`, filled by `StringCatalog.ScanGlyphs`). The catalog scan is how the UI's own Chinese and Japanese ideographs are covered.
3. **Learned glyphs** for text no catalog can predict, such as posts, messages, and input fields. Widgets report that text through `Plugin.Fonts.NoticeText`, uncovered characters join a capped ledger, the shared font rebuilds after a short debounce, and the ledger persists in `Configuration.FontGlyphCache`.

Details of the atlas, weights, and rebuild mechanics are in [Assets and media](assets-and-media.md).

## Copy style rules

These apply to every string in L.cs and all nine JSON files:

- **No em dashes, anywhere.** Not in English source, not in any translation. Use commas, colons, or parentheses. The "Verify no em dashes" CI guard fails any file that contains one.
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

## What is not in the language files

Some text a player sees never passes through L.cs or the nine JSONs. Do not add keys for it:

- **Backend-delivered copy.** Badges, frames, polls, announcements, coin shop items, and shop categories arrive from Aethernet with a `Translations` array (the DTOs in src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs and CoinDtos.cs). The client picks the entry whose language matches `Loc.Current.Code` (for example `AnnouncementText.For`, `BadgeStyle.Name`, `CoinSkuStyle.Name`) and shows the base text when there is none. That copy is written and translated on the backend.
- **Game data.** Item, place, duty, and other names read from the game's Excel sheets come in the game client's language, not the phone's. Hunts makes an exception for its zone and expansion labels: `HuntUiLanguage` reads those sheets in the phone language for German, French, and Japanese, and in English otherwise.
- **Other players' content.** Posts, comments, and messages show as written. When machine translation is available, a Translate link renders them in the "Translate into" language (`TranslationService`).
- **Compact-number suffixes.** The "K" and "M" from `NumberText.Compact` and `CountText.Compact` are Latin in every language.

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

This mirrors real call sites such as the Try again button in src/Aetherphone/Apps/Linkpearl/LinkpearlApp.Find.cs.

If the string goes into a long-lived object, pass `L.Common.CopyLink` itself as a `LocString` and resolve it in that object's `Draw`.

4. Run `dotnet test Aetherphone.sln`. `LocalizationParityTests` fails with the file and key names if any catalog is out of step, and `LocCompositeFormatTests` fails on a value with a stray brace. CI runs the same tests on your pull request. A Debug build also logs a `[Loc] 'de.json' complete (N keys).` line per language at startup, or a warning listing the keys you forgot (the first 20, plus a count).

5. If the string carries a count, declare a `LocPlural` instead and add both `.one` and `.other` keys to all nine files.

## Gotchas

- **`Loc.T` in a constructor freezes the value.** The resolved string never updates on language switch because nothing reconstructs the object. Store the `LocString` and resolve in `Draw`. This is the most common localization review comment.
- **Editing en.json changes nothing visible.** English strings resolve from the `Source` field in L.cs; `Loc.Apply` reads en.json only to scan its characters for the font atlas. Change both together or they drift.
- **Untranslated values fail silently.** CI guarantees every key exists in every file, not that its value is translated. An English value copied into another catalog passes every check and ships as English.
- **One malformed JSON file mutes the whole language.** `StringCatalog.Load` catches the parse exception, logs through `AepLog.Error`, and returns the empty catalog, so every string in that language falls back to English. The CI parity test also fails on it, since an empty catalog has no keys.
- **Placeholder mismatches throw at draw time.** A translation that references `{1}` when the call site supplies one argument throws a `FormatException` on every frame, and the shell replaces the app's screen with its "This app hit a problem" card. CI catches a value that does not parse, not an index beyond what the call site passes, so placeholder indices in translations must stay within what the English source uses.
- **Keys are case-sensitive.** `StringCatalog` compares with `StringComparer.Ordinal`; `Common.Cancel` and `common.cancel` are different keys.
- **Only two plural forms exist.** `.one` and `.other`, with a special singular rule for French zero. Do not invent `.few` or `.many` keys; nothing reads them.
- **Unknown language codes resolve to English.** `Languages.Resolve` never throws, so a bad `Configuration.Language` value degrades gracefully instead of failing loudly.

## Related docs

- [Translating Aetherphone](translating.md): the translator side, editing one language file in the browser
- [UI toolkit](ui-toolkit.md): the widgets and typography that draw these strings every frame
- [Assets and media](assets-and-media.md): fonts, glyph ranges, and the atlas rebuild pipeline
- [State and persistence](state-and-persistence.md): how `Configuration` (which holds `Language`, `TranslationTargetLanguage`, and `Use24HourClock`) loads and saves
- [Conventions](conventions.md): code style and the repo-wide copy rules
- [Testing and release](testing-and-release.md): the release flow that adds each version's `L.Changelog` entries
