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
| src/Aetherphone/Icons/ | White-on-transparent app icon PNGs, one per app id |
| src/Aetherphone/Windows/Components/AppIconTextures.cs | Resolves and tints icon PNGs |
| src/Aetherphone/Windows/Components/AppIconArt.cs | Procedural fallback art for the mini-games |
| tools/icon-generator/ | Regenerates icon PNGs from Tabler Icons |
| src/Aetherphone/Images/ | Plugin installer icon and repo screenshots, not runtime UI art |
| src/Aetherphone/Sounds/ | Bundled ringtones and notification sounds, with its own README |
| src/Aetherphone/Core/Notifications/SoundLibrary.cs | Discovers bundled plus user-imported sound files per kind |
| src/Aetherphone/Wallpapers/ | Built-in wallpapers, shipped as Light/Dark pairs |
| src/Aetherphone/Core/Wallpapers/WallpaperLibrary.cs | Discovery, custom imports, brightness analysis, theme darkness |
| src/Aetherphone/Cases/ | Art case PNGs plus the _template folder |
| src/Aetherphone/Windows/Components/PhoneCaseTextures.cs | Resolves case skin and thumbnail textures |
| docs/ART-ASSET-SPEC.md | Authoritative spec for icon and case artwork |

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

Text zoom works without rebuilding the atlas: every handle is baked at `MaxZoom` (1.5x) and drawn scaled down by setting `ImFont.Scale` to `zoom / MaxZoom` in `SetZoom` and `ApplyRenderScale`.

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

- **EmojiCatalog** (src/Aetherphone/Core/Emoji/EmojiCatalog.cs) loads catalog.json once at plugin boot (`EmojiCatalog.Load()` in src/Aetherphone/Plugin.cs). Each entry carries `file`, `short` (shortcode aliases), `group`, `order`, `label`, `tags`, and skin-tone variants under `tones`. `TryResolve` maps a shortcode like `smile` to its image file name.
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

Home-screen and in-app icons live in src/Aetherphone/Icons/ as 256x256 PNGs named after the app's registered id (`messages.png`, `settings.png`, and one per remaining app). They are stencils: pure white shapes on transparency, tinted to the active theme at draw time.

Resolution order when something asks for an app icon:

1. `AppIconTextures.TryDraw` (src/Aetherphone/Windows/Components/AppIconTextures.cs) looks for `Icons/<id>.png`, caches the resolved path, and draws it tinted, inset to 62% of the tile (`GlyphFraction`).
2. If no PNG exists, `AppIconArt.TryDraw` (src/Aetherphone/Windows/Components/AppIconArt.cs) draws procedural vector art, but only for the mini-game ids it lists (`minesweeper`, `tetris`, `chess`, and the rest of its switch).
3. If both fail, the caller draws a letter glyph fallback; see `HomeTileView` or `AppStoreApp.Rows` drawing `app.Glyph` with `Typography.DrawCentered`.

The full authoring spec (canvas, stroke weight, alpha rules, export steps) is section 1 of [the art asset spec](ART-ASSET-SPEC.md). Do not restyle icons from memory; follow it.

### To add or change an app icon

1. Preferred path: edit the `map` (app id to Tabler icon name) in tools/icon-generator/generate-app-icons.mjs and run `npm install` plus `npm run build` inside tools/icon-generator/. Pass app ids as arguments (`node generate-app-icons.mjs messages`) to regenerate a subset. Avoid Tabler `brand-*` icons; they are trademarked logos.
2. Hand-drawn path: author to [the art asset spec](ART-ASSET-SPEC.md) and drop `<appid>.png` into src/Aetherphone/Icons/. The file name must match `IPhoneApp.Id` exactly.
3. Rebuild. The csproj glob `Icons\*.png` copies the folder; `AppIconTextures` picks the file up by name with no registration step.

## General images

src/Aetherphone/Images/ is not a runtime asset pipeline:

- `Icon.png` is the plugin's installer icon. The csproj copies it to output, and src/Aetherphone/Aetherphone.json points `IconUrl` at its GitHub raw URL for the plugin repository listing.
- `screenshots/` holds images embedded in the repo README (README.md references `src/Aetherphone/Images/screenshots/Home.png`).

Nothing in Apps or Windows loads from this folder. UI imagery is either drawn procedurally, loaded from the asset pipelines on this page, or fetched at runtime (user photos, remote media).

## Sounds

Bundled audio lives in src/Aetherphone/Sounds/ in two kind-specific folders, and src/Aetherphone/Sounds/README.md is the authoritative checklist for editing them:

- `Ringtones/` plays on incoming calls, looping until answered or missed.
- `Notifications/` plays once per notification, including per-app sound overrides.

`SoundKind` (src/Aetherphone/Core/Notifications/SoundKind.cs) names the two kinds, and `PhoneServices` (src/Aetherphone/Core/PhoneServices.cs) builds one `SoundLibrary` per kind, each with two roots:

- Bundled: `<plugin output>/Sounds/Ringtones` or `.../Notifications`.
- User: `<Dalamud config dir>/Sounds/Ringtones` or `.../Notifications`, filled by the Settings "Import from PC" flow through `SoundService.AddUserFile`, which copies the picked file in. Imported files are per-user and never bundled.

