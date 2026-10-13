# Accent colors

Every app accent in `AppAccents` resolves to one of seventeen built-in colors: the fourteen ring tokens
below plus the three brand colors in `BrandAccents` (see [Brand exceptions](#brand-exceptions)). Those
accents drive the stencil tiles, the painted-icon bakes, and the palettes apps derive from them. A few
colors are picked by the user instead: the system accent (a `ThemeCatalog.Accents` preset or a custom hex,
`ThemeCatalog.IsCustomAccent`), the Jobs accent (`JobsAccentName`, a preset or a hex, which also feeds the
Jobs tile), and the chat themes Message and Linkpearl draw with (`ChatThemes`, ring tokens plus one
off-ring emerald). A tile filled from any of those must go through `IconTile.Surface`, which shades it to
legibility (see [The one rule](#the-one-rule)). This document explains where the built-in colors come
from, why they cannot simply be brightened, and how to add or change one.

## The one rule

**Every accent carries a white glyph at 3:1 or better.** That single constraint drives everything else here.

Painted icons draw first. Any app id with a painted pair in src/Aetherphone/Icons (`<id>.png` plus
`<id>.fg.png`) is drawn by `AppIconTile.TryDraw` (src/Aetherphone/Windows/Components/Chrome/AppIconTile.cs),
which `HomeTileView` and `IconTile.DrawApp` call before anything else. It renders the icon appearance the
user picked on the Settings Appearance page (`IconAppearance`):

- `Default` shows the finished painting.
- `Dark` lays the foreground on a graphite gradient; a white foreground is recoloured with the app's accent.
- `Tinted` lays the foreground as a mask in the system accent, on graphite (or on paper in Light mode).
- `Clear` draws the foreground as a white mask on `Material.LiquidGlass`.

A seasonal pair (`<id>.halloween.png` plus `<id>.halloween.fg.png`) replaces the app's pair while
`SeasonalTheme.Halloween` is on; `AppIconCache` falls back to the regular pair for any app without one.

The icon pipeline itself is covered in [Assets and media](assets-and-media.md#app-icons).

The accent squircle is the fallback. A tile whose id has no painted pair, or whose painting is not ready or
failed to load, is a solid accent squircle with a white glyph, with no exceptions and no per-tile switching.

Two variations were tried and rejected in review: flipping the glyph to dark on light accents (reads as
broken, since neighbouring tiles disagree on ink) and inverting whole tiles to a white body with a colored
glyph (reads as missing artwork at this density). Do not reintroduce either without new evidence.

`IconTile.Surface` is the normaliser for tints that never went through the ring: it shades any accent
down to `AccentRing.TileLuminance`, so white always reads on the result, while ring accents already sit
at that luminance and pass through untouched. The routed paths use it for you: `SettingsRow` icon tiles,
`ShortcutArt`, the home screen's stencil fallback, and the coin and app rows built on `IconTile.DrawApp`
all shade through `Surface` before filling. This is a convention, not a machine-enforced gate:
`IconTile.Draw` (currently uncalled) fills whatever tint it is handed, unshaded. When you draw a tile,
shade the fill with `IconTile.Surface` and paint the glyph `AccentRing.Ink` rather than passing a raw tint
straight to a fill.

## The ring

`src/Aetherphone/Core/Theme/AccentRing.cs` holds thirteen chromatic accents plus a neutral `Slate`.
They are generated, not eyeballed:

| Property | Value | Why |
| --- | --- | --- |
| Hue spacing | at least 22 degrees apart in OKLCH | below that, two tiles read as the same color |
| Relative luminance | 0.285 for all thirteen | fixes white-glyph contrast at 3.13:1 everywhere |
| Chroma | 94 percent of the sRGB gamut edge at that luminance | as vivid as the gamut allows |

Because luminance is identical across the ring, **hue is the only variable between tiles**. That is what
makes the set read as one family instead of a bag of unrelated colors.

| Token | Hex | Token | Hex |
| --- | --- | --- | --- |
| Rose | `#F95589` | Teal | `#21A29D` |
| Red | `#F95C53` | Cyan | `#219FB6` |
| Orange | `#E1741D` | Azure | `#1F96F1` |
| Gold | `#BE871D` | Indigo | `#728AF9` |
| Lime | `#809C1D` | Violet | `#A778F9` |
| Green | `#21A837` | Orchid | `#EC42F8` |
| Emerald | `#21A47D` | Slate | `#8A8F9C` |

### Why there is no bright yellow

Contrast, not chroma. A bright yellow cannot carry a white glyph: `#FFCC00` sits at 1.51:1 against
white, barely half the floor. Holding every accent to 3:1 caps relative luminance at 0.30, and `Gold`
and `Lime` are what the yellow region looks like once it is darkened enough to stay legible.

The gamut squeeze lives elsewhere. At `TileLuminance` the sRGB gamut pinches around teal and cyan: on
the shipped ring `Teal` carries 0.104 chroma and `Cyan` 0.107, against 0.278 for `Orchid` (`Gold` and
`Lime` sit higher, at 0.130 and 0.148). That is why `Teal` and `Cyan` read softer than `Red` or
`Orange`. It is a gamut limit, not an oversight, and brightening them breaks white ink.

## Assignment

`src/Aetherphone/Core/Apps/AppAccents.cs` maps every app id to a ring token. Assignments are not arbitrary:

- **Neighbours on the second seeded page differ by at least 45 degrees.**
  `AccentRingTests.DefaultSecondPageNeverPutsLikeColorsSideBySide` lays `HomeLayoutService.DefaultSecondPageApps`
  out `HomeLayoutService.Columns` wide in array order and checks every tile against its right-hand
  neighbour in the same row and against the tile below it. Each pair must sit at least 45 degrees apart in
  OKLCH hue (within a 0.05 degree tolerance). A pair passes when either side is neutral (OKLCH chroma
  under 0.04, which is `Slate`), and is skipped when both sides are brand-locked. The first seeded page
  (`DefaultFirstPageApps`) is a curated layout and is not checked or held to the rule.
- **No token repeats within a row or column** of the second seeded page. It satisfies this today, but no
  test checks it; keep it true by hand when you rearrange tiles.

When you add an app to the second seeded page, pick a token that keeps both properties true there. Run the
tests; they check the white contrast floor for every app accent and for `IconTile.Surface` of any tint,
that no accent flips to dark ink, ring separation, token distinctness, and second-page adjacency. An
adjacency failure names the offending pair and the distance.

## Brand exceptions

`src/Aetherphone/Core/Theme/BrandAccents.cs` holds identities that predate the ring and are kept off it:

| App | Hex |
| --- | --- |
| Chirper | `#2985F0` |
| Velvet | `#E51A5B` |
| Aethergram | `#EB4C61` |

These still clear the 3:1 white-glyph floor, so ink stays uniform, but they do not honour ring hue
spacing: Velvet and Aethergram sit close together deliberately. `AppAccents.IsBrandLocked` marks them and
the adjacency test skips pairs where both sides are brand-locked. Do not fold them into `AccentRing`, and
do not add new entries here without a real brand reason.

## Derived palettes

`AppPalettes.Tinted(accent)` builds all sixteen `AppPalette` fields from a single accent, so in-app chrome
always matches the tile; Jobs builds its `Tinted` palette from the user's pick (`AppPalettes.JobsFor`).
`AppPalettes.Neutral(accent)` is the variant for apps with dark neutral chrome that use the accent only as
a highlight: News, Music, Calculator, Clock, Health, Games, and Activity. The `Notes`, `Calendar`,
`PhotosThemed`, `Linkpearl` and `Announcements` factories take a `PhoneTheme` and stay theme-driven, so
those surfaces flip with Light mode. Message and Casino keep hand-authored palettes, and the Message and
Linkpearl apps swap in the palette of the chat theme the user picks (`ChatThemes.PaletteFor`).

`Tinted` backdrops go through `Palette.ShadeToLuminance`, which linearises the sRGB channels, scales them in
linear light, and re-encodes, landing every tinted backdrop at the same darkness regardless of how luminous
its accent is. A fixed `Darken` factor (a gamma-space lerp toward black) cannot do that; gold would sit
visibly brighter than azure.

## Changing a color

1. Solve the value rather than hand-picking it. No generator script ships in the repo: solve the new hue in
   OKLCH to relative luminance 0.285 at 94 percent of the sRGB gamut edge, as the table above describes;
   `OklabHue` (src/Aetherphone.Tests/OklabHue.cs) measures hue, chroma, and hue distance. A hand-picked
   value will drift off the luminance target and either break white ink or break the family look.
2. Keep the new value at relative luminance 0.285 and at least 22 degrees from every other token.
3. Run `dotnet test src/Aetherphone.Tests/Aetherphone.Tests.csproj`. `AccentRingTests` checks the white
   contrast floor (app accents and `IconTile.Surface`), ring separation, token distinctness, and
   second-page adjacency.
