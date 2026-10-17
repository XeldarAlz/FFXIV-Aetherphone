# App icon generator

Generates the painted home-screen app icons into src/Aetherphone/Icons/ from
[Phosphor Icons](https://phosphoricons.com) (MIT) fill glyphs. The output
follows the [icon spec](https://aetherphone.net/icon-spec/) (1024 px master, two files per app, tile
families, accent hues) so hand-painted replacements drop in without engineering work.

## Run

```sh
cd tools/icon-generator
npm install
npm run build
```

`npm run build` runs generate-painted-icons.mjs, which for every id in its
`map`:

1. downloads the Phosphor fill SVG pinned at version 2.1.1 (cached under
   masters/phosphor-2.1.1/, gitignored),
2. renders the glyph once to measure its real bounding box, then scales it to
   the keyline: 58 % of the canvas wide, 62 % for symbols flagged `round`, and
   60 % tall for symbols narrower than 0.88 of their height,
3. composes a 1024 x 1024 master: a full-bleed vertical gradient for the tile
   family, then the symbol centred on it,
4. writes the 1024 px masters to masters/ (gitignored) and the shipped
   512 px pair to src/Aetherphone/Icons/.

No corners, masks, shadows or gloss are baked in; the plugin draws those.

## The two files per id

| File | Content | Alpha |
|---|---|---|
| `<id>.png` | The finished icon: tile plus symbol, full-bleed | None (PNG-24) |
| `<id>.fg.png` | The symbol alone, identical position and size, transparent tile | Straight (PNG-32) |

Both are sRGB with no ICC profile, compressed with sharp at level 9, no
palette. The loader uses the foreground file to build the Dark, Tinted and
Clear appearances.

## Tile families

| Family | Tile | Symbol |
|---|---|---|
| `colour` | The app hue as a vertical gradient, top 10 % lighter and bottom 12 % darker in OKLCH, chroma clipped to sRGB | White |
| `paper` | #FFFFFF to #F2F2F7 | The app hue (Calendar, Notes) |
| `graphite` | #3A3A3C to #1C1C1E | #D8D8DC for Settings, white for Camera, Clock, Calculator |
| `photos` | Gold to coral to azure | White |

Each id's hue should equal its entry in src/Aetherphone/Core/Apps/AppAccents.cs. The `hues` table
holds the accent ring values from src/Aetherphone/Core/Theme/AccentRing.cs plus the Chirper,
Aethergram and Velvet brand colours from BrandAccents.cs. Keep the `map` and AppAccents in step
when you add an id or change its accent. An app id with no `map` entry gets no generated pair and
falls back to the accent tile until one is painted.

The data breaks the rule in two places today:

- hunts is painted Red while AppAccents gives it Indigo.
- phone, contacts, findpeople and kupoai have `map` entries but no app and no accent. The
  first three are legacy ids that the config folds into the Messages apps, and KupoAI was removed.
  Their pairs still ship; keep or delete them on purpose.

A `colour` entry may pass `ink` to paint its symbol in a colour other than white. Gamba (casino) uses
this for its gold chip on `GambaRose`, a deeper shade of its Rose accent that keeps the gold legible.

## Game icons

Every mini-game and every online room has a painted pair as well, all in the `colour` family. The
`map` holds the home apps first and the games after them.

- A mini-game's id is the id its `GameSpec` registers, usually the `GameId` constant of the game
  class in src/Aetherphone/Apps/Games/<Game>/. It is also its key in AppAccents.
- An online room shows the pair of `OnlineGameArt.AccentId(kind)`
  (src/Aetherphone/Apps/Games/Online/OnlineGameArt.cs). A room for a game that also plays
  locally reuses that game's pair; the online-only rooms (uno, pool and connectfour) have their own.
- When you add a game, add its `map` entry in the same change, with the hue of its AppAccents
  entry, then run `npm run sheet` and compare the 32 px sheet against the other games on the same
  genre shelf (`GameGenre`).
- Two games on one shelf must not share a silhouette. Grid glyphs are the usual trap (sudoku,
  nonogram and breakout already use one), which is why Pegfall shows confetti rather than a dot grid.

## Seasonal icons

The `seasonal` table paints six Halloween pairs, `<id>.halloween.png` and `<id>.halloween.fg.png`,
for chirper, aethergram, velvet, calendar, camera and music. Each painter decorates that app's normal
design (a moonlit feather, a bat over the aperture, a carved pumpkin and so on). The plugin swaps a
seasonal pair in while the Halloween theme is on and both files exist, and keeps the usual pair
otherwise. A seasonal pair for another app needs a painter function in `seasonal`; a hand-painted
pair needs no code at all.

## Regenerating one id

Pass ids as arguments to repaint only those. Seasonal ids work too:

```sh
node generate-painted-icons.mjs messages
node generate-painted-icons.mjs settings camera chirper.halloween
```

Arguments are app ids, including game ids: `2048` repaints the 2048 game, it is not a size. An
unknown id stops the run before anything is written.

`npm run build` with no arguments repaints every id in `map` and `seasonal`. Once an id's pair is
hand-painted, take that id out of the `map`, or the next full run overwrites the artist's files.

To change a symbol, edit that id's entry in the `map`
(`icon("<phosphor-name>", "<family>", "<Hue>", { round, ink })`), browse names
at https://phosphoricons.com, and re-run for that id. Both files are rewritten
together; never ship one without the other.

## Contact sheet

```sh
npm run sheet
node contact-sheet.mjs <outDir>
```

Writes painted-icons-sheet.png (every painted id at 96 px with its label)
and painted-icons-sheet-32.png (the same at 32 px) to masters/ or the
given directory, each tile masked with the plugin's 26 % squircle so the sheet
reads the way the home grid will. An id counts as painted when both
`<id>.png` and `<id>.fg.png` exist.

## Legacy stencil generator

generate-app-icons.mjs (`npm run stencils`) is the previous pipeline: it
rasterizes Tabler outline icons to 256 px white-on-transparent stencils. It is
kept for reference only. Running it overwrites painted `<id>.png` files with
stencils while leaving the `.fg.png` companions in place, so do not run it
against the shipped folder. rolladeck.png is a partner mark outside both
generators.

## License

Phosphor Icons and Tabler Icons are MIT licensed; the notices ship with the
plugin in THIRD-PARTY-NOTICES.md at the repo root.