`SoundLibrary.Refresh` lists `*.mp3` and `*.wav` from both roots, each root sorted by file name with bundled files first; a user file that reuses a bundled name appears once in the list but shadows the bundled file at playback (`TryResolvePath` checks the user root first). A Silent option is appended. Saved choices are tokens from `SoundTokens`: `file:<name>.mp3` or `silent`. When a saved token no longer resolves, `Resolve` falls back to the first bundled file alphabetically. Fresh installs default to `SoundLibrary.BundledRingtoneToken` (`Ringtone_1.mp3`) and `SoundLibrary.BundledNotificationToken` (`Notification_1.mp3`), so those constants must be renamed together with the files. Display names are derived from file names by `SoundLibrary.PrettyFileName` (`soft_bell.mp3` shows as "soft bell"). Playback goes through `SoundEffectPlayer`, which uses NAudio's `MediaFoundationReader` (Windows Media Foundation), so stick to .mp3 and .wav.

### To add a bundled sound

1. Drop the file into src/Aetherphone/Sounds/Ringtones/ or src/Aetherphone/Sounds/Notifications/ depending on which picker should list it. Keep ringtones seamless; they loop.
2. Name it for display: underscores and hyphens become spaces.
3. Confirm you have distribution rights and add attribution to THIRD-PARTY-NOTICES.md if required.
4. Rebuild; the csproj glob `Sounds\**\*.mp3;Sounds\**\*.wav` ships it and `SoundLibrary` discovers it with no code change, unless you renamed a default token file.

## Wallpapers

Built-in wallpapers are the image files in src/Aetherphone/Wallpapers/, shipped as Light/Dark pairs (`DuskLight.jpg` and `DuskDark.jpg`, and so on). `WallpaperLibrary.DiscoverBuiltIns` (src/Aetherphone/Core/Wallpapers/WallpaperLibrary.cs) lists `*.png`, `*.jpg`, `*.jpeg`, and `*.bmp` and uses the file name without extension as the wallpaper id, so the pairing is a naming convention, not code: the user picks one wallpaper for Light appearance and one for Dark in Settings, stored as `Configuration.LightWallpaperId` and `Configuration.DarkWallpaperId` (defaults `DuskLight` and `DuskDark`).

Users can also import their own: `WallpaperLibrary.AddCustom` copies the picked image into `<Dalamud config dir>/Wallpapers/` under a generated `custom-` id and stores a `WallpaperCrop` (zoom plus center) in `Configuration.CustomWallpapers`.

### Theme darkness and the light/dark crossfade

`WallpaperLibrary.ThemeDarkness` is a 0-to-1 value the whole device themes against:

- `ThemeMode.Light` targets 0, `ThemeMode.Dark` targets 1.
- In Auto mode the target is `Darkness`, which follows the local clock: day from 07:00, night from 19:00 (`DayStartHour`, `NightStartHour`), stepped through a spring in `StepDayNight` so the switch glides instead of snapping.

`DeviceChrome.DrawWallpaper` (src/Aetherphone/Windows/Components/DeviceChrome.cs) passes `ThemeDarkness` to `WallpaperRenderer.Draw`, which draws the light wallpaper and crossfades the dark one on top at that alpha. `ThemeProvider.Select` (src/Aetherphone/Core/Theme/ThemeProvider.cs) flips the whole UI palette to the dark theme when Auto-mode `Darkness` crosses 0.5.

Wallpaper luminance is a separate coupling, for legibility rather than theme choice: `WallpaperLibrary.MeasureBrightness` downsamples each loaded wallpaper to 24x24 and scores its luma. `HomeBrightness` blends the light and dark wallpapers' scores by `ThemeDarkness`, and `WallpaperLegibility.Strength` (src/Aetherphone/Windows/Components/WallpaperLegibility.cs) turns that into the strength of the home-screen scrim (`DeviceChrome.DrawHomeScrim`), so bright wallpapers get a stronger darkening layer behind icon labels.

### To add a built-in wallpaper

1. Add a Light/Dark pair to src/Aetherphone/Wallpapers/, named `<Name>Light.<ext>` and `<Name>Dark.<ext>` to match the existing convention. Ids are the file name stems, so choose them as final.
2. Rebuild. The csproj glob ships them and discovery lists them in the Settings wallpaper picker automatically; there are no per-wallpaper localization keys.
3. Check both appearance cards in Settings > Wallpaper, and check the home screen scrim on the brighter of the pair.

## Device cases

A phone case is the chassis art around the screen. `PhoneCaseKind` (src/Aetherphone/Core/Theme/PhoneCase.cs) has two kinds:

- `Color`: a flat tint, drawn procedurally (the default `Titanium`).
- `Art`: a painted PNG skin, drawn under everything by `CaseArt` (src/Aetherphone/Windows/Components/CaseArt.cs), which stretches one quad and swaps UVs to rotate the artwork when the phone is in landscape camera mode. `Silkie` is the shipped example.

