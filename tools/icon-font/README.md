# Icon font generator

Builds src/Aetherphone/Fonts/TablerIcons.ttf and its constants file, src/Aetherphone/Windows/Components/Primitives/PhoneIcons.cs, from [Tabler Icons](https://tabler.io/icons) (MIT). These are the small glyphs drawn inside apps. The painted home screen icons come from tools/icon-generator instead.

## Run

Run from this folder, so the virtual environment lands in the ignored .venv/. On Windows:

```powershell
py -m venv .venv
.venv\Scripts\pip install fonttools
.venv\Scripts\python generate-icon-font.py
```

On Linux or macOS:

```sh
python3 -m venv .venv && .venv/bin/pip install fonttools
.venv/bin/python generate-icon-font.py
```

The script downloads the pinned `@tabler/icons-webfont` package, subsets it to the glyphs listed in `OUTLINE` and `FILLED`, and merges the outline and filled fonts into one. Both outputs are generated: edit the lists in the script, never PhoneIcons.cs by hand.

The run ends by printing the font's size and glyph count and the codepoint range, for example `range U+E600..U+E689`. Adding a glyph moves the end of that range, so set `IconPlan.LastTablerCodepoint` to the last codepoint printed, or `IconFontCoverageTests` fails.

## Why a subset

The full Tabler fonts carry thousands of glyphs and run to megabytes. The plugin bakes every icon glyph into its font atlas at several sizes, so the subset carries only the glyphs the source draws.

## Why the codepoints move

Tabler's own codepoints collide with FontAwesome, which the plugin merges the same handles with. The script therefore remaps every glyph into a private block starting at `BASE` (U+E600). FontAwesome also has a few glyphs inside that block, so the plugin never asks FontAwesome for the Tabler range and Tabler always wins there. [Assets and media](../../docs/assets-and-media.md) covers how the plugin loads, draws and tests the font.

## Drawing

```csharp
PhoneIcon.Draw(drawList, center, PhoneIcons.Home, color, boxHeight);
```

`boxHeight` is Tabler's 24 unit design box in pixels.
