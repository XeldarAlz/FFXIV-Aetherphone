# Localization

This doc explains how Aetherphone translates its UI into nine languages: where strings are declared, how the nine JSON catalogs stay in lockstep, how lookup works at runtime, and the copy rules every string follows (this is their canonical home). Read it before you add, change, or delete any text a player can see. Translators who do not write code should start with [Translating Aetherphone](translating.md).

## Key files

| Path | Why you open it |
| --- | --- |
| src/Aetherphone/Core/Localization/L.cs | Source of truth: every player-visible string, with its English text |
| src/Aetherphone/Core/Localization/Loc.cs | Runtime lookup, plurals, language switching, the active culture |
| src/Aetherphone/Core/Localization/LocString.cs | The `LocString` and `LocPlural` structs |
| src/Aetherphone/Core/Localization/Language.cs | The nine-language roster, plural rules, extra glyph ranges |
| src/Aetherphone/Core/Localization/StringCatalog.cs | Loads one language file into a key-to-string dictionary |
| src/Aetherphone/Core/Localization/TimeText.cs | Every clock, date, relative time, and duration format |
| src/Aetherphone/Core/Localization/NumberText.cs, CountText.cs | Digit grouping and short forms such as 12K |
| src/Aetherphone/Core/Localization/FailureText.cs | Network failures and server error codes mapped to player text |
| src/Aetherphone/Core/Localization/CatalogLabels.cs | Display labels for stored identifiers (themes, accents, cases) |
| src/Aetherphone/Localization/ | The nine language files |
| src/Aetherphone.Tests/LocalizationParityTests.cs | CI test: every catalog carries exactly the keys L.cs declares |
| src/Aetherphone.Tests/LocCompositeFormatTests.cs | CI test: every catalog value parses as a format string |

## Source of truth: L.cs

Every string a player can see is declared once in L.cs as a `LocString`: a readonly struct holding a dot-separated key and the English source text. Keys are grouped by nested static classes, and each group shares a key prefix: `common.*` in `L.Common`, `chirper.*` in `L.Chirper`. Groups can nest further: `L.Music.Live` holds the `music.live.*` keys.

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

English never comes from JSON at runtime. `Loc.T` falls back to the English source whenever the active catalog has no entry, and the English catalog is deliberately empty, so the C# declaration is the English string.

Three field shapes exist, and the CI parity test and the Debug audit understand all of them at any nesting depth:

