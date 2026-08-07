# Aetherphone contributor rules (condensed)

Start with docs/README.md for the doc index and docs/conventions.md for the full rulebook.
This repo is the client plugin only; the Aethernet backend lives in a separate repository.

## Build

```bash
dotnet build Aetherphone.sln -c Release
```

Requires the .NET 10 SDK and Dalamud (CI pulls a dev build; see CONTRIBUTING.md).

## Hard rules

- **No em dashes anywhere**: not in code, UI strings, locale JSONs, docs, changelogs, or commits. Use commas, colons, or parentheses.
- **Localization lockstep**: every new user-visible string is a LocString in src/Aetherphone/Core/Localization/L.cs plus the same key in all nine JSONs under src/Aetherphone/Localization/ (de, en, es, fr, ja, pt, ru, tr, zh), in the same commit.
- **No AI attribution**: no co-author trailers or generated-with footers in commits or PR bodies.

## Commit style

Conventional commits with a scope and a lowercase summary, one concern per PR:

```
feat(account): link Patreon and wear the member badge automatically
fix(net): cap the rate-limit pause at 30 seconds
```

Types in use: feat, fix, docs, chore, refactor, ci, plus occasional perf, style, and test (see docs/conventions.md). Update README.md when user-visible behavior changes.

## Top style rules

- This is an immediate mode UI (Dear ImGui): Draw code runs every frame. No LINQ, no per-frame allocations, no reflection in anything reachable from Draw.
- No comments; code is self-documenting. The only allowance is a why the code cannot express, or the source of a magic constant.
- No abbreviations in names, even loop variables: index, entryIndex, drawList, never i or dl.
- Braces on every if, else, and loop body. Early returns over nesting. Explicit accessibility keywords, even for private members. Attributes on their own line.
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
