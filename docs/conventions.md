# Conventions and code style

This page is the rulebook for code, copy, and commits in the Aetherphone client repo. Read it before your first PR, and skim it again whenever a review comment surprises you. It covers the client plugin only; the Aethernet backend lives in a separate repository with its own conventions. Everything below is written against the code as it exists today, and where the code and the ideal disagree, both are stated.

## Key files

| Path | Role |
| --- | --- |
| .editorconfig | Machine-enforced formatting: indentation, braces, namespaces, var |
| CONTRIBUTING.md | Build command, PR checklist, project layout |
| Directory.Build.props | Single source of the version number (CI checks it against repo.json) |
| src/Aetherphone/Windows/Components/TextStyles.cs | The typography ladder: every text size and weight in the UI |
| src/Aetherphone/Windows/Components/Typography.cs | Text drawing, measuring, wrapping, and fitting helpers |
| src/Aetherphone/Windows/Components/Metrics.cs | Spacing, radius, size, and stroke tokens |
| src/Aetherphone/Windows/Components/ChipRail.cs | The one approved way to show a row of filter chips |
| src/Aetherphone/Core/Animation/Spring.cs | The motion primitive: critically damped, cannot overshoot |
| src/Aetherphone/Core/Localization/L.cs | Source of truth for every user-visible string |
| src/Aetherphone/Core/Localization/TimeText.cs | The single seam for clock and time formatting |
| src/Aetherphone/Core/Localization/LocAudit.cs | Debug-build audit that reports missing translation keys |

## Philosophy

Three principles decide most reviews:

