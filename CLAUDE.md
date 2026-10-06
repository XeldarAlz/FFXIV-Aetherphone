# Aetherphone contributor rules (condensed)

Start with docs/README.md for the doc index and docs/conventions.md for the full rulebook.
This repo is the client plugin only; the Aethernet backend lives in a separate repository.

## Build

```bash
dotnet build Aetherphone.sln -c Release
```

Requires the .NET 10 SDK and the Dalamud assemblies on disk (CI downloads the latest Dalamud distribution; see CONTRIBUTING.md). CI treats every warning, .editorconfig code-style rules included, as an error: a clean build has zero warnings. PRs target `dev`; `master` is release-only.

## Hard rules

- **No em dashes anywhere**: not in code, UI strings, locale JSONs, docs, changelogs, or commits. Use commas, colons, or parentheses.
- **Localization lockstep**: every new user-visible string is a LocString in src/Aetherphone/Core/Localization/L.cs plus the same key in all nine JSONs under src/Aetherphone/Localization/ (de, en, es, fr, ja, pt, ru, tr, zh), in the same commit.
- **No AI attribution**: no co-author trailers or generated-with footers in commits or PR bodies.
- **CI structure guards**: Core and Windows never `using Aetherphone.Apps*` (only AppRegistry and WidgetCatalog may); no `_`-prefixed fields; no UTF-8 BOM; the namespace equals the folder path (Windows/Components is one flat `Aetherphone.Windows.Components`).

## Commit style

Conventional commits with a scope and a lowercase summary, one concern per PR:

```
feat(account): link Patreon and wear the member badge automatically
fix(net): cap the rate-limit pause at 30 seconds
```

Types in use: feat, fix, docs, refactor, chore, plus perf, style, ci, test, and revert as needed (see docs/conventions.md). Update README.md when user-visible behavior changes.

## Top style rules

- This is an immediate mode UI (Dear ImGui): Draw code runs every frame. No LINQ, no per-frame allocations, no reflection on a per-frame path.
- No comments; code is self-documenting. The only allowance is a why the code cannot express, or the source of a magic constant.
- No abbreviations in names, even loop variables: index, entryIndex, drawList, never i or dl.
- Braces on every if, else, and loop body. Early returns over nesting. Explicit accessibility keywords, even for private members. Attributes on their own line, except a single short marker on a one-line property or field.
- File-scoped namespaces, 4-space indent, LF endings, var preferred (see .editorconfig).
- sealed by default; const and readonly wherever possible; structs and arrays over heavy collections; ref structs where they fit.
- Zero async void; await every awaitable; fire-and-forget from draw code uses an explicit discard: `_ = Task.Run(...)`.
- All text through TextStyles and Typography; spacing through Metrics tokens times UiScale.Current; text wraps and never overflows.
- All clock text through TimeText.Clock (src/Aetherphone/Core/Localization/TimeText.cs); never hand-format "HH:mm".
- Motion uses Spring (critically damped, no overshoot); bouncy easing is games-only.
- One pannable ChipRail for chip rows, never a wrapping chip wall; prefer free input over preset chips.

## Quick traps

- Toggle.Draw returns the new value, not "clicked"; assign it back every frame.
- Typography.Draw without an ImDrawListPtr moves the ImGui cursor; pass the draw list in bespoke drawing.
- English never loads en.json at runtime; fix English text in L.cs (and mirror en.json).
- Resolve Loc.T at draw time, never in constructors, or the text freezes in the old language.
