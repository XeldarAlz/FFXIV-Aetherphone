# Icon font generator

Builds `src/Aetherphone/Fonts/TablerIcons.ttf` and its
`src/Aetherphone/Windows/Components/Primitives/PhoneIcons.cs` constants from
[Tabler Icons](https://tabler.io/icons) (MIT). The painted app icons in
`tools/icon-generator/` use Phosphor; only its legacy stencil script uses Tabler.

## Run

```sh
python3 -m venv .venv && .venv/bin/pip install fonttools
.venv/bin/python generate-icon-font.py
```

The script downloads the pinned `@tabler/icons-webfont` tarball, subsets it to
the glyphs listed in `OUTLINE` and `FILLED`, and merges the two source fonts
into one. Both outputs are generated: edit the lists in the script, never
`PhoneIcons.cs` by hand. Adding a glyph moves the last codepoint, so bump
`IconPlan.LastTablerCodepoint` to match the range the run prints, or
`IconFontCoverageTests` fails.

## Why a subset

The two source fonts are 2.7 MB and 297 KB. The shipped subset is under 50 KB; each run prints
the exact size and glyph count. [Assets and media](../../docs/assets-and-media.md#icon-font)
covers how the plugin loads, draws and tests it.

## Why the codepoints move

`FontService.BuildIconHandle` merges this font on top of FontAwesome, and
`FontService.NoticeIcon` only learns codepoints inside
`[FirstIconCodepoint, LastIconCodepoint]` (U+E000..U+F8FF). Tabler's native
codepoints collide with FontAwesome, most importantly the filled set at
U+F669..U+FECF, which sits inside FontAwesome's classic U+F000..U+F8FF block.
Every glyph is therefore remapped to `BASE` onward (U+E600), above
FontAwesome 6's U+E0xx..U+E5xx additions and below its classic block.
FontAwesome 7 has since placed a few glyphs inside the block (U+E678,
U+E67A, U+E682), so `FontService.ComposeIconRanges` never asks FontAwesome
for `[FirstTablerCodepoint, LastTablerCodepoint]` and Tabler always wins
there. Moving `BASE` is no longer needed when FontAwesome grows.

## Drawing

`PhoneIcon.Draw(drawList, center, PhoneIcons.Home, colour, boxHeight)`.
`boxHeight` is the 24 unit Tabler design box in pixels, so it matches the size
argument the old hand drawn vector icons took.
