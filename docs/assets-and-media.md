# Assets and media

This page maps every asset pipeline in the client: fonts, emoji, app icons, general images, sounds, wallpapers, and device cases. For each one it explains where the files live, which code loads them, and what you have to do to add a new asset. Read it before you touch anything under src/Aetherphone/Fonts, Emoji, Icons, Images, Sounds, Wallpapers, or Cases, or before you run the generator tools. Everything here is client-side; the Aethernet backend lives in a separate repository and ships no assets to the plugin.

Two terms you will see throughout:

- **Dear ImGui** is the immediate mode UI library Dalamud exposes. Nothing is retained between frames; every texture and every glyph is drawn again each frame, so assets are loaded once into GPU textures and then referenced every frame.
- **Texture wrap**: Dalamud's `ITextureProvider.GetFromFile(path)` loads an image file into a GPU texture and caches it by path. Most asset loaders below are thin resolve-and-cache layers on top of it.

All bundled assets ship inside the plugin output folder. `src/Aetherphone/Aetherphone.csproj` copies each asset folder to the build output with `CopyToOutputDirectory`, and loaders find them at runtime via `Plugin.PluginInterface.AssemblyLocation.DirectoryName`. Consequence: after adding or editing a bundled asset you must rebuild before the running plugin can see it.

## Key files

| Path | Role |
|---|---|
| src/Aetherphone/Fonts/ | Inter TTFs (four weights) plus the OFL license text |
| src/Aetherphone/Core/FontService.cs | Font atlas owner: weights, size tiers, glyph ranges, lazy CJK ledger |
| src/Aetherphone/Windows/Components/TextStyles.cs | The named text style ladder built on the font tiers |
| src/Aetherphone/Emoji/ | Twemoji PNGs (one per emoji sequence) plus catalog.json |
| src/Aetherphone/Core/Emoji/EmojiCatalog.cs | Parses catalog.json, resolves shortcodes to image files |
| src/Aetherphone/Core/Emoji/EmojiImages.cs | Draws an emoji PNG through the texture provider |
| src/Aetherphone/Core/Emoji/EmojiScanner.cs | Finds `:shortcode:` spans in message text |
| tools/emoji-generator/ | Downloads Twemoji images and rebuilds catalog.json |
| src/Aetherphone/Icons/ | Painted app icons: `<id>.png` finished tile plus `<id>.fg.png` symbol, one pair per app id |
| src/Aetherphone/Windows/Components/Chrome/AppIconTile.cs | Draws a painted icon cut to the squircle in the chosen appearance |
| src/Aetherphone/Windows/Components/Chrome/AppIconCache.cs | Decodes and bakes icon textures off the main thread |
| src/Aetherphone/Core/Media/IconBake.cs | Builds the Dark, Tinted and Clear appearances from the symbol file |
| src/Aetherphone/Windows/Components/Chrome/AppIconArt.cs | Fallback art on the accent tile for ids without a painted pair |
| tools/icon-generator/ | Generates the painted icon set from Phosphor glyphs |
| src/Aetherphone/Images/ | Plugin installer icon and repo screenshots, not runtime UI art |
| src/Aetherphone/Sounds/ | Bundled ringtones, notification, interface and game sounds, with its own README |
| tools/sound-generator/ | Rebuilds every bundled sound: synthesis, pinned downloads, trim and loudness |
| src/Aetherphone/Core/Notifications/SoundLibrary.cs | Discovers bundled plus user-imported sound files per kind |
| src/Aetherphone/Wallpapers/ | Built-in wallpapers, shipped as Light/Dark pairs |
| src/Aetherphone/Core/Wallpapers/WallpaperLibrary.cs | Discovery, custom imports, brightness analysis, theme darkness |
| src/Aetherphone/Cases/ | Art case PNGs plus the _template folder |
| src/Aetherphone/Windows/Components/Chrome/PhoneCaseTextures.cs | Resolves case skin and thumbnail textures |
| docs/ART-ASSET-SPEC.md | Where artists work (aetherphone.net) and how delivered art lands here |

## Fonts

