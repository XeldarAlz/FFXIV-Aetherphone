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
| docs/ai-usage.md | AI involvement level to declare in a PR, and asset provenance rules |

## Philosophy

Three principles decide most reviews:

- **YAGNI** (you aren't gonna need it). Implement things when you actually need them, never because you foresee needing them. No configuration points nobody configures, no interfaces with one implementation "for later". CONTRIBUTING.md states it as: no heavy abstractions "for later".
- **DRY** (don't repeat yourself), via small shared utilities. When the same drawing or string logic appears twice, it moves into src/Aetherphone/Windows/Components/ or a small static helper like UiText.Truncate or TimeText.Ago, not into a base class.
- **Self-documenting code.** Names and structure carry the meaning. The C# under src/Aetherphone is kept comment-free as a rule, though a handful of legacy comment lines survive, concentrated in the radio and media decoding code (src/Aetherphone/Core/Radio/RadioPlayer.cs and its neighbors). Explanatory prose lives in build files and docs; Directory.Build.props and the CI workflows carry comments freely. The allowance, per CONTRIBUTING.md: a comment may explain *why* something is done when the code cannot, or the source of a magic constant. It never narrates *what* the code does.

## Naming

No abbreviations, anywhere, including loop variables.

| Write | Not |
| --- | --- |
| index, count, entryIndex, keyIndex | i, j, k, n, cnt |
| drawList | dl |
| minuteOfDay, hourOfDay | min, hr |
| conversationIndex, groupIndex | ci, gi |

Real examples: src/Aetherphone/Core/Localization/LocAudit.cs iterates with groupIndex, fieldIndex, and entryIndex; src/Aetherphone/Core/Localization/TimeText.cs takes hourOfDay and minuteOfHour. Locals named drawList outnumber the older dl several times over, and a few legacy single-letter loops survive: src/Aetherphone/Windows/Components/ProgressRing.cs and src/Aetherphone/Core/Video/ScreenPainter.cs among them, plus x and y coordinate loops in a handful of renderers (SnakeRenderer.cs, WeatherSky.cs, ArtworkCache.cs). Do not add to the legacy side.

Other naming norms you can see throughout the tree:

- Types and members are PascalCase, locals and parameters are camelCase, standard C#.
- Bespoke drawing code for a specific app lives in a type named `*Renderer` (for example src/Aetherphone/Apps/Games/Chess/ChessRenderer.cs).
- Localization keys are dotted camelCase strings like `common.saveToGallery` (see src/Aetherphone/Core/Localization/L.cs).

## Formatting

The repo has an .editorconfig and editors plus `dotnet format` respect it. What it actually sets:

| Setting | Value |
| --- | --- |
| Encoding and endings | UTF-8, LF line endings, final newline, trailing whitespace trimmed (kept in .md) |
| Indentation | 4 spaces for code, 2 spaces for yml and yaml. On json the config contradicts itself: one section sets 2, a trailing catch-all re-matches json at 4, and the last match wins. The repo's JSON files are 2-space in practice |
| Namespaces | File-scoped (`namespace Aetherphone.Core.Apps;`), enforced at warning level |
| Braces | Opening brace on its own line, everywhere; `else`, `catch`, `finally` start a new line |
| var | Preferred everywhere (built-in types, apparent types, and elsewhere) |
| Modifier order | public, private, protected, internal, new, abstract, virtual, sealed, override, static, readonly, extern, unsafe, volatile, async |
| Unused parameters | Flagged on non-public methods |

House rules the config cannot express, all confirmed against src/Aetherphone/Core and src/Aetherphone/Windows/Components:

- **Attributes go on their own line** directly above the type, method, or multi-line member they decorate (`[Serializable]` above `internal sealed class Configuration` in src/Aetherphone/Configuration.cs). There are zero inline attributes on a type or a method in the tree. The exception, and it is the dominant style rather than a lapse, is a single short marker on a one-line auto-property or field: `[JsonPropertyName("id")] public uint Id { get; set; }` through the DTO model files, and `[PluginService]` through src/Aetherphone/Plugin.cs. Splitting those doubles the length of a serialization contract for nothing, so leave them inline.
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
- **Structs over classes for small data.** The tree has well over a hundred `readonly struct` and `readonly record struct` declarations (TextStyle in TextStyles.cs, SweepTier inside Typography.cs).
- **`ref struct` where it fits.** A ref struct can only live on the stack, so it never allocates on the garbage-collected heap. Examples: InputShield in src/Aetherphone/Core/Animation/InputShield.cs, ChatSearchModel in src/Aetherphone/Windows/Components/ChatSearchController.cs, ChatTranscriptModel in src/Aetherphone/Windows/Components/ChatTranscript.cs.
- **Compact representations.** Glyph ranges are `ushort[]`, PluralKind is `enum : byte` (both in src/Aetherphone/Core/Localization/Language.cs). Pick the smallest type that holds the data.
- **Static data tables over runtime lookups.** App accents come from AppAccents.For(id) (src/Aetherphone/Core/Apps/AppAccents.cs); the changelog is a readonly array in src/Aetherphone/Core/Changelog/ChangelogData.cs.

## Performance

Aetherphone draws with Dear ImGui, an immediate mode UI library: nothing is retained between frames, the entire phone UI is rebuilt and redrawn every frame, up to your monitor's refresh rate, inside the game's render loop. Any code reachable from a `Draw` method is a hot path. That drives every rule here.

- **No LINQ in per-frame or hot paths.** LINQ extension methods (Where, Select, Any, First and friends) allocate iterators and delegates every call. The ban is structural rather than advisory: `Aetherphone.csproj` carries `<Using Remove="System.Linq" />`, so `System.Linq` is not in scope anywhere and LINQ cannot be reached without adding a `using System.Linq;` line to the file. Eight files do, all in cold paths: CalendarEvents.cs, MusicApp.cs, PhotosApp.Grid.cs, VelvetFilterSelection.cs, VelvetNotInterestedArchive.cs, VelvetStore.cs, RadioPlayer.cs and SongPlayer.cs. The "Verify no new LINQ" guard in .github/workflows/ci.yml lists exactly those and fails a pull request that adds a ninth. Write a `for` loop instead.
- **`for` over `foreach` on indexable collections.** `for` with a named index avoids enumerator allocation on non-array collections and is the dominant pattern. `foreach` remains where there is no indexer, mostly dictionary and set iteration (src/Aetherphone/Apps/Calendar/CalendarEventMerger.cs).
- **Watch allocations in draw code.** Anything allocated per frame becomes garbage-collector pressure and eventually a visible stutter in-game. The pattern to copy: compute once, cache, invalidate on a real change. Typography.cs keeps FitCache and WrapCache and clears them only when the font atlas generation changes; ChipRail.Draw takes `ReadOnlySpan<string>` so callers can pass stack or pooled data without allocating.
- **Reflection only in rarely-executed paths.** Reflection is slow and allocation-heavy. The plugin uses it in a handful of cold paths: LocAudit.CollectKeys (compiled only in debug builds, runs once at plugin boot), the Activator.CreateInstance call that creates the Media Foundation transform when an AAC radio stream starts (src/Aetherphone/Core/Radio/AacStreamDecoder.cs), and the delegate inspection that maps a chat command to its owning plugin while the Shortcuts catalog is built (src/Aetherphone/Core/Shortcuts/PluginCatalog.cs). None of it is reachable from a Draw method; keep it that way.
- **Always await awaitables in async contexts.** There are zero `async void` methods in the tree. ImGui draw code cannot await (a frame cannot pause), so work is pushed off the frame with an explicit discard, `_ = Task.Run(...)`, and inside those async bodies every awaitable is awaited.

## UI conventions in brief

Full detail with examples lives in [UI toolkit](ui-toolkit.md); this is the checklist form.

- **All text goes through the typography ladder.** Pick a TextStyle from TextStyles (LargeTitle down to Caption2) and draw with the Typography helpers. Never hand-pick a font scale for a screen.
- **Metrics tokens over pixel literals.** Spacing, radii, and control sizes come from Metrics.Space, Metrics.Radius, Metrics.Size, and Metrics.Stroke. Every pixel value, token or not, is multiplied by `UiScale.Current` so the phone scales with both Dalamud's global UI scale and the user's chosen phone size. Never read `ImGuiHelpers.GlobalScale` directly: `UiScale.cs` is the only place allowed to, and CI fails the build on any other use.
- **Text wraps, it never overflows.** Use Typography.Wrapped, Typography.DrawWrappedLeft, or Typography.FitText. Clipped or overlapping text is a bug, always.
- **A centered card is a measured stack, not a pile of hardcoded offsets.** Measure each line, add Metrics.Space gaps between them, and size the card from the total. A card with a fixed height and rows placed at `center.Y + 26f` drifts the moment a line is missing, a badge appears, or a translation is taller, and the last row lands under the button. GameOverlay (src/Aetherphone/Apps/Games/Framework/GameOverlay.cs) is the reference.
- **A top highlight follows the corner curve.** Draw the inner sheen on a card, pill, or tile with Material.Sheen (Material.SheenRounded for a plain rounded rect), never a straight `AddLine` inset by the corner radius. A squircle corner starts curving well before the radius, so a straight hairline stops short of the real edge on both sides and reads as a floating line across the top. Material.Sheen starts and ends exactly where the shape's top edge sits at that depth and fades into both corners.
- **One pannable chip rail, never a chip wall.** A row of filter chips is a single horizontally draggable ChipRail. Chips never wrap to a second line.
- **Free input over preset chips.** When the user enters a value, let them enter any value. TimeOfDayField (src/Aetherphone/Windows/Components/TimeOfDayField.cs) steps hours and minutes across the whole day rather than offering a handful of preset times.
- **Critically damped motion, no bounce.** All UI motion runs through Spring (src/Aetherphone/Core/Animation/Spring.cs), whose Step clamps at the target so it cannot overshoot. Bouncy easing (Easing.EaseOutBack) lives only in games: the mini-games under src/Aetherphone/Apps/Games/ and the casino cabinets under src/Aetherphone/Apps/Casino/Cabinets/.
- **All clock text goes through the single clock seam.** TimeText.Clock (src/Aetherphone/Core/Localization/TimeText.cs) formats every clock string and honors the user's 12/24-hour preference via TimeText.Use24Hour. There are dozens of call sites and zero hand-rolled `"HH:mm"` format strings outside TimeText itself. Keep it that way.

### Grouped lists (settings and any list of rows)

The settings tree is the reference implementation of these three rules; follow them anywhere you build a card of rows.

- **A row shows a value only when it deviates.** The right-hand string in SettingsRow.Link, AppLink, and Disclosure is for state worth acting on: the chosen language, an unread count, a feature that is off. A row never restates its own purpose ("Slash commands", "What's new") or repeats what the page below it already shows. When there is nothing to report, pass `string.Empty`.
- **One card per group of switches, never one card per switch.** Related switches share a GroupCard with hairlines between them. Reach for a SettingsSection.Header only where a page genuinely turns a corner, and never as a label for a single row.
- **A footer has to earn its place.** Explaining what one control does is the hint icon's job: pass `hint` to SettingsRow.Bool or SettingsRow.Switch, or the optional third argument of SettingsSection.Header for a whole section. SettingsSection.Hint stays for destructive warnings, loading and empty states, and sign-in prompts.

### List anatomy (which container a list of rows gets)

Every list on the phone uses one of two shared anatomies; a floating card is reserved for content that is genuinely a card.

- **Feed content gets edge-to-edge cells.** Posts, conversations, call logs, articles, activity, search results: FeedCell inside AppSurface.BeginEdgeToEdge. The cell and its trailing hairline span the full screen width, content sits a single 16px inset in (FeedCell.PadX), hover is the flat HoverWash, the whole cell taps. Chirper is the reference implementation. Section headers over cell lists come from ListSection.Header (`ListSection.Label` takes the AppSkin overload; Velvet uses VSectionHeader.Overline with an inset).
- **A photo feed goes full bleed.** In Aethergram, Velvet and anywhere else the image is the post, the media spans the entire cell width with no rounding, while the header, action row and caption keep the 16px inset. Pass `0f` rounding to the carousel and size the media off the full cell width, not the inset width.
- **A cell whose interior is all controls opts out of the whole-cell tap.** Pass `interactive: false` to FeedCell.Begin so the cell paints and separates without claiming the press. Post cards, queue rows with inline buttons, and the Discover person card do this.
- **Settings and detail lists get grouped inset cards.** GroupCard (theme or AppSkin overload) plus row painters (SettingsRow, MarketRowViews style). Fixed row heights, left-inset hairlines, the three grouped-list rules above. Velvet's Me page is the Velvet-side example, and it keeps VRow.Draw rather than VRow.Cell.
- **Floating cards are for card-shaped content only.** Sanctioned: Venues listings (hero imagery), the Notifications stack, Health metric cards, AppStore hero content, the Calendar month grid, the Photos grid, Skywatcher, Games and Casino table art, Camera, and Calculator. A card that survives inside an edge-to-edge surface is inset by FeedCell.PadX so it lines up with the cells around it: Message's my-number card, Muster's pinned muster, and the Games online host cards. Do not add new floating-card row lists.

### Sheets, dialogs, menus, and toasts

- **A destructive or choice flow is a bottom sheet.** Post and row overflow actions use an ActionSheet (style via ActionSheetStyle.From). A single destructive confirm goes through ConfirmService.Ask with `Sheet = true`, which the shell renders as an action sheet with the title and message in the header band.
- **An irreversible, monetary, or consent confirm stays an alert.** Account deletion, purchases, vault wipes, NSFW and external-link gates keep the centered ConfirmDialog (`Sheet` unset). Every `Alert(...)` stays an alert.
- **Pickers stay menus.** Sort, scope, quality, channel, and filter pickers, and any menu with inline edit or delete affordances, remain DropdownMenu.
- **Transient feedback is a toast.** ShellToast.Show for anything raised from shared components or app code; it renders as the bottom pill in whichever host (phone, popout, minimized) the pointer was in. Per-app ScreenToast instances remain for app-styled toasts (Chirper).
- **Sharing between apps is the ShareSheet; a titled content panel is a SheetSurface.** Sending something to a *person* is a different feature and does not belong in the ShareSheet: it needs the app's own recipient list, its own send state, and usually multiple sends in one visit. Aethergram's Send to screen is the reference. An app still declares `AcceptedShares` so the system sheet can route into it.
- **A popover is not a menu.** PopoverSurface hosts editors and readouts that a label list cannot express: the Jobs color editor (preset swatches plus a hex field), the Jobs category name editor, the Housing map legend, and the Housing ward grid. Leave those bespoke. Reach for DropdownMenu only when the content really is a list of labeled choices.

## Copy rules

These apply to UI strings, docs, changelogs, and commit messages alike.

- **No em dashes, anywhere.** Not in UI copy, not in the nine locale JSONs, not in docs, not in changelog bullets. Use a comma, colon, or parentheses. CI enforces this repo-wide: the "Verify no em dashes" guard in .github/workflows/ci.yml fails any pull request whose tracked files contain one. The single exemption is .github/workflows/announce-commits.yml, which carries the character on purpose so it can substitute stray dashes out of commit subjects before posting to Discord.
- **Changelog bullets carry one idea each.** The changelog ships inside the plugin: entries are LocString arrays in the Changelog section of src/Aetherphone/Core/Localization/L.cs, listed in src/Aetherphone/Core/Changelog/ChangelogData.cs, and translated in all nine locale JSONs.
- **Credit contributors in the changelog.** The pattern is a trailing clause: "..., contributed by Ehno". See the 0.9.9.5 entries in L.cs.
- **Use in-app names.** Copy refers to apps and features by their on-screen names (Photos, Jobs, Camera), not internal identifiers.
- **Name games for what they do.** Mini-game names describe the game: Sweeper, Pairs, Gem Swap (`games.*` keys in L.cs). No lore-flavored prefixes.

## Localization lockstep

Every new, renamed, or deleted user-visible string changes src/Aetherphone/Core/Localization/L.cs plus all nine JSON files in src/Aetherphone/Localization/ in the same commit. Full procedure: [Localization](localization.md).

## Git conventions

This repo uses conventional-commit style for hand-written commits: a type, a scope in parentheses, a colon, then a lowercase summary. Workflow-generated commits are the exception and keep their own fixed subjects: the "Release vX.Y.Z.W: bump repo.json" and "Update issue template versions for vX.Y.Z.W" commits come from the release pipeline, and merge commits keep their default subjects.

```
feat(account): link Patreon and wear the member badge automatically
fix(net): cap the rate-limit pause at 30 seconds
docs(changelog): note the 1.0.0.5 Casino and Coin reopen for the Chinese game version
chore(release): bump version to 1.0.0.5
refactor(sounds): make all ringtones and notification sounds file-based
ci: stop pinging the role in commit announcements
```

- Types in active use: `feat`, `fix`, `docs`, `chore`, `refactor`, `ci`, plus the occasional `perf`, `style`, and `test`. Scope is the app or subsystem you touched (`velvet`, `settings`, `ui`, `net`, `release`).
- The summary is a lowercase sentence fragment, no trailing period, and describes the user-visible outcome, not the diff.
- **One concern per PR.** Keep the diff focused (CONTRIBUTING.md). A fix and a refactor are two PRs.
- **No AI attribution.** Do not add co-author trailers or generated-with footers to commits or PR bodies. Commit messages carry substantive content only. This is the going-forward policy, not a description of history: a few dozen commits merged before mid-August 2026 still carry Co-authored-by trailers. Do not copy them.
- **Declare your AI involvement level in the PR description** when you went past autocomplete, using the six level names in [AI usage](ai-usage.md). That doc is the disclosure seam; this rule is only about keeping commit messages free of boilerplate.
- **Update the README when user-visible behavior changes**: commands, layout, settings (CONTRIBUTING.md). The README has eight translations under docs/readme/ (README.fr.md and friends); update at least README.md at the repo root.
- Release versioning is not part of a feature PR: the version lives in Directory.Build.props and CI fails if it drifts from repo.json (.github/workflows/ci.yml). See [Testing and release](testing-and-release.md).

## Gotchas

- **Typography.Draw without a draw list moves the ImGui cursor**; inside bespoke drawing, always pass the `ImDrawListPtr` overload. Full story: [UI toolkit](ui-toolkit.md).
- **Toggle.Draw returns the new value, not "was clicked"**; assign it back every frame instead of treating it as a click event. Full story: [UI toolkit](ui-toolkit.md).
- **Fixing English text only in en.json changes nothing in-game.** English resolves from the source strings in L.cs (Loc gives English an empty catalog in src/Aetherphone/Core/Localization/Loc.cs). Fix the text in L.cs and mirror it in en.json.
- **Text resolved at construction freezes its language**; store the LocString and call `Loc.T` in your Draw path. Full story: [Localization](localization.md).
- **Lockstep is enforced by the test suite, not the compiler.** LocalizationParityTests (src/Aetherphone.Tests/LocalizationParityTests.cs) fails CI whenever a key declared in L.cs is missing from any of the nine catalogs, or a catalog carries a key L.cs no longer declares. For a faster local signal, LocAudit (wrapped in `#if DEBUG`, runs once at boot) reports missing keys in the Dalamud log on a debug build after you touch L.cs.

## Related docs

- [Getting started](getting-started.md): prerequisites, build, loading the dev plugin
- [UI toolkit](ui-toolkit.md): the Components library, typography, metrics, input handling
- [Localization](localization.md): L.cs, the nine catalogs, and copy in depth
- [Testing and release](testing-and-release.md): CI, versioning, and the changelog pipeline
- [Creating an app](creating-an-app.md): these rules applied end to end in a tutorial
