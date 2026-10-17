# Conventions

This page is the rulebook for code and commits in the Aetherphone client repo. Read it before your first pull request, and come back when a review comment surprises you. Most of these rules are enforced by the build or by CI; the rest are what a reviewer checks. Design rules for screens live in [UI toolkit](ui-toolkit.md), and the rules for user-visible text live in [Localization](localization.md).

## Key files

| Path | Role |
| --- | --- |
| .editorconfig | Formatting, naming, and code-style rules, enforced in the build |
| Directory.Build.props | Turns on code style and analyzers in the build, and warnings as errors in CI |
| .github/workflows/ci.yml | The guard checks and the build every pull request must pass |
| .github/workflows/label-pull-request.yml | The pull request title check |
| docs/ai-usage.md | The AI involvement levels and the asset provenance rules |

## Philosophy

Three principles decide most reviews:

- **YAGNI** (you aren't gonna need it). Build what the change needs now, never what you expect to need later. No settings nobody asked for, no interface with one implementation "for later".
- **DRY** (don't repeat yourself). When the same drawing or string logic appears twice, move it into src/Aetherphone/Windows/Components/ or a small static helper such as `TimeText.Ago`, not into a base class.
- **Self-documenting code.** Names and structure carry the meaning, so the C# has no comments. The one allowance is a comment that explains why, when the code cannot, or names the source of a magic constant. It never narrates what the code does. A few older comments remain in the tree; do not copy them.

## Naming

No abbreviations anywhere, including loop variables.

| Write | Not |
| --- | --- |
| index, count, entryIndex, keyIndex | i, j, k, n, cnt |
| drawList | dl |
| minuteOfDay, hourOfDay | min, hr |
| conversationIndex, groupIndex | ci, gi |

src/Aetherphone/Core/Localization/LocAudit.cs is a good model: its loops run on groupIndex, fieldIndex, and entryIndex. A few single-letter loops survive in older drawing code; do not add to them.

- Types and members are PascalCase; locals and parameters are camelCase. The build checks types, members, constants, interfaces, and parameters.
- Fields never start with an underscore. Name the field for what it holds. CI fails any private or protected field that starts with `_`.
- Bespoke drawing code for one app lives in a type named `*Renderer`, for example src/Aetherphone/Apps/Games/Chess/ChessRenderer.cs.

## Formatting

The .editorconfig is enforced by the compiler: Directory.Build.props turns on code style and the .NET analyzers in the build, so every rule the config marks as a warning becomes a build warning, and CI fails on any warning. These are build warnings:

- File-scoped namespaces.
- Braces on every `if`, `else`, and loop body, even a one-line body.
- An accessibility modifier on every member except interface members, even `private`.
- `readonly` on any field assigned only at declaration or in the constructor, and `sealed` on any internal class with no subclass.
- `var` everywhere, and the modifier order the config lists.
- The naming rules above.

The rest of the config is editor formatting that the build does not check: UTF-8, LF line endings, a final newline, 4-space indentation (2 for YAML), and opening braces on their own line. An editor that reads .editorconfig applies it for you, and CI separately rejects a byte order mark in C# files.

A few analyzers are set to suggestion level, so an editor shows them but the build never fails on them; .editorconfig gives the reason next to each.

House rules the config cannot express:

- **Attributes go on their own line** above the type, method, or multi-line member. The exception is a single short marker on a one-line property or field, such as `[JsonPropertyName("id")]` on a DTO property or `[PluginService]` in Plugin.cs; leave those inline.
- **Early returns over nesting.** Guard clauses first, then the flat happy path.
- **A blank line after a closing brace** before the next statement in the same block. The build does not catch this one: .editorconfig raises its analyzer (IDE2003), but that analyzer is experimental and reports nothing unless a separate option turns it on, so reviewers check it.

All of it together, in a real method from src/Aetherphone/PhoneCommands.cs:

```csharp
private IPhoneApp? FindApp(string query)
{
    IPhoneApp? best = null;
    var bestScore = 0;
    for (var index = 0; index < apps.Count; index++)
    {
        var app = apps[index];
        if (!app.IsAvailable)
        {
            continue;
        }

        var score = Math.Max(SpotlightMatch.Score(app.DisplayName, query),
            SpotlightMatch.Score(app.Id, query) / 2);
        if (score > bestScore)
        {
            best = app;
            bestScore = score;
        }
    }

    return best;
}
```

### Structure rules

CI checks these with fast source scans before anything compiles; [Testing and release](testing-and-release.md) lists every guard.

- **Layering.** Code under Core and Windows never imports `Aetherphone.Apps`; only the two composition roots may. [Architecture](architecture.md) explains the rule and where new code goes instead.
- **The namespace matches the folder.** src/Aetherphone/Core/Apps/ is `Aetherphone.Core.Apps`. Windows/Components is the one exception: its subfolders group files by kind, and every file there declares the flat `Aetherphone.Windows.Components` namespace.
- **No underscore-prefixed fields** and **no byte order marks** in C# files.
- **Two seams.** Draw code scales through `UiScale.Current`, and only UiScale.cs reads Dalamud's `ImGuiHelpers.GlobalScale`. Clock text goes through `TimeText.Clock`, and only TimeText.cs may write a pattern such as "HH:mm". [UI toolkit](ui-toolkit.md) explains both.
- **The vendored src/ManagedDoom** keeps its own style and is exempt from the code-style build and the namespace check.

## Types and data

The codebase leans data-oriented: plain data in flat structures, transformed by static helpers, rather than deep class hierarchies. Interfaces exist for real seams: the contracts features plug into (`IPhoneApp` for apps, `IHomeWidget` for home widgets, `IMiniGame` for games) and the inversions the layering rule requires. Do not add one with a single implementation and no such reason.

- **`sealed` by default.** `internal sealed class` is the normal class declaration.
- **`const` and `readonly` wherever possible**, including `static readonly` arrays for fixed tables.
- **Structs for small data.** Small values are a `readonly struct` or a `readonly record struct`.
- **`ref struct` where it fits.** A ref struct lives only on the stack, so it never allocates on the heap. `InputShield` (src/Aetherphone/Core/Animation/InputShield.cs) is one, used as a `using` scope.
- **The smallest type that holds the data**, for example an `enum : byte` such as `PluralKind`.
- **Static tables over runtime lookups.** App accents come from `AppAccents.For(id)`, and the changelog is a readonly array.

## Performance

Dear ImGui rebuilds the whole phone every frame, up to the monitor's refresh rate, inside the game's render loop. Any code reachable from a `Draw` method is a hot path, and an allocation there becomes garbage collector pressure and, eventually, a visible stutter in game.

- **No LINQ in the plugin.** LINQ allocates iterators and delegates on every call. The plugin project removes the implicit `System.Linq` using, so LINQ does not compile without adding the using to a file, and a CI guard allows that in only a few cold-path files. Write a `for` loop. The test project keeps LINQ.
- **`for` over `foreach`.** A `foreach` over an interface such as `IReadOnlyList<T>` boxes its enumerator and allocates on every pass; a `for` loop over the indexer does not. `foreach` stays where there is no indexer, mostly dictionaries and sets.
- **Compute once, cache, invalidate on a real change.** Typography caches fitted and wrapped text and clears the caches when the font atlas changes. Draw helpers such as `ChipRail.Draw` take `ReadOnlySpan` parameters so callers can pass stack, pooled, or cached data.
- **Cache delegates.** Turning an instance method into a delegate, or writing a lambda that captures state, allocates a new delegate each time. SettingsApp stores `drawPage = DrawPage` once in its constructor and passes the field to its router every frame.
- **Reflection only on cold paths.** Today it appears only in places such as a debug-only localization audit and building the Shortcuts plugin list, never per frame. Keep it that way.
- **Await every awaitable, and no `async void`.** Draw code cannot await, because a frame cannot pause, so it starts work with an explicit discard, `_ = Task.Run(...)`, and every awaitable inside that task is awaited. CI rejects `async void`.

## UI design rules

Typography, spacing tokens, scaling, motion, materials, list anatomy, sheets, and toasts all follow the design rules in [UI toolkit](ui-toolkit.md).

## Copy rules

Two rules engineers break most often; [Localization](localization.md) holds the full set.

- **No em dashes, anywhere.** Not in UI strings, the locale JSONs, docs, changelog bullets, or commit messages. Use a comma, colon, or parentheses. A CI guard fails any pull request that adds one.
- **Use the on-screen names.** Copy calls apps and features by the names players see (Photos, Jobs, Camera), never by internal ids.

Every new, renamed, or deleted user-visible string changes src/Aetherphone/Core/Localization/L.cs and all nine JSON files under src/Aetherphone/Localization/ in the same commit. The test suite fails when they drift.

## Git conventions

Open pull requests against `dev`, the default branch. `master` moves only when a maintainer cuts a release ([Testing and release](testing-and-release.md)).

Hand-written commits use conventional commit style: a type, a scope in parentheses, a colon, and a lowercase summary.

```text
feat(emoji): suggest emoji while typing a :shortcode in every composer
fix(jobs): count a level 50 beastmaster as capped
perf(ui): draw progress rings as one path instead of a line per segment
docs(changelog): fold the 1.3.0.0 notes into one bullet per change
chore(release): bump version to 1.3.0.0
```

- **Types:** `feat`, `fix`, `docs`, `refactor`, `perf`, `style`, `chore`, `ci`, `test`, `revert`, and `deps` for dependency bumps. The title check accepts exactly these.
- **Scope:** the app or subsystem you touched (`velvet`, `settings`, `ui`, `net`). The check accepts a title without one, but use one whenever the change has a clear home.
- **Summary:** a lowercase sentence fragment with no trailing period that describes the user-visible outcome, not the diff.
- **The pull request title follows the same shape, and CI checks it.** label-pull-request.yml fails a title that does not match, then sets the labels from the type and the paths you changed, so you never pick them by hand.
- **One concern per pull request.** A fix and a refactor are two pull requests.
- **No AI attribution.** No AI co-author trailers or generated-with footers in commits or pull request bodies. A co-author trailer that credits a person is fine.
- **Declare your AI involvement level** in the pull request description when you went past autocomplete, using the level names in [AI usage](ai-usage.md). That declaration is the disclosure; commit messages stay free of it.
- **Update the root README.md when user-visible behavior changes**: commands, layout, settings. Its eight translations live under docs/readme/.
- **Leave the version alone.** It changes only in a release, and CI fails if Directory.Build.props and repo.json disagree.

Workflow commits, such as release bumps and Dependabot updates, keep their own fixed subjects.

## Gotchas

- **A warning on your machine is an error in CI.** Local builds only print code-style and analyzer warnings. Build Release and clear the warning list before you push.
- **`.Any()` or `.Where()` does not compile in the plugin.** That is the LINQ removal, not a missing package. Write the loop; adding `using System.Linq;` fails the CI guard.
- **Fixing English text only in en.json changes nothing in game.** English comes from the source strings in L.cs. Fix it there and mirror en.json.
- **Text resolved in a constructor freezes its language.** Store the `LocString` and call `Loc.T` in the draw path.
- **A debug build reports missing translations early.** LocAudit runs once at boot in debug builds and logs missing keys, before the test suite would catch them.

## Related docs

- [Getting started](getting-started.md): build, load, and reload the plugin.
- [Architecture](architecture.md): the layering rule and where new code goes.
- [UI toolkit](ui-toolkit.md): the widget library and the UI design rules.
- [Localization](localization.md): L.cs, the nine catalogs, and the copy rules.
- [Testing and release](testing-and-release.md): CI guards, versioning, and the changelog.