- **YAGNI** (you aren't gonna need it). Implement things when you actually need them, never because you foresee needing them. No configuration points nobody configures, no interfaces with one implementation "for later". CONTRIBUTING.md states it as: no heavy abstractions "for later".
- **DRY** (don't repeat yourself), via small shared utilities. When the same drawing or string logic appears twice, it moves into src/Aetherphone/Windows/Components/ or a small static helper like UiText.Truncate or TimeText.Ago, not into a base class.
- **Self-documenting code.** Names and structure carry the meaning. As of today the C# under src/Aetherphone contains zero comment lines; explanatory prose lives in build files and docs (Directory.Build.props and the CI workflows have comments, the code does not). The allowance, per CONTRIBUTING.md: a comment may explain *why* something is done when the code cannot, or the source of a magic constant. It never narrates *what* the code does.

## Naming

No abbreviations, anywhere, including loop variables.

| Write | Not |
| --- | --- |
| index, count, entryIndex, keyIndex | i, j, k, n, cnt |
| drawList | dl |
| minuteOfDay, hourOfDay | min, hr |
| conversationIndex, groupIndex | ci, gi |

Real examples: src/Aetherphone/Core/Localization/LocAudit.cs iterates with groupIndex, fieldIndex, and entryIndex; src/Aetherphone/Core/Localization/TimeText.cs takes hourOfDay and minuteOfHour. Locals named drawList outnumber the older dl several times over, and a few legacy single-letter loops survive in src/Aetherphone/Windows/Components/ProgressRing.cs. Do not add to the legacy side.

Other naming norms you can see throughout the tree:

- Types and members are PascalCase, locals and parameters are camelCase, standard C#.
- Bespoke drawing code for a specific app lives in a type named `*Renderer` (for example src/Aetherphone/Apps/Games/Chess/ChessRenderer.cs).
- Localization keys are dotted camelCase strings like `common.saveToGallery` (see src/Aetherphone/Core/Localization/L.cs).

## Formatting

The repo has an .editorconfig and editors plus `dotnet format` respect it. What it actually sets:

| Setting | Value |
| --- | --- |
| Encoding and endings | UTF-8, LF line endings, final newline, trailing whitespace trimmed (kept in .md) |
| Indentation | 4 spaces for code, 2 spaces for yml, yaml, and json |
| Namespaces | File-scoped (`namespace Aetherphone.Core.Apps;`), enforced at warning level |
| Braces | Opening brace on its own line, everywhere; `else`, `catch`, `finally` start a new line |
| var | Preferred everywhere (built-in types, apparent types, and elsewhere) |
| Modifier order | public, private, protected, internal, new, abstract, virtual, sealed, override, static, readonly, extern, unsafe, volatile, async |
| Unused parameters | Flagged on non-public methods |

House rules the config cannot express, all confirmed against src/Aetherphone/Core and src/Aetherphone/Windows/Components:

- **Attributes go on their own line** directly above what they decorate, never inline (`[Serializable]` above `internal sealed class Configuration` in src/Aetherphone/Configuration.cs).
- **Braces on every body**, even a single-line `if`, `else`, or loop body.
- **Early returns over nesting.** Guard clauses first, then the flat happy path. See Languages.Resolve in src/Aetherphone/Core/Localization/Language.cs.
- **Explicit accessibility keywords on everything**, including private members: `private static`, `private const`, never a bare `static`.
- **A blank line after a closing brace** before the next statement in the same block.

All of it together, in project style:

```csharp
internal static class UnreadTally
{
    private const int BadgeCap = 99;

    public static int Count(IReadOnlyList<ConversationSummary> conversations)
    {
        if (conversations.Count == 0)
        {
            return 0;
        }

        var total = 0;
        for (var conversationIndex = 0; conversationIndex < conversations.Count; conversationIndex++)
        {
            total += conversations[conversationIndex].UnreadCount;
        }

        return Math.Min(total, BadgeCap);
    }
}
```

## Types and data

The codebase leans data-oriented: plain data in flat structures, transformed by static helpers, over deep object-oriented hierarchies. Interfaces exist where a real seam exists (IPhoneApp in src/Aetherphone/Core/Apps/IPhoneApp.cs is the app contract), and almost nowhere else.

- **`sealed` liberally.** `internal sealed class` is the default class declaration; the tree has hundreds of them. Unsealed classes are the exception, not the rule.
- **`const` and `readonly` wherever possible.** Metrics is nothing but consts; TextStyles is nothing but `public static readonly` values of a `readonly record struct`.
- **Structs over classes for small data.** The tree has well over a hundred `readonly struct` and `readonly record struct` declarations (TextStyle in TextStyles.cs, WrapEntry inside Typography.cs).
- **`ref struct` where it fits.** A ref struct can only live on the stack, so it never allocates on the garbage-collected heap. Examples: InputShield in src/Aetherphone/Core/Animation/InputShield.cs, ChatSearchModel in src/Aetherphone/Windows/Components/ChatSearchController.cs, ChatTranscriptModel in src/Aetherphone/Windows/Components/ChatTranscript.cs.
- **Compact representations.** Glyph ranges are `ushort[]`, PluralKind is `enum : byte` (both in src/Aetherphone/Core/Localization/Language.cs). Pick the smallest type that holds the data.
- **Static data tables over runtime lookups.** App accents come from AppAccents.For(id) (src/Aetherphone/Core/Apps/AppAccents.cs); the changelog is a readonly array in src/Aetherphone/Core/Changelog/ChangelogData.cs.

## Performance

Aetherphone draws with Dear ImGui, an immediate mode UI library: nothing is retained between frames, the entire phone UI is rebuilt and redrawn every frame, up to your monitor's refresh rate, inside the game's render loop. Any code reachable from a `Draw` method is a hot path. That drives every rule here.

- **No LINQ in per-frame or hot paths.** LINQ extension methods (Where, Select, Any, First and friends) allocate iterators and delegates every call. In practice only a couple of LINQ call sites survive, in rarely-run event paths: a FirstOrDefault in an album context-menu handler (src/Aetherphone/Apps/Photos/PhotosApp.Grid.cs) and a Select in a favorites load (src/Aetherphone/Apps/Music/MusicApp.cs). Write a `for` loop instead.
- **`for` over `foreach` on indexable collections.** `for` with a named index avoids enumerator allocation on non-array collections and is the dominant pattern. `foreach` remains where there is no indexer, mostly dictionary and set iteration (src/Aetherphone/Apps/Calendar/CalendarEventMerger.cs).
- **Watch allocations in draw code.** Anything allocated per frame becomes garbage-collector pressure and eventually a visible stutter in-game. The pattern to copy: compute once, cache, invalidate on a real change. Typography.cs keeps FitCache and WrapCache and clears them only when the font atlas generation changes; ChipRail.Draw takes `ReadOnlySpan<string>` so callers can pass stack or pooled data without allocating.
- **Reflection only in rarely-executed paths.** Reflection is slow and allocation-heavy. The entire plugin uses it exactly once: LocAudit.CollectKeys, which is compiled only in debug builds and runs once at plugin boot.
- **Always await awaitables in async contexts.** There are zero `async void` methods in the tree. ImGui draw code cannot await (a frame cannot pause), so work is pushed off the frame with an explicit discard, `_ = Task.Run(...)`, and inside those async bodies every awaitable is awaited.

## UI conventions in brief

Full detail with examples lives in [UI toolkit](ui-toolkit.md); this is the checklist form.

- **All text goes through the typography ladder.** Pick a TextStyle from TextStyles (LargeTitle down to Caption2) and draw with the Typography helpers. Never hand-pick a font scale for a screen.
- **Metrics tokens over pixel literals.** Spacing, radii, and control sizes come from Metrics.Space, Metrics.Radius, Metrics.Size, and Metrics.Stroke. Every pixel value, token or not, is multiplied by `UiScale.Current` so the phone scales with both Dalamud's global UI scale and the user's chosen phone size. Never read `ImGuiHelpers.GlobalScale` directly: `UiScale.cs` is the only place allowed to, and CI fails the build on any other use.
- **Text wraps, it never overflows.** Use Typography.Wrapped, Typography.DrawWrappedLeft, or Typography.FitText. Clipped or overlapping text is a bug, always.
- **One pannable chip rail, never a chip wall.** A row of filter chips is a single horizontally draggable ChipRail. Chips never wrap to a second line.
- **Free input over preset chips.** When the user enters a value, let them enter any value. TimeOfDayField (src/Aetherphone/Windows/Components/TimeOfDayField.cs) steps hours and minutes across the whole day rather than offering a handful of preset times.
- **Critically damped motion, no bounce.** All UI motion runs through Spring (src/Aetherphone/Core/Animation/Spring.cs), whose Step clamps at the target so it cannot overshoot. Bouncy easing (Easing.EaseOutBack) is used only inside the mini-games under src/Aetherphone/Apps/Games/.
- **All clock text goes through the single clock seam.** TimeText.Clock (src/Aetherphone/Core/Localization/TimeText.cs) formats every clock string and honors the user's 12/24-hour preference via TimeText.Use24Hour. There are dozens of call sites and zero hand-rolled `"HH:mm"` format strings outside TimeText itself. Keep it that way.

## Copy rules

These apply to UI strings, docs, changelogs, and commit messages alike.

- **No em dashes, anywhere.** Not in UI copy, not in the nine locale JSONs, not in docs, not in changelog bullets. Use a comma, colon, or parentheses. The Discord announce workflow (.github/workflows/announce-commits.yml) even replaces stray dashes in commit subjects before posting. One legacy file, src/Aetherphone/Cases/_template/README.md, predates the rule; do not use it as a copy reference.
- **Changelog bullets carry one idea each.** The changelog ships inside the plugin: entries are LocString arrays in the Changelog section of src/Aetherphone/Core/Localization/L.cs, listed in src/Aetherphone/Core/Changelog/ChangelogData.cs, and translated in all nine locale JSONs.
- **Credit contributors in the changelog.** The pattern is a trailing clause: "..., contributed by Ehno". See the 0.9.9.5 entries in L.cs.
- **Use in-app names.** Copy refers to apps and features by their on-screen names (Photos, Jobs, Camera), not internal identifiers.
- **Name games for what they do.** Mini-game names describe the game: Sweeper, Pairs, Gem Swap (`games.*` keys in L.cs). No lore-flavored prefixes.

## Localization lockstep

Every new, renamed, or deleted user-visible string changes src/Aetherphone/Core/Localization/L.cs plus all nine JSON files in src/Aetherphone/Localization/ in the same commit. Full procedure: [Localization](localization.md).

## Git conventions

This repo uses conventional-commit style, confirmed in history: a type, a scope in parentheses, a colon, then a lowercase summary.

```
feat(account): link Patreon and wear the member badge automatically
fix(net): cap the rate-limit pause at 30 seconds
docs(changelog): add the 0.9.9.6 entry in all nine languages
chore(release): prepare 0.9.9.6
refactor(sounds): make all ringtones and notification sounds file-based
ci: stop pinging the role in commit announcements
```

- Types in active use: `feat`, `fix`, `docs`, `chore`, `refactor`, `ci`, plus the occasional `perf`, `style`, and `test`. Scope is the app or subsystem you touched (`velvet`, `settings`, `ui`, `net`, `release`).
- The summary is a lowercase sentence fragment, no trailing period, and describes the user-visible outcome, not the diff.
- **One concern per PR.** Keep the diff focused (CONTRIBUTING.md). A fix and a refactor are two PRs.
- **No AI attribution.** Do not add co-author trailers or generated-with footers to commits or PR bodies. Commit messages carry substantive content only.
- **Update the README when user-visible behavior changes**: commands, layout, settings (CONTRIBUTING.md). The README has eight translated siblings (README.fr.md and friends); update at least README.md.
- Release versioning is not part of a feature PR: the version lives in Directory.Build.props and CI fails if it drifts from repo.json (.github/workflows/ci.yml). See [Testing and release](testing-and-release.md).

## Gotchas

- **Typography.Draw without a draw list moves the ImGui cursor**; inside bespoke drawing, always pass the `ImDrawListPtr` overload. Full story: [UI toolkit](ui-toolkit.md).
- **Toggle.Draw returns the new value, not "was clicked"**; assign it back every frame instead of treating it as a click event. Full story: [UI toolkit](ui-toolkit.md).
- **Fixing English text only in en.json changes nothing in-game.** English resolves from the source strings in L.cs (Loc gives English an empty catalog in src/Aetherphone/Core/Localization/Loc.cs). Fix the text in L.cs and mirror it in en.json.
- **Text resolved at construction freezes its language**; store the LocString and call `Loc.T` in your Draw path. Full story: [Localization](localization.md).
- **Missing-translation warnings only exist in debug builds.** LocAudit is wrapped in `#if DEBUG` and runs once at boot. A clean Release build proves nothing about lockstep; watch the Dalamud log on a debug build after touching L.cs.

## Related docs

- [Getting started](getting-started.md): prerequisites, build, loading the dev plugin
- [UI toolkit](ui-toolkit.md): the Components library, typography, metrics, input handling
- [Localization](localization.md): L.cs, the nine catalogs, and copy in depth
- [Testing and release](testing-and-release.md): CI, versioning, and the changelog pipeline
- [Creating an app](creating-an-app.md): these rules applied end to end in a tutorial
