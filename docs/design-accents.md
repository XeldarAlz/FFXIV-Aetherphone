# Accent colors

**Every accent carries a white glyph at 3:1 contrast or better.** That one rule shapes every app color in the phone: the fourteen ring tokens in `AccentRing`, the three brand colors in `BrandAccents`, the tiles and icons painted from them, and the palettes apps derive from them. This document explains where the colors come from, why they cannot be brightened, and how to pick or change one. Adding an app? Step 5 of [Creating an app](creating-an-app.md) is where you pick a token; reviewers may suggest another.

## Tiles and icons

Painted icons draw first. An app id with a painted pair in src/Aetherphone/Icons (`<id>.png` plus `<id>.fg.png`) is drawn by `AppIconTile` in the icon appearance the user picked under Settings, Appearance:

- `Default` shows the finished painting.
- `Dark` lays the foreground on a graphite gradient, recoloring a white foreground with the app's accent.
- `Tinted` lays the foreground as a mask in the system accent, on graphite (or on paper in Light mode).
- `Clear` draws the foreground as a white mask on glass.

A seasonal pair (`<id>.halloween.png` plus `<id>.halloween.fg.png`) replaces the regular pair while the Halloween theme is on; an app without one keeps its regular pair. [Assets and media](assets-and-media.md) covers the icon pipeline.

The accent tile is the fallback. When an id has no painted pair, or its textures are not loaded yet, the home tile (src/Aetherphone/Windows/Components/Chrome/HomeTileView.cs) fills the accent as a vertical gradient, a little lighter at the top and darker at the bottom, and draws the app's stencil or built-in icon art on it in white. An app with neither shows its `Glyph` text in white. Every tile uses white ink, with no exceptions and no per-tile switching.

Two variations were rejected in review: a dark glyph on light accents (neighboring tiles disagree on ink and it reads as broken) and a white tile with a colored glyph (it reads as missing artwork). Do not bring either back without new evidence.

Some colors come from the user instead: the system accent, the Jobs accent, and the ChocoChat and Linkpearl chat themes. `IconTile.Surface` normalizes those by shading any color down to the ring's tile luminance, so white always reads on it, while ring tokens pass through untouched. Settings row icons, shortcut art and the home fallback already use it. When you paint a tile yourself, fill it with `IconTile.Surface(tint)` and draw the glyph in `AccentRing.Ink`, never a raw tint.

## The ring

src/Aetherphone/Core/Theme/AccentRing.cs holds thirteen chromatic accents plus a neutral `Slate`. They are generated, not eyeballed:

| Property | Value | Why |
| --- | --- | --- |
| Hue spacing | at least 22 degrees apart in OKLCH | below that, two tiles read as the same color |
| Relative luminance | 0.285 for all thirteen | fixes white-glyph contrast at 3.13:1 everywhere |
| Chroma | 94 percent of the sRGB gamut edge at that luminance | as vivid as the gamut allows |

Because luminance is identical across the ring, **hue is the only thing that differs between tiles**. That is what makes the set read as one family instead of a bag of unrelated colors.

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

Contrast, not chroma. A bright yellow cannot carry a white glyph: `#FFCC00` sits at 1.51:1 against white, barely half the floor. Holding every accent to 3:1 caps relative luminance at 0.30, and `Gold` and `Lime` are what the yellow region looks like once it is dark enough to stay legible.

The gamut squeeze lives elsewhere. At the tile luminance the sRGB gamut pinches around teal and cyan: `Teal` carries 0.104 chroma and `Cyan` 0.107, against 0.278 for `Orchid`. That is why `Teal` and `Cyan` read softer than `Red` or `Orange`. It is a gamut limit, not an oversight, and brightening them breaks white ink.

## Assignment

src/Aetherphone/Core/Apps/AppAccents.cs maps every app id to a ring token. Assignments follow two rules on the second page of the default home layout (`DefaultSecondPageApps` in HomeLayoutService.cs):

- **Neighbors differ by at least 45 degrees of hue**, both side by side and one above the other. A pair passes when either side is the neutral `Slate`, and pairs of two brand colors are skipped. `AccentRingTests` checks this and names the offending pair and the distance when it fails. The first page is a curated layout and is not held to the rule.
- **No token repeats within a row or column.** No test checks this one, so keep it true by hand when you rearrange tiles.

The suite also checks the contrast floor for every app accent and for `IconTile.Surface`, and that ring tokens stay apart. It only fails a color under 3:1, so a dark enough hand-picked color passes and still breaks the family look; using a ring token is a review rule.

## Brand exceptions

src/Aetherphone/Core/Theme/BrandAccents.cs holds identities that predate the ring and stay off it:

| App | Hex |
| --- | --- |
| Chirper | `#2985F0` |
| Velvet | `#E51A5B` |
| Aethergram | `#EB4D61` |

The hex values are the floats in BrandAccents.cs rounded to bytes, matching the icon generator's hue table. These colors clear the 3:1 floor, so ink stays uniform, but ignore the ring's hue spacing: Velvet and Aethergram sit close on purpose. `AppAccents.IsBrandLocked` marks them. Do not fold them into `AccentRing`, and do not add new entries without a real brand reason.

## Derived palettes

`AppPalettes.Tinted(accent)` builds a whole palette from one accent, so in-app chrome matches the tile; Jobs builds its palette from the user's pick. `AppPalettes.Neutral(accent)` gives dark neutral chrome with the accent as a highlight (News, Music, Calculator, Clock and Games, among others). The `Notes`, `Calendar`, `PhotosThemed`, `Linkpearl` and `Announcements` factories take a `PhoneTheme`, so those screens follow Light mode.

Three apps keep hand-made palettes: Message (ChocoChat), Housing (a navy chrome with its own parchment, brass and results colors) and Gamba (`AppPalettes.Gamba`). ChocoChat and Linkpearl also swap in the palette of the chat theme the user picks.

`Tinted` backdrops shade in linear light, so every tinted backdrop lands at the same darkness; a plain gamma-space darken would leave gold visibly brighter than azure.

## Changing a color

1. Solve the value rather than hand-picking it. No generator script ships in the repo: solve the new hue in OKLCH to relative luminance 0.285 at 94 percent of the sRGB gamut edge, as the table above describes. `OklabHue` (src/Aetherphone.Tests/OklabHue.cs) measures hue, chroma and hue distance.
2. Keep the new value at least 22 degrees from every other token.
3. Update the matching entry in the icon generator's hue table (tools/icon-generator) and regenerate the affected icons.
4. Run `dotnet test`. `AccentRingTests` checks the contrast floor, ring separation, token distinctness and second-page adjacency.