The UI renders with the Inter family. `FontService` (src/Aetherphone/Core/FontService.cs) owns a Dalamud `IFontAtlas`, the texture that holds every rasterized glyph. It bakes one font handle per combination of weight and size tier:

- **Weights**: `FontWeight` (Regular, Medium, SemiBold, Bold) maps to the four TTFs listed in `FontService.WeightFiles`.
- **Size tiers**: `FontService.SizeMultipliers` defines twelve fixed multipliers of the Dalamud default font size, from 0.60 to 1.90. `Push(scale, weight)` snaps any requested scale to the nearest tier with `NearestSize`, so text is never rasterized at arbitrary sizes.

You rarely call `Push` with a raw number. `TextStyles` (src/Aetherphone/Windows/Components/TextStyles.cs) names the ladder (`TextStyles.Body`, `TextStyles.Title1`, and so on), and each `TextStyle` carries a `Scale` and a `FontWeight` that map onto the tiers through the same `NearestSize` snap:

```csharp
using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
{
    ImGui.TextUnformatted(label);
}
```

Text zoom works without rebuilding the atlas: every handle is baked at `MaxZoom` (1.5x) and drawn scaled down by setting `ImFont.Scale` to `renderScale = zoom * phoneZoom / MaxZoom` in `ApplyRenderScale`. Two inputs feed that product: `SetZoom` carries the text zoom setting, and `SetPhoneZoom` carries the phone size factor, `PhoneSizeCatalog.ZoomFor(Cfg.PhoneWidth)` (seeded in the `Plugin` constructor, refreshed each frame by `PhoneWindow.PreDraw`). Both route through `ApplyZoom`, which recomputes `renderScale` and calls `ApplyRenderScale`; neither touches `ImFont.Scale` directly.

### Per-language glyph ranges and lazy CJK

A glyph range tells ImGui which Unicode codepoints to rasterize into the atlas. Baking everything would be enormous, so `FontService.ComposeRanges` combines:

- `BaseGlyphRanges`: Latin, punctuation, math, and symbol blocks that every language needs.
- The characters of every language's native name (so the language picker always renders).
- `LanguageInfo.ExtraGlyphRanges` for the current language, defined in src/Aetherphone/Core/Localization/Language.cs: Cyrillic for Russian, kana plus CJK punctuation and fullwidth blocks for Japanese, CJK punctuation and fullwidth blocks for Chinese.

Notice that no language bakes the full Han ideograph block up front. CJK ideographs (and any other codepoint outside the base ranges) load lazily through the glyph ledger:

1. Draw sites that show user-generated text call `FontService.NoticeText(text)` before drawing (see `RichText.Build` or `ChatComposer` in src/Aetherphone/Windows/Components/).
2. `NoticeText` records unseen codepoints into a per-bucket ledger, capped at `LedgerCapPerBucket` (2500) codepoints per weight-size bucket.
3. `MaybeRebuildLedger` debounces for `LedgerRebuildDebounceMs` (600 ms), then rebuilds the atlas asynchronously with `BuildFontsAsync` and bumps `FontService.Generation` so cached text layouts invalidate.
4. The ledger persists in `Configuration.FontGlyphLedger`, so a returning user does not see tofu (the hollow box shown for a missing glyph) again on the same conversations.

The CJK glyph shapes themselves come from Dalamud's bundled Noto font: `BuildHandle` merges `DalamudAsset.NotoSansCjkRegular` into each Inter handle with `AddDalamudAssetFont`, using the same ranges. Inter supplies what it covers; Noto fills the rest.

### Atlas rebuild suppression

Creating a font handle normally triggers an atlas rebuild. `FontService` creates 48 handles (4 weights x 12 tiers), so both `Build` and `OnLanguageChanged` wrap the churn in `atlas.SuppressAutoRebuild()`:

```csharp
using (atlas.SuppressAutoRebuild())
{
    handles = Build();
    DisposeHandles(previous);
}
```

Follow the same pattern in any code that creates or disposes several handles: without the guard, every handle triggers its own full rebuild and the cost grows quadratically with handle count. `OnLanguageChanged` also shows the loading screen (`LoadingScreen.Show`) because the rebuild takes visible time.