- `LocString`: one key, one string.
- `LocPlural`: a key base that expands to `.one` and `.other` keys (see [Plurals and formatting](#plurals-and-formatting)).
- `LocString[]`: arrays of entries, used for the in-app changelog (`L.Changelog`) and the conduct rule lists (`L.Conduct`).

## The nine languages

The roster lives in `Languages` in Language.cs. Each language has a code, native and English names, a .NET culture, a plural rule, and optional extra font glyph ranges:

| Code | Language | Culture | Notes |
| --- | --- | --- | --- |
| en | English | en-US | Reference file; players see the L.cs text, never this file |
| de | German | de-DE | |
| fr | French | fr-FR | 0 and 1 are both singular |
| ja | Japanese | ja-JP | Extra glyph ranges for kana |
| es | Spanish | es-ES | |
| pt | Portuguese (Brazilian) | pt-BR | |
| ru | Russian | ru-RU | Extra glyph range for Cyrillic |
| tr | Turkish | tr-TR | |
| zh | Chinese (Simplified) | zh-CN | Ideographs come from scanning the catalog |

Each file sits in src/Aetherphone/Localization/ under its code (de.json, for example): a flat object, one `"group.key": "value"` pair per line, UTF-8 without a BOM, two-space indent. The build copies the folder next to the plugin binary, and `Plugin.InitializeLocalization` points `Loc` at it.

en.json is the reference copy that translators and reviewers read, and it must stay in lockstep with L.cs like every other file. CI checks its key set; nothing checks that its values match L.cs. When they disagree, L.cs wins: players see the L.cs text, and the fix is to bring en.json back in line.

## The sync rule

This is the rule the whole pipeline rests on:

**Every new, renamed, or deleted key changes L.cs plus all nine JSON files in the same commit.**

All nine files carry the same keys in the same order, so a key sits on the same line in every file. Keep it that way: add a new key at the same position in all nine. CI checks the key set, not the order, so the order is on you.

Safety nets catch most slips, and none of them replaces the rule:

- **CI key parity.** `LocalizationParityTests` fails when any file lacks a key L.cs declares or carries one it does not, and names the file and the keys.
- **CI format check.** `LocCompositeFormatTests` fails when any value does not parse as a .NET composite format string, such as a stray `{` or `}`.
- **Debug audit.** Debug builds log at startup, per non-English file, a "complete" line or the missing keys (`LocAudit`).
- **Runtime fallback.** A missing key shows the English source, not an error.

Both CI tests run on every push and pull request to `dev` and `master`. None of the nets checks that a value is translated, that en.json matches L.cs, or that a placeholder index stays within what the call site passes.

### Writing the other eight languages

When you add a key, put a real translation into each of the eight non-English files in the same commit. The project's non-English text is translated with AI assistance and reviewed by people (see [AI usage](ai-usage.md)), so a machine draft is a fine start, as long as you check each value:

- Every placeholder from the English source is present and written the same way.
- A plural has both forms, and the Russian form follows the pattern in [Plurals and formatting](#plurals-and-formatting).
- Names from [Names that never change](#names-that-never-change) are untouched, and a term the file already translates (an app name, "post", "Settings") uses the same word. Search the file for it.
- The value is about as long as the English one.

If you cannot produce a usable draft for a language, put the English source in that file and say so in the pull request. It passes CI and ships as English until a translator fixes it.

### Renaming or retiring a key

Renaming a key is a delete plus an add: change the key in L.cs, change it on the same line in all nine files, and keep each file's translated value unless the meaning changed. Retiring a key removes the L.cs field and the line in all nine files. Never reuse a retired key for different text, or a stale translation can come back with the new meaning.

## Runtime lookup

`Loc` is a static class holding the active language, its `CultureInfo`, and the loaded catalog:

```csharp
var label = Loc.T(L.Common.Cancel);
var counted = Loc.T(L.Common.PhotoLimit, maximumPhotos);
var plural = Loc.Plural(L.Chirper.Posts, postCount);
```

Formatting uses the active culture, and the generic overloads of `Loc.T` cache the parsed template so a per-frame call does not reparse it. `Loc.Upper` uppercases with the culture's rules (the Turkish dotted I, for example).

Aetherphone draws with Dear ImGui, an immediate-mode UI library that redraws every widget every frame, so almost every call site invokes `Loc.T` inside a `Draw` method. The lookup is one dictionary hit, and a language switch shows on the next frame with no rebuild of UI objects.

If you cache resolved text for performance, key the cache on `Loc.Current` (and on `TimeText.FormatVersion` for clock text) and rebuild it when either changes. `FailureSlot` (src/Aetherphone/Core/Localization/FailureText.cs) is the small reference for the language key. A cache that ignores them keeps the previous language or clock format after a switch.

### Language selection and switching

- First boot: `Plugin.DetectLanguage` maps the game client's language (the Chinese client to zh, then German, French, and Japanese to theirs), then tries the Windows UI language, then falls back to English. The result is saved in `Configuration.Language`.
- Manual switch: the Settings language page saves the code, calls `Loc.SetLanguage`, rebuilds the font atlas through `Plugin.Fonts.OnLanguageChanged()`, then calls `Plugin.OnLanguageChanged()`. That last call re-applies the clock preference, re-resolves the help text of the primary chat command (the alias is hidden from Dalamud's command help, so it has no text), and refreshes the server info bar entry.
- When translation is available, the same page has a "Translate into" section: the language other players' posts and messages are machine translated into. "Same as phone language" stores an empty value.

## LocString versus resolved strings

Long-lived objects store `LocString`, never the result of `Loc.T`. `Loc.T` resolves against the language active at the moment of the call, so a string resolved in a constructor freezes: after a language switch nothing re-runs the constructor, and the text stays in the previous language. Constructors and records take a `LocString` and translate at draw time, the way the Control Center toggle tile (src/Aetherphone/Core/ControlCenter/Modules/ToggleModule.cs) calls `Loc.T(label)` inside `Draw`. The shape, reduced to its essentials:

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

`LocString` is a two-field readonly struct, so passing it around costs nothing. If you must cache a resolved string (Dalamud registers command help text once, for example), re-resolve it in `Plugin.OnLanguageChanged`, as src/Aetherphone/Plugin.cs does for `L.Plugin.CommandHelp`.

## Plurals and formatting

Count-dependent strings use `LocPlural`, a key base plus English templates for the singular and the plural:

```csharp
public static readonly LocPlural Posts = new("chirper.posts", "{0} post", "{0} posts");
```

In every JSON file the base expands to two keys, `chirper.posts.one` and `chirper.posts.other`. `Loc.Plural(entry, count)` picks `.one` when the count is exactly 1 (0 or 1 in French) and `.other` otherwise. Only these two forms exist; nothing reads `.few` or `.many`. Japanese and Chinese usually repeat the same text in both.

Russian needs a pattern, because its nouns take three forms (1, 2 to 4, 5 and up) and 21 counts as singular, while the catalog has two forms and `.one` means exactly 1. Write the Russian value as a label followed by the number, so the noun never agrees with the count. From ru.json:

```json
"music.live.newMessages.one": "Новых сообщений: {0}",
"music.live.newMessages.other": "Новых сообщений: {0}",
```

Placeholders are positional, `{0}`, `{1}`, and survive translation unchanged. Some carry a format after a colon, such as `{0:N0}` (grouped digits) or `{1:00}` (two-digit padding); a translation copies the whole placeholder.

Never hand-build a date or time. TimeText.cs covers clocks, past and future moments, and durations, all through the active culture; the "Verify clock format seam" CI guard rejects an `"HH:mm"`-style literal anywhere else. The 12 or 24 hour choice follows the language until the player sets it, so switching language can flip the clock. Integers go through `NumberText` or `CountText` for grouped digits and short forms.

When a stored identifier (a theme name, an accent, a phone case) needs a display label, never localize the identifier itself. Map it in CatalogLabels.cs so the stored value stays the same in every language.

### Server error text

Players never see raw server error text. `FailureText.Resolve` maps each failure kind (offline, timeout, signed out) and each known server error code to an `L.Failure` string, and an unknown code to text for its HTTP status, with a reference id the player can quote when nothing more specific fits. A new error code needs a constant in `FailureCodes`, an `L.Failure` entry, and a case in `FailureText.FromServer`. Screens that redraw an error every frame hold a `FailureSlot`.

## Fonts and glyph coverage

Switching language can need glyphs the font atlas does not hold, so the language page rebuilds fonts right after `Loc.SetLanguage`. The atlas bakes each language's extra ranges from Language.cs, a shared set of common ranges, every character found in the active language file, and characters learned from player text at runtime. The atlas pipeline is in [Assets and media](assets-and-media.md).

## Copy rules

These apply to every string in L.cs and in all nine files, to the in-app changelog, and to player-facing text in docs:

- **No em dashes, anywhere.** Not in the English source, not in any translation. Use a comma, a colon, or parentheses. The "Verify no em dashes" CI guard fails any file that contains one. The Chinese dash (two of them) counts too.
- **Use the ellipsis character**, never three periods: `common.loading` is "Loading…".
- **Speak to the player in plain second person**: "You can add up to {0} photos". Describe what a feature does, never what it imitates, and never promise absolute privacy.
- **Use in-app names**, the names players see on screen: Chirper, Linkpearl, Activity, MogCast, never internal type or folder names. In a translation, use that language's value of the matching `app.*` key.
- **Use the game's official terms** for jobs, places, items, and duties, as the game client shows them in that language.
- **Name games for what they do**: Sweeper, Pairs, Gem Swap. No lore prefixes.
- **Keep it short.** The phone is a fixed-width screen; text much longer than the English wraps onto extra lines or clips inside buttons and chips.
- **Be consistent.** One term, one translation, in every string of a file.
- **Keep every placeholder** from the source, in whatever order the language needs.
- **Describe Aethernet to players as the phone's social network**, not as a server or backend.

Changelog bullets (the `L.Changelog` arrays, keyed `changelog.r<version digits>.N`) add their own rules:

- One idea per bullet. Split "fixed X and added Y" into two bullets.
- Open every bullet with its verb, and order a section's bullets Added, Overhauled, Changed, Fixed.
- A follow-up to a feature that is new in the same unreleased version folds into that feature's Added bullet; Changed and Fixed describe only what shipped before.
- Credit an outside contributor with a trailing clause: ", contributed by" and their GitHub name.

Where entries live and how versions map to keys is in [Testing and release](testing-and-release.md).

## Names that never change

- **Aetherphone** stays in Latin script in every language, including Japanese, Chinese, and Russian.
- **Linkpearl** (the in-game chat app, key `app.linkpearl`) is never translated: its value is "Linkpearl" in all nine files.
- **Velvet** and **Muster** also keep their English names in all nine files.

App names in general are not exempt: Japanese and Chinese transliterate Chirper (チャーパー, 叽叽) and Aethergram (エーテルグラム, 以太图集). The names above are specific decisions, not a blanket rule.

## What is not in the language files

Some text a player sees never passes through L.cs or the nine files. Do not add keys for it:

- **Server-delivered copy.** Badges, frames, polls, announcements, and coin shop items arrive with their own translations; the client picks the one matching `Loc.Current.Code`, or the base text. That copy is written outside this repository.
- **Game data.** Item, place, and duty names from the game's sheets come in the game client's language.
- **Other players' content.** Posts and messages show as written, with a Translate link.
- **Compact-number suffixes.** The K and M of short numbers are Latin in every language.

## Worked example: adding one string

Suppose the Photos viewer needs a "Copy Image" button.

1. Declare the constant in the Photos group of L.cs:

```csharp
public static readonly LocString CopyImage = new("photos.copyImage", "Copy Image");
```

2. Add the key to all nine files in src/Aetherphone/Localization/, at the same position among the `photos.*` keys in each. en.json gets the English text, and the other eight get a checked translation (see [Writing the other eight languages](#writing-the-other-eight-languages)). For example, in en.json, de.json, and zh.json:

```json
"photos.copyImage": "Copy Image",
"photos.copyImage": "Bild kopieren",
"photos.copyImage": "复制图片",
```

3. Use it at the call site, resolving per frame inside `Draw`:

```csharp
if (TextButton.Draw(buttonCenter, Loc.T(L.Photos.CopyImage), accentColor, scale))
{
    CopyImageToClipboard();
}
```

If the string goes into a long-lived object, pass `L.Photos.CopyImage` itself and resolve it in that object's `Draw`.

4. Run `dotnet test Aetherphone.sln`. The parity test names any file and key out of step, and the format test fails on a stray brace. CI runs both on your pull request.

5. If the string carries a count, declare a `LocPlural` instead and add both `.one` and `.other` to all nine files.

## Gotchas

- **`Loc.T` in a constructor freezes the value.** This is the most common localization review comment.
- **Editing en.json changes nothing on screen.** English resolves from L.cs; Loc reads en.json only to scan its characters for the font atlas.
- **One malformed JSON file mutes the whole language.** `StringCatalog.Load` logs the parse error and returns an empty catalog, so every string in that language falls back to English.
- **A placeholder beyond what the call site passes throws at draw time.** A translation that uses `{1}` where the code supplies one argument throws on every frame, and the shell replaces the app's screen with its "This app hit a problem" card. CI does not catch it.
- **Keys are case-sensitive.** `Common.Cancel` and `common.cancel` are different keys.
- **Unknown language codes resolve to English.** `Languages.Resolve` never throws, so a bad saved value degrades quietly.

## Related docs

- [Translating Aetherphone](translating.md): the translator side, editing one language file in the browser
- [UI toolkit](ui-toolkit.md): the widgets and typography that draw these strings every frame
- [Assets and media](assets-and-media.md): fonts, glyph ranges, and the atlas rebuild
- [Testing and release](testing-and-release.md): the test run, the CI guards, and the changelog entries
- [AI usage](ai-usage.md): how the non-English languages are produced and reviewed