The catalog is `ThemeCatalog.BuiltInCases` (src/Aetherphone/Core/Theme/ThemeCatalog.cs); each entry is `PhoneCase.Color(id, tint)` or `PhoneCase.Art(id, tint)`. For art cases, `PhoneCaseTextures` (src/Aetherphone/Windows/Components/PhoneCaseTextures.cs) resolves `Cases/<CaseId>.png` for the skin and `Cases/<CaseId>.thumb.png` for the Settings picker, falling back to the skin when the thumb is missing.

The artwork itself (canvas size, the 38 px metal band, the 250 px overflow margin, superellipse corners, alpha bleed, size budgets) is specified in section 2 of [the art asset spec](ART-ASSET-SPEC.md). That document is authoritative; do not work from this page for case art. src/Aetherphone/Cases/_template/ carries the working materials: `ArtCaseTemplate.svg` (the guide template), `generate-template.ps1` (regenerates it from `Core/Theme/ChassisMetrics.cs`), `generate-case.ps1` (produces conforming reference cases), and a README mirror of the spec.

### To add a case

1. Author `<CaseId>.png` and `<CaseId>.thumb.png` to [the art asset spec](ART-ASSET-SPEC.md) and drop both into src/Aetherphone/Cases/. `CaseId` is PascalCase ASCII.
2. Add one line to `ThemeCatalog.BuiltInCases`: `PhoneCase.Art("<CaseId>", <dominant metal colour>)`. The tint fills the minimized phone and the pre-load frame, and it colors the procedural hardware buttons, so pick the case's main body tone.
3. Add the display name: a `catalog.case.<caseid>` entry in `L.cs` (see `L.Catalogs.CaseSilkie`), a matching arm in `CatalogLabels.PhoneCase` (src/Aetherphone/Core/Localization/CatalogLabels.cs), and the key in all nine JSON files under src/Aetherphone/Localization/.
4. Rebuild and check the Settings > Case picker, the minimize animation, and camera-mode landscape rotation.

## Gotchas

- **Font handle churn without `SuppressAutoRebuild` is quadratic.** Each handle created or disposed outside `atlas.SuppressAutoRebuild()` triggers its own full atlas rebuild; `FontService` manages 48 handles, so an unguarded rebuild storm freezes the UI. Both `FontService.Build` and `FontService.OnLanguageChanged` show the required pattern.
- **The glyph ledger has a hard cap.** `FontService.NoticeText` stops recording once a bucket holds `LedgerCapPerBucket` (2500) codepoints, and it never records characters that were not passed to `NoticeText` in the first place. Draw sites that render user text without calling `NoticeText` show missing-glyph boxes for non-Latin text and no rebuild ever fixes them.
- **App icons are stencils.** `AppIconTextures.TryDraw` multiplies the whole PNG by the caller's ink tint, so only white-on-transparent art tints correctly; painted color survives the multiply and clashes with the themed ink instead of being restyled. Shape must live in the alpha channel; see section 1 of [the art asset spec](ART-ASSET-SPEC.md).
- **The emoji generator skips existing PNGs.** Re-running with the same pinned versions only rewrites catalog.json. If upstream Twemoji redrew an image, delete the local PNG or the stale art ships forever.
- **catalog.json and the PNG set must move together.** `EmojiCatalog` resolves `file` names against the folder with no validation pass; a catalog entry without its PNG draws nothing (`EmojiImages.TryDraw` returns false).
- **Sound default tokens are file names.** `SoundLibrary.BundledRingtoneToken` and `BundledNotificationToken` embed `Ringtone_1.mp3` and `Notification_1.mp3`. Renaming those files without updating the constants silently shifts every fresh install to the alphabetically first file.
- **Wallpaper and case ids are persisted config values.** A built-in wallpaper's id is its file name stem, and `CaseId` is both the saved setting and the localization key suffix. Renaming either after release resets or breaks every user who selected it (`ThemeCatalog.IndexOf` and `WallpaperLibrary.Resolve` both fall back to the first entry on a miss).
- **Icon and case texture paths are cached in static dictionaries.** `AppIconTextures` and `PhoneCaseTextures` cache resolved paths for the plugin's lifetime, and `PhoneCaseTextures` caches misses too, so case art that was requested while missing is never re-checked until reload (icon misses are re-checked each draw). Rebuild and reload after adding assets.
- **Assets load from the build output, not the repo.** Every loader resolves against `AssemblyLocation.DirectoryName`. Editing a file under src/Aetherphone/ does nothing for a running dev plugin until you rebuild so the csproj copies it.

## Related docs

- [UI toolkit](ui-toolkit.md): Typography, TextStyles usage, and the widget library that consumes these assets.
- [Localization](localization.md): L.cs, the nine language JSONs, and how case names get translated.
- [Notifications](notifications.md): where notification sounds and per-app sound overrides fire.
- [State and persistence](state-and-persistence.md): the Dalamud config directory that holds imported sounds, custom wallpapers, and the glyph ledger.
- [Architecture](architecture.md): plugin boot order, including font and emoji initialization.
- [Art asset spec](ART-ASSET-SPEC.md): the authoritative icon and case artwork specification.