### To add or change a font

1. Drop the TTF into src/Aetherphone/Fonts/. The csproj glob `Fonts\*.ttf` copies it to output.
2. Point the matching `FontService.WeightFiles` entry at the new file name. Adding a fifth weight means extending both the `FontWeight` enum and `WeightFiles`; their order is the array index contract.
3. Ship the license text next to it (the Inter license is src/Aetherphone/Fonts/Inter-OFL.txt) and record attribution in THIRD-PARTY-NOTICES.md at the repo root.
4. Rebuild and check `FontService.Ready` turns true (the loading screen waits on it).

## Emoji

Emoji are not font glyphs. They are individual Twemoji PNG images (72x72, one file per emoji sequence, roughly 3,500 of them) in src/Aetherphone/Emoji/, plus a `catalog.json` describing them. The pieces:

- **EmojiCatalog** (src/Aetherphone/Core/Emoji/EmojiCatalog.cs) loads catalog.json once at plugin boot (`EmojiCatalog.Load()` in src/Aetherphone/Plugin.cs). Each entry carries `file`, `short` (shortcode aliases), `group`, `order`, `label`, `tags`, and skin-tone variants under `tones`; the loader never reads `order`, so display ordering comes from each entry's position in the catalog array. `TryResolve` maps a shortcode like `smile` to its image file name.
- **EmojiScanner** finds `:shortcode:` spans in a string. Messages store emoji as shortcode text, never as image references.
- **RichText** (src/Aetherphone/Windows/Components/RichText.cs) turns those spans into `RichTextRunKind.Emoji` runs during layout, and **EmojiRender** draws each one inline at 1.2x the font size.
- **EmojiImages** resolves `<file>.png` inside the Emoji folder and draws it through the texture provider. A missing file makes `TryDraw` return false and nothing is drawn.
- **EmojiPicker** (src/Aetherphone/Windows/Components/EmojiPicker.cs) browses the catalog by group, searches `label` plus `tags` plus shortcodes, and inserts `:shortcode:` into the active composer.

File names follow Twemoji's codepoint convention (`1f600.png`, `1f1e6-1f1e8.png` for flag pairs; the FE0F variation selector is stripped except inside ZWJ joins).

### The generator, and when to re-run it

tools/emoji-generator/ (`npm install`, then `npm run build`, which runs `generate-emoji.mjs`) downloads the Twemoji image set pinned by `TWEMOJI_VERSION` in the script and rebuilds catalog.json from emojibase-data. Both outputs are committed, so normal builds need no network access.

Re-run it when:

- You bump `TWEMOJI_VERSION` or the emojibase-data dependency (new Unicode emoji).
- catalog.json is missing entries or shortcodes changed upstream.

Existing PNGs are skipped on re-runs, so a rerun with unchanged versions only refreshes catalog.json. If Twemoji redraws an existing emoji, delete that PNG first so the script re-downloads it.

### To add emoji

