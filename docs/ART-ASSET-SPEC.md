# Art assets

Artists work from the spec pages and checkers on [aetherphone.net](https://aetherphone.net/), not from this repository. This page is for engineers: where each delivered file lands, what code change it needs, and which plugin constants the website copies, so a change here does not leave the artist pages stale. Read it before you add art an artist made, or before you change phone geometry, icon rendering, wallpaper legibility or the hardware keys.

## Key files

| Path | Role |
|---|---|
| src/Aetherphone/Icons/ | Painted app icons: `<id>.png` finished tile and `<id>.fg.png` symbol, one pair per app id |
| src/Aetherphone/Wallpapers/ | Built-in wallpapers, one `<Name>Light` and `<Name>Dark` file per pair |
| src/Aetherphone/Cases/ | Art case skins and thumbnails, plus the `_template` working folder |
| src/Aetherphone/Core/Theme/ThemeCatalog.cs | The case catalog and the theme accents |
| src/Aetherphone/Core/Wallpapers/BuiltInWallpapers.cs | Default wallpaper ids and retired-id replacements |
| tools/icon-generator/ | Generates the painted icon set from Phosphor glyphs |
| tools/wallpaper-generator/ | Generates the bundled wallpaper pairs |
| Aethernet `website/assets/art-kit.js` | The website's shared copy of the plugin constants the checkers and templates use |
| Aethernet `website/build-art-kit.mjs` | Rebuilds the site's guide templates, preview icons and wallpaper previews from art-kit.js and a plugin checkout |
| Aethernet `website/*-spec/index.html`, `*-checker/index.html` | A few page-local copies in each page's script (shipped icon ids, wallpaper names, case tints, glass finish, island, key shading), with the source file in a comment |

## Where artists work

| Asset | Spec | Checker | Ships through |
|---|---|---|---|
| Avatar frame | [frame-spec](https://aetherphone.net/frame-spec/) | [frame-checker](https://aetherphone.net/frame-checker/) | Mod console upload, no plugin release |
| Phone case | [case-spec](https://aetherphone.net/case-spec/) | [case-checker](https://aetherphone.net/case-checker/) | Plugin release |
| App icon | [icon-spec](https://aetherphone.net/icon-spec/) | [icon-checker](https://aetherphone.net/icon-checker/) | Plugin release |
| Wallpaper | [wallpaper-spec](https://aetherphone.net/wallpaper-spec/) | [wallpaper-checker](https://aetherphone.net/wallpaper-checker/) | Plugin release |
| Theme | [theme-spec](https://aetherphone.net/theme-spec/) | Theme panel in the wallpaper checker | No file: a recipe players build as a Look |

Each asset spec page offers guide templates to download (the theme spec has none), and each checker runs the same limits the page states, calibrated on the set that ships today. Ask for a clean checker run before art reaches review.

## Adding delivered art

### App icons

1. Drop `<id>.png` and `<id>.fg.png` into src/Aetherphone/Icons/. The id must match `IPhoneApp.Id`.
2. Rebuild. There is no registration: `AppIconCache` treats an id as painted when both files exist.
3. A brand-new id also needs its app in code and an `AppAccents` entry (see [creating an app](creating-an-app.md)).
4. For the website, add the id's accent to `appAccents` in art-kit.js and the id to `SHIPPED_IDS` in the icon checker. The icon spec previews only the ids in `previewIds` in build-art-kit.mjs.

To generate an icon instead of painting one, add the id to the `map` in tools/icon-generator/generate-painted-icons.mjs and run it for that id. A hand-painted pair simply replaces the generated files.

### Wallpapers

1. Drop `<Name>Light.jpg` and `<Name>Dark.jpg` into src/Aetherphone/Wallpapers/.
2. Rebuild. Discovery is by file name, and the Settings picker lists the pair with no code change.
3. To remove or rename a shipped pair, add its ids to `BuiltInWallpapers` with a replacement so saved settings migrate.
4. The website previews list wallpapers by name: add the name to `wallpaperNames` in build-art-kit.mjs and to `WALLPAPERS` in the icon checker, then rebuild and redeploy the site. Until then the pair never reaches the spec and checker pages.

### Phone cases

1. Drop `<CaseId>.png` and `<CaseId>.thumb.png` into src/Aetherphone/Cases/.
2. Add one line to `ThemeCatalog.BuiltInCases` with the category, the artist's dominant metal colour (the case checker suggests one) and the artist name.
3. Add the display name: `catalog.case.<caseid>` in `L.cs`, an arm in `CatalogLabels.PhoneCase`, and the key in all nine JSON files.

The full steps are in [assets and media](assets-and-media.md#device-cases).

### Avatar frames

Frames upload through the mod console's Frames page and reach players without a plugin release. The flow lives in the Aethernet repository.

### Themes

Themes ship no file. Players build Looks in Settings > Appearance; a theme an artist designs reaches them as a recipe until preset Looks exist.

## Keeping the website in sync

art-kit.js copies plugin constants by hand and names the source file next to each block; the page-local copies in the spec and checker pages do the same. When you change any of these, update art-kit.js or the page-local block that names the file, run `AETHERPHONE_ROOT=<plugin checkout> node website/build-art-kit.mjs` in the Aethernet repo (the script reads Icons/ and Wallpapers/ from that checkout, and its default path is a Windows one), and redeploy the site (Aethernet `docs/WEBSITE.md`). The pages read art-kit.js at runtime, so a change that touches no template or preview needs only the redeploy:

| Plugin source | What the website uses it for |
|---|---|
| Core/Theme/PhoneSizeCatalog.cs, ChassisMetrics.cs, PhoneTheme.cs | Phone sizes, screen and bezel, case geometry, the fixed palette |
| Core/Shell/Home/HomeMetrics.cs, HomeChrome.cs, HomeGridRenderer.cs, Core/Home/HomeLayoutService.cs | Grid, icon size, dock, Search pill, default dock apps |
| Windows/Components/Primitives/Squircle.cs, Metrics.cs | The corner exponent and the icon corner box |
| Core/Media/IconBake.cs, TextureSizes.cs, Windows/Components/Chrome/AppIconTile.cs, AppIconCache.cs | The four icon appearances, the icon size ladder, the painted-pair rule |
| tools/icon-generator/generate-painted-icons.mjs | Icon master size and keylines |
| Core/Apps/AppAccents.cs, Core/Theme/AccentRing.cs, BrandAccents.cs, ThemeCatalog.cs | App hues, theme accents, the default and sample case tints |
| Core/Wallpapers/WallpaperLibrary.cs, Windows/Components/Chrome/WallpaperLegibility.cs | Wallpaper ladder, glass blur, brightness score, legibility curve |
| tools/wallpaper-generator/generate-wallpapers.py | Wallpaper size |
| Windows/Components/Chrome/CaseArt.cs, Core/Theme/CaseFinish.cs, Core/Shell/StatusBar.cs | Case canvas and nine-slice margins, glass finish, the resting island |
| Windows/Components/Chrome/DeviceChrome.cs | Hardware key placement, home scrim alphas |
| Windows/Components/Chrome/HardwareButton.cs | Key footprint and shading |
| Windows/Components/Primitives/Material.cs | Liquid glass tint |

## Open art decisions

- **Case band width.** The band is 38 px; comparable plugins use about 2.4 times that. Widening it changes the case canvas and forces every finished case to be re-exported, so decide before more case art is commissioned.
- **Off-centre avatar frames.** A frame's scale sets its size, not its position. A design that cannot sit centred needs two more numbers on the frame record.

## Gotchas

- **An icon needs both files.** With either `<id>.png` or `<id>.fg.png` missing, the tile falls back to the accent tile and stencil path, which tints the whole image and makes a painted file look broken.
- **Shipped art predates some rules.** Most bundled cases have rounder corners than the phone's curve and some paint inside the cutout; the bundled Light wallpapers are pale. The checkers warn on these rather than fail, so a new file that matches a shipped one can still be flagged.
- **Case templates are generated by hand-kept copies.** `Cases/_template/generate-template.ps1` and art-kit.js both hardcode the chassis fractions, the key placements and the key shares. Nothing enforces agreement with `ChassisMetrics.cs`, `DeviceChrome.cs` or `HardwareButton.cs`; update them together.
- **The website lists ids and names by hand.** art-kit.js `appAccents`, the icon checker's `SHIPPED_IDS` and `WALLPAPERS`, and build-art-kit.mjs `previewIds` and `wallpaperNames` are copies. An app id missing from `appAccents` previews on the Slate fallback hue.
- **Ids are permanent.** Icon ids, wallpaper file stems and `CaseId` are saved in every user's config. Renaming one after release resets or breaks everyone who picked it.

## Related docs

- [Assets and media](assets-and-media.md): every asset pipeline, the loaders and the step-by-step adds.
- [Creating an app](creating-an-app.md): accent and icon for a new app.
- [Accent colors](design-accents.md): the accent ring and its contrast rule.
- [UI toolkit](ui-toolkit.md): the squircle, materials and tiles that draw the art.