Do not hand-add PNGs. Update `TWEMOJI_VERSION` in tools/emoji-generator/generate-emoji.mjs (the emojibase-data version lives in the tool's package.json), run the generator, and commit the new PNGs plus catalog.json together. The catalog and the image set must stay in lockstep because `EmojiCatalog` trusts `file` names blindly.

## App icons

Home-screen and in-app icons live in src/Aetherphone/Icons/ as painted pairs named after the app's registered id. `<id>.png` is the finished 512 px tile, opaque and painted to the square edges; `<id>.fg.png` is the symbol alone on transparency, in the same position and size. The plugin cuts the squircle, draws the shadow and lit edge, and builds the other appearances at runtime, so neither file carries corners or effects.

Callers draw through `AppIconTile` (src/Aetherphone/Windows/Components/Chrome/AppIconTile.cs), which resolves in order:

1. `AppIconTile.TryDraw` asks `AppIconCache` (src/Aetherphone/Windows/Components/Chrome/AppIconCache.cs) whether both files exist. If they do, it draws the tile in `Configuration.IconAppearance`: Default is the finished file, while Dark, Tinted and Clear are baked from the symbol file by `IconBake` (src/Aetherphone/Core/Media/IconBake.cs). The cache decodes off the main thread into the `TextureSizes` ladder (32 to 512 px) and never blocks a frame.
2. If either file is missing, the caller draws the accent tile and `AppIconArt` paints the stencil through `AppIconTextures` or its own procedural art.
3. If both miss, the tile falls back to the app's `Glyph` letter.

`AppIconTile.TryDrawGlyph` serves the in-app logo sites (Velvet's top bar, the Aethergram logo, the Coin wallet) with the symbol file as a tinted mask.

Artists author icons from the [icon spec](https://aetherphone.net/icon-spec/) and check them with the [icon checker](https://aetherphone.net/icon-checker/). [Art assets](ART-ASSET-SPEC.md) covers the handoff.

### To add or change an app icon

1. Generated path: add the id to the `map` in tools/icon-generator/generate-painted-icons.mjs (`icon("<phosphor-name>", "<family>", "<Hue>")`), run `npm install`, then `node generate-painted-icons.mjs <id>` inside tools/icon-generator/. It writes both files. See tools/icon-generator/README.md.
2. Hand-painted path: the artist follows the icon spec and passes the icon checker; drop both files into src/Aetherphone/Icons/. The names must match `IPhoneApp.Id` exactly.
3. Rebuild. The csproj glob `Icons\*.png` copies the folder; there is no registration step.

## General images

src/Aetherphone/Images/ is not a runtime asset pipeline:

- `Icon.png` is the plugin's installer icon. The csproj copies it to output, and src/Aetherphone/Aetherphone.json points `IconUrl` at its GitHub raw URL for the plugin repository listing.
- `screenshots/` holds images embedded in the repo README (README.md references `src/Aetherphone/Images/screenshots/Home.png`).

Nothing in Apps or Windows loads from this folder. UI imagery is either drawn procedurally, loaded from the asset pipelines on this page, or fetched at runtime (user photos, remote media).

## Sounds

Bundled audio lives in src/Aetherphone/Sounds/, and src/Aetherphone/Sounds/README.md is the authoritative checklist for editing it. Every bundled clip is rebuilt by `tools/sound-generator/generate-sounds.py` (see its README): it synthesizes the original clips, downloads the third-party ones from pinned sources, and trims, fades and loudness-normalizes everything. `Ui/` and `Games/` hold the interface and mini-game clips wired by name in `UiSoundCatalog` (src/Aetherphone/Core/Notifications/UiSound.cs). The two picker folders are kind-specific:

- `Ringtones/` plays on incoming calls, looping until answered or missed.
- `Notifications/` plays once per notification, including per-app sound overrides.

`SoundKind` (src/Aetherphone/Core/Notifications/SoundKind.cs) names the two kinds, and `PhoneServices` (src/Aetherphone/Core/PhoneServices.cs) builds one `SoundLibrary` per kind, each with two roots:

- Bundled: `<plugin output>/Sounds/Ringtones` or `.../Notifications`.
- User: `<Dalamud config dir>/Sounds/Ringtones` or `.../Notifications`, filled by the Settings "Import from PC" flow. `SoundService.AddUserFile` is a one-line forward to `SoundLibrary.AddUserFile`, which copies the picked file in. Imported files are per-user and never bundled.

`SoundLibrary.Refresh` lists `*.mp3` and `*.wav` from both roots, each root sorted by file name with bundled files first; a user file that reuses a bundled name appears once in the list but shadows the bundled file at playback (`TryResolvePath` checks the user root first). A Silent option is appended. Saved choices are tokens from `SoundTokens`: `file:<name>.mp3` or `silent`. When a saved token no longer resolves, `Resolve` falls back to the first bundled file alphabetically. Fresh installs default to `SoundLibrary.BundledRingtoneToken` (`Signal.mp3`) and `SoundLibrary.BundledNotificationToken` (`Chime.mp3`), so those constants must be renamed together with the files. Removed bundled files stay listed in `RetiredSounds` (src/Aetherphone/Core/Notifications/RetiredSounds.cs) with a replacement, and `Configuration.MigrateRetiredSounds` rewrites saved choices, per-app overrides included, on load. Display names are derived from file names by `SoundLibrary.PrettyFileName` (`soft_bell.mp3` shows as "soft bell"). Playback goes through `SoundEffectPlayer`, which dispatches by file extension: `.wav` opens with NAudio's `WaveFileReader` and `.mp3` with NLayer's managed decoder, both Wine-safe; any other extension (or a file the managed reader rejects) falls back to `MediaFoundationReader` (Windows Media Foundation). Stick to .mp3 and .wav so playback stays on the managed decoders; src/Aetherphone/Sounds/README.md covers the details.

### To add a bundled sound

1. Add the file to `RINGTONES` or `NOTIFICATIONS` in tools/sound-generator/generate-sounds.py and run the generator, which writes it into src/Aetherphone/Sounds/Ringtones/ or src/Aetherphone/Sounds/Notifications/ depending on which picker should list it. Keep ringtones seamless; they loop.
2. Name it for display: underscores and hyphens become spaces.
3. Confirm you have distribution rights and add attribution to THIRD-PARTY-NOTICES.md if required.
4. Rebuild; the csproj glob `Sounds\**\*.mp3;Sounds\**\*.wav` ships it and `SoundLibrary` discovers it with no code change, unless you renamed a default token file.

## Wallpapers

Built-in wallpapers are the image files in src/Aetherphone/Wallpapers/, shipped as Light/Dark pairs (`BloomLight.jpg` and `BloomDark.jpg`, and so on). `WallpaperLibrary.DiscoverBuiltIns` (src/Aetherphone/Core/Wallpapers/WallpaperLibrary.cs) lists `*.png`, `*.jpg`, `*.jpeg`, and `*.bmp` and uses the file name without extension as the wallpaper id, so the pairing is a naming convention, not code: the user picks one wallpaper for Light appearance and one for Dark in Settings, stored as `Configuration.LightWallpaperId` and `Configuration.DarkWallpaperId` (defaults `BuiltInWallpapers.DefaultLightId` and `DefaultDarkId`, Bloom). Removed pairs stay listed in `BuiltInWallpapers` (src/Aetherphone/Core/Wallpapers/BuiltInWallpapers.cs) with a replacement, and `Configuration.MigrateRetiredWallpapers` rewrites saved ids, Looks included, on load.

Users can also import their own: `WallpaperLibrary.AddCustom` copies the picked image into `<Dalamud config dir>/Wallpapers/` under a generated `custom-` id and stores a `WallpaperCrop` (zoom plus center) in `Configuration.CustomWallpapers`.

Textures are sized to the draw, not the file. `WallpaperLibrary.TryGetTexture(path, drawnExtent, ...)` picks a level from a 640, 1280, 2560 and native ladder by the larger drawn dimension and decodes that level on demand (`ImageProcessor.DecodeToTextureAsync` with a `maxDimension`), serving the nearest resident level while a better one loads, so Dalamud's mip-less sampler never minifies past about 2:1. Each wallpaper also bakes one 320 px wide blurred and saturated copy (`TryGetBlurred`); `WallpaperRenderer.Draw` records that copy's screen mapping in `WallpaperBackdrop` every frame the home screen paints, and `Material.LiquidGlass` samples it through any squircle to draw the dock, widgets, folders and home sheets as glass.

### Theme darkness and the light/dark crossfade

`WallpaperLibrary.ThemeDarkness` is a 0-to-1 value the whole device themes against:

- `ThemeMode.Light` targets 0, `ThemeMode.Dark` targets 1.
- In Auto mode the target is `Darkness`, which follows the local clock: day from 07:00, night from 19:00 (`DayStartHour`, `NightStartHour`), stepped through a spring in `StepDayNight` so the switch glides instead of snapping.

`DeviceChrome.DrawWallpaper` (src/Aetherphone/Windows/Components/Chrome/DeviceChrome.cs) passes `ThemeDarkness` to `WallpaperRenderer.Draw`, which draws the light wallpaper and crossfades the dark one on top at that alpha. `ThemeProvider.Select` (src/Aetherphone/Core/Theme/ThemeProvider.cs) flips the whole UI palette to the dark theme when Auto-mode `Darkness` crosses 0.5.

Wallpaper luminance is a separate coupling, for legibility rather than theme choice: `WallpaperLibrary.MeasureBrightness` downsamples each loaded wallpaper to 24x24 and scores its luma. `HomeBrightness` blends the light and dark wallpapers' scores by `ThemeDarkness`, and `WallpaperLegibility.Strength` (src/Aetherphone/Windows/Components/Chrome/WallpaperLegibility.cs) turns that into the strength of the home-screen scrim (`DeviceChrome.DrawHomeScrim`), so bright wallpapers get a stronger darkening layer behind icon labels.

Artists author wallpapers from the [wallpaper spec](https://aetherphone.net/wallpaper-spec/) and check a pair with the [wallpaper checker](https://aetherphone.net/wallpaper-checker/), which previews it under the real status bar, labels, scrim and glass dock.

### To add a built-in wallpaper

1. Add a Light/Dark pair to src/Aetherphone/Wallpapers/, named `<Name>Light.<ext>` and `<Name>Dark.<ext>` to match the existing convention. Ids are the file name stems, so choose them as final; to remove a pair, add its ids to `BuiltInWallpapers` with a replacement. The set (Bloom, Crystal, Current, Ember, Frost, Grove, Prism, Sunset) is rendered by tools/wallpaper-generator/generate-wallpapers.py; add a palette entry there rather than hand-painting a sibling.
2. Rebuild. The csproj glob ships them and discovery lists them in the Settings wallpaper picker automatically; there are no per-wallpaper localization keys.
3. Check both appearance cards in Settings > Wallpaper, and check the home screen scrim on the brighter of the pair.

## Device cases

A phone case is the chassis art around the screen. `PhoneCaseKind` (src/Aetherphone/Core/Theme/PhoneCase.cs) has two kinds:

- `Color`: a flat tint, drawn procedurally (the default `Titanium`, the only shipped one).
- `Art`: a painted PNG skin, drawn under everything by `CaseArt` (src/Aetherphone/Windows/Components/Chrome/CaseArt.cs), which stretches one quad and swaps UVs to rotate the artwork when the phone is in landscape camera mode. 58 art cases ship alongside `Titanium`.

The catalog is `ThemeCatalog.BuiltInCases` (src/Aetherphone/Core/Theme/ThemeCatalog.cs), exposed as `ThemeCatalog.Cases`; each entry is `PhoneCase.Color(id, tint)` or `PhoneCase.Art(id, category, tint, artistName, artistUrl)`. Every case carries a `PhoneCaseCategory` (`Colors`, `Gradients`, or `ArtistSeries`), and art cases record artist attribution (`ArtistName`, optionally `ArtistUrl`). The `Art` factory sets `TextureId` to the case id, and `PhoneCaseTextures` (src/Aetherphone/Windows/Components/Chrome/PhoneCaseTextures.cs) keys on `TextureId`, not `CaseId`: it resolves `Cases/<TextureId>.png` for the skin and `Cases/<TextureId>.thumb.png` for the Settings picker, falling back to the skin when the thumb is missing.

Artists author the artwork itself (canvas size, the 38 px metal band, the 250 px overflow margin, superellipse corners, alpha bleed, size budgets) from the [case spec](https://aetherphone.net/case-spec/) and check it with the [case checker](https://aetherphone.net/case-checker/); do not work from this page for case art. src/Aetherphone/Cases/_template/ carries the engineering materials: `ArtCaseTemplate.svg` (the guide template), `generate-template.ps1` (regenerates it from its own hardcoded copies of the `Core/Theme/ChassisMetrics.cs` fractions and the `DeviceChrome` key placements; the script reads no code, so they agree only by hand and nothing enforces it), and `generate-case.ps1` (produces conforming reference cases).

### To add a case

1. Take `<CaseId>.png` and `<CaseId>.thumb.png` that pass the case checker and drop both into src/Aetherphone/Cases/. `CaseId` is PascalCase ASCII.
2. Add one line to `ThemeCatalog.BuiltInCases`: `PhoneCase.Art("<CaseId>", <category>, <dominant metal colour>, "<artist name>")`, plus the artist URL when there is one. Pick the `PhoneCaseCategory` the case belongs to; every existing entry passes one. The tint fills the minimized phone and the pre-load frame, and it colors the procedural hardware buttons, so pick the case's main body tone.
3. Add the display name: a `catalog.case.<caseid>` entry in `L.cs` (see `L.Catalogs.CaseSilkie`), a matching arm in `CatalogLabels.PhoneCase` (src/Aetherphone/Core/Localization/CatalogLabels.cs), and the key in all nine JSON files under src/Aetherphone/Localization/.
4. Rebuild and check the Settings > Case picker, the minimize animation, and camera-mode landscape rotation.

## Gotchas

- **Font handle churn without `SuppressAutoRebuild` is quadratic.** Each handle created or disposed outside `atlas.SuppressAutoRebuild()` triggers its own full atlas rebuild; `FontService` manages 48 handles, so an unguarded rebuild storm freezes the UI. Both `FontService.Build` and `FontService.OnLanguageChanged` show the required pattern.
- **The glyph ledger has a hard cap.** `FontService.NoticeText` stops recording once a bucket holds `LedgerCapPerBucket` (2500) codepoints, and it never records characters that were not passed to `NoticeText` in the first place. Draw sites that render user text without calling `NoticeText` show missing-glyph boxes for non-Latin text and no rebuild ever fixes them.
- **A painted icon needs both files.** `AppIconCache.IsPainted` requires `<id>.png` and `<id>.fg.png`. With one missing, the id drops to the stencil path, where `AppIconTextures.TryDraw` multiplies the whole PNG by the caller's ink tint, so a full-colour file comes out as a flat smear.
- **Tinted and Clear read the symbol's brightness.** `IconBake` uses luminance times alpha of `<id>.fg.png` as the mask, so a coloured symbol (Calendar, Notes) renders at about half strength in those two appearances. Keep foreground symbols white unless the design accepts that.
- **The emoji generator skips existing PNGs.** Re-running with the same pinned versions only rewrites catalog.json. If upstream Twemoji redrew an image, delete the local PNG or the stale art ships forever.
- **catalog.json and the PNG set must move together.** `EmojiCatalog` resolves `file` names against the folder with no validation pass; a catalog entry without its PNG draws nothing (`EmojiImages.TryDraw` returns false).
- **Sound default tokens are file names.** `SoundLibrary.BundledRingtoneToken` and `BundledNotificationToken` embed `Signal.mp3` and `Chime.mp3`. Renaming those files without updating the constants silently shifts every fresh install to the alphabetically first file.
- **Wallpaper and case ids are persisted config values.** A built-in wallpaper's id is its file name stem, and `CaseId` is both the saved setting and the localization key suffix. Renaming either after release resets or breaks every user who selected it (`ThemeCatalog.IndexOf` and `WallpaperLibrary.Resolve` both fall back to the first entry on a miss).
- **Icon and case lookups are cached in static dictionaries.** `AppIconCache` remembers per id whether both painted files exist, misses included, and `PhoneCaseTextures` caches resolved paths and misses too, so art requested while missing is never re-checked until reload. Rebuild and reload after adding assets.
- **Assets load from the build output, not the repo.** Every loader resolves against `AssemblyLocation.DirectoryName`. Editing a file under src/Aetherphone/ does nothing for a running dev plugin until you rebuild so the csproj copies it.

## Related docs

- [UI toolkit](ui-toolkit.md): Typography, TextStyles usage, and the widget library that consumes these assets.
- [Localization](localization.md): L.cs, the nine language JSONs, and how case names get translated.
- [Notifications](notifications.md): where notification sounds and per-app sound overrides fire.
- [State and persistence](state-and-persistence.md): the Dalamud config directory that holds imported sounds, custom wallpapers, and the glyph ledger.
- [Architecture](architecture.md): plugin boot order, including font and emoji initialization.
- [Art assets](ART-ASSET-SPEC.md): where artists work on aetherphone.net, the handoff for each asset, and the constants the website copies.
