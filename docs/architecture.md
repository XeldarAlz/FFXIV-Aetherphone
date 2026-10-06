# Architecture overview

This page is the big-picture map of the Aetherphone client: what gets built at boot, how services are wired together, what runs on which thread, and how a frame travels from Dalamud down to a single app's `Draw` call. Read it before your first dive into the code, then keep it open as a reference while you explore. Everything here describes the plugin (client) only; the Aethernet backend is a separate ASP.NET service in its own repository, and its client lives in `src/Aetherphone/Core/Aethernet` (see [Networking](networking.md)).

Two terms you need up front:

- **Dalamud** is the plugin framework injected into Final Fantasy XIV. It loads plugin assemblies, hands them game services (chat, object table, textures, and so on) through dependency injection, and hosts the UI layer.
- **Dear ImGui** is an immediate mode UI library: there is no retained widget tree. Every visible pixel is re-issued every frame by your draw code. If you stop calling a draw function, the thing disappears.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Plugin.cs | Plugin entry point; constructs and owns everything |
| src/Aetherphone/Core/PhoneServices.cs | Composition root; builds every shared service once |
| src/Aetherphone/Core/Runtime/FrameworkTicker.cs | Interval-throttled work on the game framework tick |
| src/Aetherphone/Windows/PhoneWindow.cs | The one borderless ImGui window the phone lives in |
| src/Aetherphone/Core/Shell/PhoneShell.cs | Per-frame orchestrator: chassis, content, chrome, overlays |
| src/Aetherphone/Core/Shell/ShellOverlayCoordinator.cs | Overlay z-order and pointer-capture arbitration |
| src/Aetherphone/Core/Shell/ShellScreenPainter.cs | Paints home or the active app into the screen rect |
| src/Aetherphone/Core/Apps/NavigationStack.cs | Which app is open, history, present/dismiss motion |
| src/Aetherphone/Core/Apps/IPhoneApp.cs | The contract every phone app implements |
| src/Aetherphone/Core/Apps/AppRegistry.cs | Builds the list of every app at boot |
| src/Aetherphone/Windows/Components/Chrome/DeviceChrome.cs | Draws the physical phone body, glass, and screen |
| src/Aetherphone/Core/Theme/ChassisGeometry.cs | Body/Glass/Screen rectangles and corner radii |
| src/Aetherphone/Core/Rect.cs | The tiny rectangle struct the whole UI is measured in |
| src/Aetherphone/Configuration.cs | All persisted settings, one Dalamud plugin config object |
| src/Aetherphone/Core/Config/ConfigMigrations.cs | Rewrites legacy type names in the config JSON before load |

## The layer stack

```
FFXIV
 └── Dalamud                 plugin host: game services, Dear ImGui, WindowSystem
      └── Plugin             entry point: config, services, fonts, windows
           └── PhoneWindow   one borderless ImGui window, sized like a phone
                └── PhoneShell        chassis, status bar, overlays, navigation
                     ├── HomeScreen           app grid, widgets, folders
                     └── IPhoneApp.Draw       the currently open app
```

Each layer only talks downward: `Plugin` builds `PhoneShell` and `PhoneWindow`, `PhoneWindow.Draw` calls `PhoneShell.Draw`, and the shell decides whether the home screen or an app paints the screen this frame.

## Plugin boot (Plugin.cs)

`Plugin` implements Dalamud's `IDalamudPlugin`. Dalamud fills the `[PluginService]` static properties (for example `IFramework`, `IClientState`, `ITextureProvider`) before the constructor runs, so the references are populated by the time your code sees them. That does not make every call on them safe: the constructor can run off the game's main thread, so reading live character state there is a crash. See [the constructor is not the framework thread](game-integration.md#the-constructor-is-not-the-framework-thread).

The constructor runs, in order:

1. `ConfigMigrations.Run(PluginInterface.ConfigFile)` rewrites legacy type names inside the raw config JSON, then `PluginInterface.GetPluginConfig()` deserializes `Configuration`, `NormalizeAethernetBaseUrl()` resets a blank, malformed or retired backend URL to the build's default, and the `Migrate*` methods on it run (sounds, changelog, messages merge, character sessions, and more).
2. `InitializeLocalization()` loads the string catalog for `Configuration.Language`. On first run that field is empty, so it detects a language first (game client language, then OS language, then English) and saves it; later boots load the saved language.
3. `InstallSource.Initialize(PluginInterface)` records where the plugin was installed from (a dev plugin or its source repository) and the informational build string.
4. `Device = new DeviceStatus(...)` starts the battery/latency/signal sampler used by the status bar.
5. `PhoneServices.Build(...)` constructs every shared service (next section).
6. `Fonts = new FontService(...)` builds the Inter font atlas at every weight and size bucket.
7. `EmojiCatalog.Load()` runs, then the video subsystem comes up: one `VideoSuite` (src/Aetherphone/Core/Video/VideoSuite.cs) constructs and owns `ScreenController`, `VideoPlayer`, `VideoLibrary`, `AetherStreamQueue`, `WatchAlongSession`, `StreamSuggestionNotifier`, and `ScreenChatFeed`. `OnVideoFrameworkUpdate`, `OnDeviceLinkTick` and `OnCallsTick` subscribe to `Framework.Update` (see [Frame loop and threads](#frame-loop-and-threads)), and `VideoDebugWindow`, `AetherStreamScreenWindow`, and `VideoWorldOverlay` are built.
8. The Linkpearl pop-out stack is built before the registry, because the Linkpearl app (id `messages`) takes it: `LinkpearlPopouts` (detached game chat windows, gated on the `messages` app), `PopoutPresence`, and `LinkpearlHotkey`.
9. `AppRegistry.BuildDefault(services, videoSuite, screenWindow, linkpearlPopouts)` constructs every app into an `AppBundle` (apps, home widgets and widget actions, photo library, contact book, message pop-outs, and the video suite).
10. `new PhoneShell(services, bundle)` builds the shell, `services.NameplateTitles.Bind(...)` connects nameplate titles to the watch-along session and the foreground app, `ScreenshotImportService` starts, and `new PhoneWindow(shell, Cfg)` builds the window. The fixed windows (`PhoneWindow`, `UpdateChipWindow`, `HuntsMapMarkersIndicatorWindow`, `PhotoWindow`, `VideoDebugWindow`, `AetherStreamScreenWindow`) plus every `MessagePopoutWindow` and `LinkpearlPopoutWindow` are added to a Dalamud `WindowSystem`, the helper that tracks window open state and calls each window's draw methods. The message and Linkpearl pop-outs then `Restore()` the windows that were open last session, and `OnLinkpearlPresenceTick` joins `Framework.Update`.
11. `services.Visibility.Bind(...)` connects the visibility probes to the window (next section), then background services start: `PhoneEmoteController`, `TimerNotifier`, `CalendarReminderService`, `ClockAlarmService`, `ReminderService`, the character session watchers, and `CallHub`.
12. A server info bar entry (`ServerBarEntry` over `IDtrBar`) is created, `MarketIndex.EnsureBuilt()` starts the market item index, and a context menu hook plus the chat commands `/phone` and `/aetherphone` (see `AepConstants`; Debug and Beta builds use their own) are registered.
13. `PluginInterface.UiBuilder.Draw += windowSystem.Draw` wires the whole UI into Dalamud's ImGui frame, next to `FilePicker.Draw`, `LinkpearlHotkey.Tick` and `VideoWorldOverlay.Draw`. `OpenMainUi` toggles the phone, `OpenConfigUi` opens Settings, `DisableGposeUiHide` follows `Configuration.ShowInGpose`, and `ClientState.Login` queues the auto-open.

If any step throws, the catch block calls `TearDownPartialConstruction()` so a half-built plugin never leaks event subscriptions, then rethrows so Dalamud reports the load failure.

A handful of statics are exposed for hot paths that would otherwise thread a parameter through dozens of constructors: `Plugin.Cfg`, `Plugin.Fonts`, `Plugin.Wallpapers`, `Plugin.LiveBackdrop`, `Plugin.Device`, `Plugin.Updates`, `Plugin.PhotoWindow`, `Plugin.Instance`. Services flow through constructor injection, apart from a few static seams bound once at boot: `PhoneServices.Build` calls the likes of `UiFeedback.Bind`, `Frames.Use`, `UserName.Configure` and `UrlActions.Configure`, and the `Plugin` constructor sets `FilePicker.ProblemReporter`.

## Service composition (PhoneServices.cs)

`PhoneServices` is not a service locator or DI container. It is a single composition root: one class with `required` init-only properties, built exactly once by the static `PhoneServices.Build(...)` factory inside the `Plugin` constructor. `Build` news up every service in dependency order (notification pipeline, HTTP and caches, Aethernet session and API, market, music, telephony, and so on) and returns the filled object.

Consumers never "look up" services at runtime. `AppRegistry.BuildDefault(...)` passes each app exactly the services it needs through its constructor, and `PhoneShell` does the same for shell components. `PhoneServices.Dispose()` tears everything down in reverse dependency order when the plugin unloads.

`PhoneVisibility` (src/Aetherphone/Core/Runtime/PhoneVisibility.cs) deserves a note: it is a two-probe indirection that services use to ask "is the full phone on screen right now?" (`IsVisible`) and "where is the phone's frame?" (`TryGetFrame`, used to place Linkpearl pop-outs beside the phone). The `Plugin` constructor binds both to the window state:

```csharp
services.Visibility.Bind(() => phoneWindow is { IsOpen: true, IsMinimized: false },
    () => phoneWindow is { IsOpen: true }
        ? new Rect(phoneWindow.LastPosition, phoneWindow.LastPosition + phoneWindow.LastSize)
        : default);
```

Services built before the window exists can hold `PhoneVisibility` and probe it later, which breaks what would otherwise be a construction-order cycle.

## Frame loop and threads

Two Dalamud callbacks drive everything:

- **`IFramework.Update`** fires once per game tick on the game's framework (main) thread. This is where anything that reads game memory must run: `IObjectTable`, `IClientState` player data, condition flags. Background services (activity tracking, alarms, inventory capture, health sampling) live here, and so does anything that must keep running while the phone is minimized or closed. `Plugin` subscribes its own handlers too: `OnVideoFrameworkUpdate` (the video suite), `OnDeviceLinkTick` (device links, the encryption guide, decrypted chat history), `OnCallsTick`, `OnLinkpearlPresenceTick` (hides and restores the Linkpearl pop-outs), and `OnAutoOpenTick` while an auto-open is pending. `OnCallsTick` advances `CallHub` every tick, so ring and dial timeouts and the reconnect grace run even when the phone is not drawn.
- **`PluginInterface.UiBuilder.Draw`** fires once per rendered frame while ImGui is building its draw data. `Plugin` subscribes `windowSystem.Draw` here, which calls `PhoneWindow.PreDraw`, `Draw`, and `PostDraw`, plus `FilePicker.Draw`, `LinkpearlHotkey.Tick` and `VideoWorldOverlay.Draw`. All ImGui calls must happen inside this callback.

`FrameworkTicker` (src/Aetherphone/Core/Runtime/FrameworkTicker.cs) is the standard way to do periodic work on the framework thread. It subscribes to `Framework.Update`, skips ticks until `intervalMilliseconds` has elapsed, and honors an `AppGate` (src/Aetherphone/Core/Home/AppGate.cs) so work for an uninstalled app never runs:

```csharp
private void OnUpdate(IFramework owner)
{
    if (!gate.Open)
    {
        return;
    }

    var now = Environment.TickCount64;
    if (now - lastTickMilliseconds < intervalMilliseconds)
    {
        return;
    }

    lastTickMilliseconds = now;
    onTick();
}
```

`ActivityTracker`, `HealthTracker`, `InventoryCaptureService`, `TimerNotifier`, `ReminderService`, `ClockAlarmService`, and `CalendarReminderService` are among the services that run on `FrameworkTicker` instances.

Crossing threads: `Configuration.Save()` checks `Plugin.Framework.IsInFrameworkUpdateThread` and, when called from anywhere else, marshals the save onto the framework thread with `RunOnFrameworkThread`. Follow that pattern whenever you need game-thread affinity from async code.

## PhoneWindow: how ImGui windows work in Dalamud

A Dalamud window is a class deriving from `Dalamud.Interface.Windowing.Window`. You set `Size`, `Position`, and `Flags`, override `PreDraw`/`Draw`/`PostDraw`, and the `WindowSystem` calls them each frame while `IsOpen` is true. `PhoneWindow` uses flags that remove everything a normal window has (`NoTitleBar`, `NoResize`, `NoBackground`, `NoScrollbar`), leaving a transparent canvas that `DeviceChrome` paints a phone onto.

Scaling rule, verified in `PhoneWindow.PreDraw`: **`Window.Size` is specified in unscaled units and Dalamud multiplies it by `UiScale.Global` (the raw Dalamud scale, *not* `UiScale.Current`); `Window.Position` is raw screen pixels and is not scaled.** This is the one place the phone zoom must be left out, because the size assigned to `Window.Size` already carries it. Getting this wrong double counts the zoom. That is why centering the window multiplies the size manually:

```csharp
var viewport = ImGui.GetMainViewport();
var scaledSize = size * UiScale.Global;
Position = viewport.Pos + (viewport.Size - scaledSize) * 0.5f;
```

Other things `PhoneWindow` handles:

- The window size comes from `PhoneSizeCatalog.SizeFor(width)`, where `width` is `Configuration.PhoneWidth` run through `PhoneBounds.ClampWidth`. Width is continuous (240 to 900, clamped further to fit the game window), height is always `width * PhoneSizeCatalog.AspectRatio`, and the size is re-applied every frame with `SizeCondition = ImGuiCond.Always`. The clamp is applied for display only and never written back to config, so shrinking the game window does not destroy a saved size.
- `PreDraw` publishes the zoom for the frame: `UiScale.SetPhone(zoom)` for layout and `Plugin.Fonts.SetPhoneZoom(zoom)` for text, where `zoom = width / 360`. It also pushes `FramePadding`, `ItemSpacing`, `ItemInnerSpacing`, `ScrollbarSize` and `GrabMinSize` scaled by the zoom so native ImGui widgets track the phone.
- Minimized mode sizes the window to the mini phone: a fixed body of `MinimizedShapes.BodyWidth` by `BodyHeight` design units (92 by 188) that `MinimizedPhone.Measure` returns at `UiScale.Global` times `UiScale.Minimized`, so it ignores the full phone's zoom and never grows with its content. `UiScale.Minimized` is `Configuration.MinimizedScale` clamped by `PhoneBounds.ClampMinimizedScale`, set by `PhoneWindow.PreDraw` every frame, and it is also the phone zoom while resting minimized, so fonts, tooltips and toasts follow the mini phone. The only thing that changes its size is dragging its bottom right corner (`ResizeGrip.Track`, the same grip type the full phone uses), which snaps to 1 near the default. Text inside the mini phone goes through `UiScale.MinimizedText`, which cancels whatever phone zoom the morph is running under while keeping the mini phone's own scale. The window pins itself to a saved dock anchor clamped inside the viewport, and lerps the position between the saved maximized spot and that anchor while the morph runs. The mini phone handles its own drag (`NoMove` is set), so the anchor moves by the drag delta it reports.
- `Configuration.MinimizedShape` picks what the face shows. `MinimizedShape.Phone` is the clock face below; `MinimizedShape.Minimap` uses the same body and shows the zone map instead. The map span is `MinimizedShapes.MapSpan(Configuration.MinimizedMapZoom)`, a zoom level that the mouse wheel (or the zoom buttons shown on hover) steps over the map. Both shapes go through the same `ChassisGeometry.Puck` fractions and the same `MinimizeMorphView` morph, so the only seam is the map branch in `MinimizedPhone.DrawFace`.
- The phone face is a status line (the do-not-disturb moon, the unread count, and a small island pill while a call and music are both live), a large clock with the date, and one glass card at the bottom. `MinimizedLayoutService` (src/Aetherphone/Core/Shell/MinimizedLayoutService.cs) holds every `MinimizedPart` in one ordered list with an on/off flag, persisted as `Configuration.MinimizedLayout` and edited in Settings > Display > Minimized phone, which splits the parts into live activities and card pages (`MinimizedParts.IsLive` / `IsPage`). The card shows the enabled card pages one at a time, drawn by `MinimizedWidgetRenderer`: Eorzea time and zone weather (both on by default), the next reset, gil, Aether Coin, retainer ventures and the activity rings; the mouse wheel over the card flips between them. A live call or live music (the phone's own playback or PC media) takes the card over with its controls always visible, and notifications drop in as a glass banner over the clock. Widget data comes from `MinimizedFeed`, which reads only what a drawn page asks for: weather once a real minute or when the zone, the weather window or the live weather changes (`WeatherPulse`), gil every second, and ventures every 5 s from the Timers ledger (`TimerLedger`). `Configuration.MinimizedWallpaper` paints the home wallpaper (the same light/dark pair through `WallpaperLibrary.ThemeDarkness`) behind the face, dimmed by `WallpaperLegibility.Strength`; whenever the screen is painted edge to edge like that, or with the map, `DrawFace` follows up with `DeviceChrome.MaskScreenCorners` because a texture cannot be clipped to the rounded screen (the live glass body draws the wallpaper rounded and skips the mask).
- The minimap shape is drawn by `MinimapFace` (src/Aetherphone/Windows/Components/Chrome/MinimapFace.cs) from `MinimapReader` (src/Aetherphone/Core/Maps/MinimapReader.cs). The reader resolves the current map every half second (`AgentMap.CurrentMapId`, falling back to the territory's `TerritoryType.Map`), reads `SizeFactor` and the offsets from the `Map` row once per map, and each frame converts the player's world position to a canvas pixel through `MapPixelMath.ToCanvasPixel`, the exact inverse of `ToWorldCoordinate`. The face is north up: it windows the zone texture (via the shared `ZoneMapTextures` cache) to the current zoom span around the player (`MapSpan`, from 140 down to 24 yalms, 62 by default), clamps that window inside 0..1 so the wrapping sampler never tiles at a map edge, and places the player marker by mapping the player UV back into the drawn window, so near an edge the marker moves instead of the map. Coordinates reformat at 5 Hz and only when the rounded value changes. Over the map sit a header with the clock and zone name, the notification banner, and, while a call or music is live, a control pill in place of the coordinates.
- Landscape (requested through `AppLandscape` by the camera app, the AetherStream theater mode, the Strats viewer and landscape games such as Doom) turns the phone instead of reshaping it. `OrientationTurn` (src/Aetherphone/Core/Shell/OrientationTurn.cs) runs a 0.36 s ramp that `PhoneShell.PrepareFrame` advances, and the shell only ever lays out at one of the two resting sizes: the first half of the ramp draws the portrait phone, the second half the landscape one. `PhoneWindow` sizes the window to the rotated footprint, centres the device rect inside it, and after `shell.Draw` rewrites every vertex and clip rect of the window and its children through `LayerTransform.Turn`. The turn is counter-clockwise, so the portrait right rail (side button) lands on the landscape top rail and the artwork sweeps the way `CaseArt` already swaps its UVs. The landscape chassis is exactly the portrait chassis transposed and scaled by the zoom ratio, so body, rails, buttons and radii line up across the halfway swap; the screen content does not, so `OrientationTurn.ContentAlpha` fades the child layers out and back in around it, the transparent camera band is suppressed (the body paints an opaque screen to fade against), the brightness veil moves into the chrome child (the foreground seal cannot rotate), and input is shielded for the length of the turn. When the rotated footprint would not fit the viewport, the transform scales down to fit and back up as it lands. Each orientation keeps its own resting position (`Configuration.MaximizedPosition` for portrait, `Configuration.LandscapePosition` for landscape, both recorded every resting frame and persisted): the turn glides the pivot from the centre the phone left to the centre the destination orientation last rested at, so a phone that had to slide away from a screen edge to fit landscape, or was dragged while landscape, comes back to exactly where it stood in portrait. A first turn into an orientation with no remembered position pivots in place, and a reversal mid-turn restarts the glide from wherever the pivot is. The landscape width is `Configuration.LandscapePhoneWidth` when the corner grip has set one in landscape, otherwise the clamped portrait width times `PhoneSizeCatalog.LandscapeGrowth` (1.5), run through `PhoneBounds.ClampLandscapeWidth`, whose ceiling fits the long side to the viewport width and the short side to its height. The zoom is the resting zoom of whichever orientation is being drawn, so text is rasterised once per turn instead of once per frame, and the transform carries the visual growth.
- Separate maximized, minimized and landscape positions persist in `Configuration.MaximizedPosition` / `MinimizedPosition` / `LandscapePosition` via `PersistPositions()`.
- `Draw()` pushes the base font, reserves the full content region with `ImGui.Dummy`, centres a device-sized `Rect` in it (the two differ only while a turn is running), and hands it to `shell.Draw(device)`. `LastPosition` and `LastSize` report that device rect, not the window. Apart from the other window classes below, nothing else in the codebase talks to the `Window` API.

`UpdateChipWindow` (src/Aetherphone/Windows/UpdateChipWindow.cs) is a small chip shown under the phone when a plugin update is available.

`HuntsMapMarkersIndicatorWindow` (src/Aetherphone/Windows/HuntsMapMarkersIndicatorWindow.cs) is a chip pinned to the top right corner of the game's own area map (the `AreaMap` addon) while `services.HuntsMapMarkers` has hunt markers on it. It shows the instance being displayed and a legend that expands on click, and it draws only while both the markers and the map are up.

The Linkpearl pop-outs (src/Aetherphone/Windows/LinkpearlPopouts.cs, `LinkpearlPopoutWindow`) are game chat conversations detached from the phone into their own windows, a fixed pool of `LinkpearlPopouts.MaxWindows`. Their placement persists in `Configuration.LinkpearlPopouts`; a new one opens at the last remembered placement, or else at the chosen placement relative to the phone frame (`PhoneVisibility.TryGetFrame`). `PopoutPresence` hides them during combat, duties, cutscenes or a hidden game UI as the user chooses, and `LinkpearlHotkey` opens a recent conversation in one from a configurable key chord, cycling through recent conversations on repeated presses.

The Message pop-outs (src/Aetherphone/Apps/Message/MessagePopouts.cs, `MessagePopoutWindow`) do the same for Aethernet direct messages: the Message app pops a conversation out of the phone, the set of open windows persists in `Configuration.MessagePopouts`, and `AppBundle.MessagePopouts` hands the windows to `Plugin`, which restores them at boot.

`PhotoWindow` (src/Aetherphone/Windows/PhotoWindow.cs) is the photo pop-out: an ordinary resizable Dalamud window that shows one image fitted to its content region. `PhotoZoomView` draws the button that opens it (leftmost in the control row), every fullscreen photo viewer returns that click to its caller, and the caller hands `Plugin.PhotoWindow.Open` a `Func<IDalamudTextureWrap?>` plus the `IPhoneApp` it came from. The texture source means the window re-resolves from the cache every frame instead of holding a wrap that eviction could free; the app supplies the window title, read as `DisplayName` every frame so it follows a language switch. It sizes itself to the image aspect the first frame the texture resolves, then leaves the size alone.

`VideoDebugWindow` (src/Aetherphone/Windows/VideoDebugWindow.cs) is the video subsystem's decode debug panel, and `AetherStreamScreenWindow` (src/Aetherphone/Windows/AetherStreamScreenWindow.cs) is a resizable pop-out that mirrors the in-world AetherStream screen while media is playing. `VideoWorldOverlay` (src/Aetherphone/Windows/VideoWorldOverlay.cs) is not a window: it hooks `UiBuilder.Draw` directly, projects the in-world screen onto the viewport, and draws the chat bubbles, the reactions, and the drag handles shown while the screen is being placed.

## The shell layer (Core/Shell)

`PhoneWindow.PreDraw` calls `PhoneShell.PrepareFrame`, which advances the minimize morph and the orientation turn before the window is sized. `PhoneShell.Draw(Rect device)` is then the per-frame orchestrator. In order it: short-circuits into `MinimizeMorphView` while the phone is minimized or morphing, applies the notification shake offset, steps day/night wallpaper blending, computes the chassis, draws the phone body, advances `LoadingScreen`, `NavigationStack` and the `AppSwitcher`, sends the user home if a suspension now blocks the open app, advances the banner, polls `InstallSourceNotice`, handles the four hardware keys laid out like an iPhone 17 Pro (Side button for minimize/close, Action button for do-not-disturb and a Lock Position key where the volume rocker sits, both confirmed by a Dynamic Island notice, Camera Control to open Camera) along with the bezel double-click that also minimizes and the corner resize grip, opens the Message call screen when a call connects, asks `ShellOverlayCoordinator.Assess` who owns the pointer, advances the onboarding director and its `UiAnchors`, draws the screen content, then the chrome, then the overlays, and last the live glass bezel band (`DeviceChrome.DrawLiveBand`). The shell does not advance calls: `Plugin.OnCallsTick` does that on the framework tick (see [Frame loop and threads](#frame-loop-and-threads)), so a ringing or dialing call times out even while the phone is minimized or closed.

The main members of the shell's cast, all in `src/Aetherphone/Core/Shell/`:

| Type | Role |
| --- | --- |
| `PhoneShell` | Owns and sequences everything below |
| `HomeScreen` | App grid: pages, dock, folders, widgets, edit mode |
| `StatusBar` | Clock, island cutout, signal/battery icons at the screen top |
| `ControlCenter` | Pull-down panel of control tiles plus the notification center |
| `DynamicIsland` | Live activity surface at the top of the screen: calls, watch-along sessions, playback, PC media, timers, musters, game timers and fishing, plus the do-not-disturb and lock notices |
| `IslandActivities` | Picks which live activity the island shows when several are running |
| `Spotlight/` | Home screen search (search pill or swipe down on the grid): apps, actions, contacts, conversations, settings, a calculator, and more |
| `AppSwitcher` | Hold the home indicator for live previews of open and recently closed apps; takes over the screen while active |
| `HomeIndicatorGesture` | Home indicator input: tap to go home, drag up to scrub the open app home, hold for the app switcher |
| `LiveBackdrop` | Experimental live glass (off by default, `Configuration.LiveGlass`): samples what is behind the phone (the game world or the whole screen) for glass surfaces, prepared in `PhoneWindow.PreDraw` |
| `LoadingScreen` | Boot animation that gates the UI (wraps `BootSequence`) |
| `ShellScreenPainter` | Paints home or one app into the screen rect |
| `ShellTransitionRenderer` | Composites the app open/close motion |
| `ShellOverlayCoordinator` | Decides overlay visibility, z-order, and pointer capture |
| `OrientationTurn` | Portrait/landscape turn ramp |
| `MinimizeTransition` / `MinimizeMorphView` | Phone-to-mini-phone collapse state and rendering |
| `MinimizedLayoutService` / `MinimizedFeed` | Mini phone layout and its throttled widget data |
| `RateLimitPill` | Small pill shown when the backend rate limiter pushes back |
| `ShortcutRunPill` | Progress and stop button for the running shortcut, plus its outcome |
| `CoinEarnPill` | Pill under the island announcing coins just earned |
| `CoinEarnFloats` | Floating coin bursts drawn over the screen when coins land |

### Loading screen

When the window opens full size, `PhoneShell.OnOpened` calls `loading.BeginSession()` (reopening into the minimized phone skips the boot). `LoadingScreen` wraps `BootSequence` (src/Aetherphone/Core/Animation/BootSequence.cs), which plays the power-on animation and, at the emblem hold, waits until `Plugin.Fonts.Ready` reports every font handle built (capped at `BootTiming.FontWaitCapSeconds`, 60 seconds). The result: the UI never renders app content with placeholder glyphs on first open. `FontService.OnLanguageChanged` calls `loading.Show()` to replay the short variant while the atlas rebuilds for a new language.

### Overlays and z-order

The overlay model is plain ImGui: **later draw calls appear on top**, so the call order in `ShellOverlayCoordinator.DrawOverlays` is the z-order. From bottom to top: notification banner, dynamic island, shortcut run pill, coin earn pill, rate limit pill, alarm overlay, incoming call overlay, app switcher, control center, tooltips and toasts (`HoverTooltip.Flush`, `ShellToast`), share sheet, report overlay, confirm overlay, onboarding director, conduct gate, encryption help overlay, ban overlay, coin earn floats. Three special cases sit outside that list:

- While `LoadingScreen.IsActive`, `DrawOverlays` draws the boot screen and returns early, so nothing else can appear above it.
- While the account setup flow is active, `SetupOverlay` draws first and, once past boot, the frame short-circuits to just tooltips, toasts, and the ban and confirm overlays above it; everything else is skipped.
- `DeviceChrome.SealScreen` runs last among the overlays. It draws the screen corner mask and the brightness veil on `ImGui.GetForegroundDrawList()`, which renders above every ImGui window, so no content can ever poke outside the rounded screen. It is skipped during an orientation turn (the veil moves into the chrome child, because the foreground seal cannot rotate). After it, `PhoneShell.Draw` paints only `DeviceChrome.DrawLiveBand`, the live glass bezel band outside the screen.

The banner and the three pills all sit in the same strip under the island, so they take turns rather than stack: the notification banner wins, then `ShortcutRunPill`, then `CoinEarnPill`, and `RateLimitPill` draws only when all three are hidden. `ShortcutRunPill` is the only pill that takes input (its stop button), so it joins the banner and the island in the pointer-capture term that `Assess` folds into `IslandCaptures`. None of the strip, island included, draws while the control center or the app switcher is open, and the strip plus the alarm and incoming call overlays also step aside while the onboarding director holds the pointer.

`Assess` runs before content each frame and returns a `ShellOverlayState` (`Busy`, `ShieldBase`, and friends). `PhoneShell` wraps content drawing in `InputShield.Engage(...)` (src/Aetherphone/Core/Animation/InputShield.cs) so that when any overlay owns the pointer, the layers underneath stop reacting to hover and clicks even though they still draw.

### Transitions

`NavigationStack` owns app open/close motion: `BeginPresent`/`BeginDismiss` launch a spring toward a cover value with an initial kick (`TransitionTiming.LaunchVelocity`, half the spring's natural frequency, so the first frame already moves and the curve decelerates like a native app launch), `IsTransitioning` flips true, and `PhoneShell.DrawContent` delegates to `ShellTransitionRenderer`, which paints the outgoing and incoming screens (home zoom for home-to-app, vertical slide-over for app-to-app) via `ShellScreenPainter`. The motion step is clamped to `MotionFrameSeconds` (33 ms) so a frame hitch reads as a slightly slower animation, never a skip; the spring finalizes within `MotionSettleEpsilon` of the target and `FinalizeMotion` fires `OnClosed` on the app that left.

The home indicator drives the same motion by hand. `HomeIndicatorGesture` turns a tap into `GoHome`, an upward drag into `NavigationStack.Scrub` (the open app follows the finger toward its icon), and the release into `ReleaseScrub`, which either finishes the trip home or springs the app back. Holding the indicator opens the `AppSwitcher`, whose cards come from `NavigationStack.CollectOpen`: the open app plus recently closed apps that implement `IResumableApp`. While the switcher is active, `PhoneShell.DrawContent` hands the screen to `AppSwitcher.DrawStage` instead of the transition renderer or the painter. Reopening an `IResumableApp` within the resume window (`ResumeWindowMilliseconds` in `NavigationStack`) calls `OnResumed` instead of `OnOpened`, so the app can pick up where it was.

Every screen, at rest or mid transition, is painted into a `ScreenLayer` stage (src/Aetherphone/Core/Animation/ScreenLayer.cs): one child window at the screen rect whose ID chain is `<app id>/stage` (or `home/stage`). The renderer never moves or shrinks that window; it paints the screen at its resting layout and then post-transforms the stage's draw lists (`LayerTransform`: uniform scale, translate, alpha, clip) after the stage ends, walking the stage window and every child it begun this frame. That keeps the app's ImGui windows identical across the transition boundary (scroll offsets, focus and per-window state survive), keeps layout reads such as content width correct during the motion, and lets the app zoom out of its icon as a scaled live snapshot the way iOS does. The home stage recedes the same way (scaled about the tapped icon, then dimmed), with the launching tile hidden via `HomeMotion.RevealAppId` while the card stands in for it. `SceneCompositor` (used by `ViewRouter` pushes) is the same mechanism with a translate-only transform. The phone-to-mini-phone minimize is a separate state machine (`MinimizeTransition`, phases `None`, `Collapsing`, `Minimized`, `Expanding`) rendered by `MinimizeMorphView`, which scales the live screen into the mini phone through `ShellScreenPainter.PaintCurrentTransformed`.

## How an app gets drawn each frame

The full path from Dalamud to one app, every frame while that app is open:

1. Dalamud fires `UiBuilder.Draw`, which runs `windowSystem.Draw`.
2. `WindowSystem` calls `PhoneWindow.Draw`, which computes the device `Rect` and calls `PhoneShell.Draw(device)`.
3. `PhoneShell.Draw` computes `ChassisGeometry`, draws the body, and calls `DrawContent`.
4. Not transitioning and no app switcher on screen: `ShellScreenPainter.PaintCurrent` checks `navigation.Current`. With no app open it paints wallpaper, scrim, and `HomeScreen.Draw`; otherwise `PaintApp` runs for that app.
5. `PaintApp` resolves the app's theme, fills the screen background unless the app sets `WantsTransparentScreen`, insets the screen into a content `Rect`, and calls the app:

```csharp
var contentRect = ContentRect(screen, theme);
try
{
    using (AppVisits.Enter(app.Id))
    {
        app.Draw(new PhoneContext(contentRect, content, navigation));
    }
}
catch (Exception exception)
{
    AepLog.Error(exception, $"[shell] app-draw {app.Id} threw");
    DrawAppFailure(contentRect, content);
}
```

`PhoneContext` (src/Aetherphone/Core/Apps/PhoneContext.cs) is everything an app receives: the content `Rect` it may draw in, the resolved `PhoneTheme`, and an `INavigator` for navigation. `IPhoneApp` is deliberately small: identity (`Id`, `DisplayName`, `Glyph`), `BadgeCount`, lifecycle (`OnOpened`, `OnClosed`), share hooks, and `Draw`, plus defaulted opt-ins (`Accent`, badge style, `WantsTransparentScreen`, `WantsSystemTheme`, `TransparentViewport`, `IsAvailable`) and `Dispose`, since it extends `IDisposable`. The registry in `AppRegistry.BuildDefault` is a plain ordered list; there is no dynamic discovery. See [App framework](app-framework.md) for the contract in depth, and [Creating an app](creating-an-app.md) for a tutorial that builds one from zero.

Apps are opened through `NavigationStack.Open(appId)` (string id, checks `AppInstaller.IsInstalled` and `IsAvailable`) or `OpenApp`/`OpenAppFrom` (direct reference, used by the home grid to zoom from a tile's `Rect`). `Back()` pops the history stack; `GoHome()` clears it. `SuspensionGate` (src/Aetherphone/Core/Moderation/SuspensionGate.cs) can veto opening socially-connected apps for suspended accounts.

## Device chrome and screen geometry

All layout is done in absolute screen coordinates using `Rect` (src/Aetherphone/Core/Rect.cs), a `readonly record struct` of `Min`/`Max` vectors with `Width`, `Height`, `Size`, `Center`, `Inset`, `Translate`, and `Contains`. There is no layout engine: parents compute child rects and pass them down.

`ChassisGeometry.Device(window, theme, scale)` turns the window rect into three nested, pixel-snapped rects with matching corner radii: `Body` (the metal frame), `Glass` (the bezel), and `Screen` (where content lives). `DeviceChrome` (src/Aetherphone/Windows/Components/Chrome/DeviceChrome.cs) renders them as squircles, plus the hardware key slots (`KeyRect`, one fractional placement per `HardwareKey`), the antenna lines on the metal band, the wallpaper, and `SealScreen`. Each slot spans the full rail gutter so the hit target stays large, while `HardwareButton` paints only the proud part (under half the gutter) as a frame-coloured pill. Camera Control is the exception: a sapphire cap tinted from the theme glass, standing less proud than the metal keys.

Two scale factors are in play and they multiply, which is what `UiScale.Current` returns:

- **`UiScale.Global`** is Dalamud's global UI scale, straight from `ImGuiHelpers.GlobalScale`.
- **`UiScale.Phone`** is the phone zoom, `Configuration.PhoneWidth / 360`. The whole UI is authored against a 360 wide phone and rendered larger or smaller as one unit, so a 720 wide phone is the same layout at 2x, not a bigger phone showing more rows.

Every hardcoded design unit in draw code is multiplied by `UiScale.Current` at draw time (`44f * UiScale.Current` and similar throughout the shell). **`UiScale.cs` is the only file allowed to read `ImGuiHelpers.GlobalScale`,** and a CI guard enforces it. Text follows the same zoom through `FontService`, which folds it into `ImFont.Scale` alongside the text zoom setting, so sizes stay exact with no atlas rebuild.

Because the zoom already scales everything at draw time, **`ChassisMetrics` is built from the fixed design width (360), never the live width.** It sizes bezels as a fraction of the width it is handed, and that result is then multiplied by `UiScale.Current`, so passing the live width would double count the zoom and grow bezels quadratically.

`PhoneScalingTests` (src/Aetherphone.Tests/PhoneScalingTests.cs) asserts chassis geometry, screen aspect, and home grid metrics stay proportional to phone width across a spread of widths and global scales. If a change breaks proportionality, those fail.

Apps do not see any of this: they receive a ready-made content `Rect` already inset by the theme's top zone (status bar) and bottom zone (home indicator) via `ShellScreenPainter.ContentRect`.

## Configuration

`Configuration` (src/Aetherphone/Configuration.cs) implements Dalamud's `IPluginConfiguration`: one serializable class holding every persisted setting, from window positions to market favorites to notification preferences, saved as JSON in the Dalamud config directory. `Save()` is thread-safe (marshals to the framework thread); `SaveNow()` writes synchronously on the spot, used on shutdown paths like `PhoneWindow.PersistPositions` and wherever losing the change would hurt (`PhotosApp` saves album edits with it).

Migrations happen in two stages at boot:

1. `ConfigMigrations.Run` operates on the raw JSON text before deserialization, rewriting fully-qualified type names that moved between namespaces. It writes a one-time `.pre-migration.bak` backup next to the config file.
2. The `Migrate*` instance methods on `Configuration` (called from the `Plugin` constructor) reshape deserialized data: merging legacy app ids on the home layout, upgrading sound tokens, moving account tokens into per-character sessions, and so on. All of them run on every boot and are idempotent: most check a one-time flag, the rest check the data they would change and do nothing when it is already current.

See [State and persistence](state-and-persistence.md) for per-character data and media storage.

## Core directory map

One line per subfolder of `src/Aetherphone/Core/`. The few files at the root are `AepConstants.cs` (name, commands, URLs), `AepLog.cs` (logging wrapper), `FontService.cs`, `IconPlan.cs` (icon font codepoint plan), `PhoneServices.cs`, `RealtimeSignalBus.cs` (realtime fan-out), and `Rect.cs`.

| Folder | What lives there |
| --- | --- |
| Activity | Play-session tracking, activity rings, goals, EXP ledger |
| Aethernet | HTTP client, session, and typed API surface for the backend |
| Animation | Easing, springs, boot sequence, input shield, kinetic scrolling |
| Announcements | Deep-link launcher state for the admin Announcements app |
| Apps | App contracts and plumbing: `IPhoneApp`, `AppRegistry`, `NavigationStack`, launchers |
| Audio | `AudioOutputFactory`: WASAPI output with a waveOut fallback |
| Calculator | Calculator history records |
| Calendar | Custom calendar event records |
| Casino | Casino game stores and rules: rooms, tables, rounds, spins, per-game rules, the verifier |
| Changelog | In-app changelog entries and version data |
| Clock | Alarm and world clock records |
| Coins | Coin wallet: balance and ledger store, catalog, earn notifier, game session tracker |
| Collections | Collectible catalog service and unlock models |
| Conduct | Per-app conduct rules acknowledgement gate |
| Config | `ConfigMigrations` (raw config JSON rewrites) and `SettingsSnapshotStore` |
| Confirm | `ConfirmService` behind the shell confirmation dialog |
| Contacts | In-game friend list reading and actions |
| ControlCenter | Control tile registry, layout, and gallery data (drawing is in Shell) |
| Crypto | End-to-end encryption: key vault, conversation keys, envelope codec |
| Dailies | Daily and weekly checklist catalog and stores |
| Device | `DeviceStatus`: battery, latency, and signal sampling for the status bar |
| Emoji | Twemoji catalog, atlas images, and text scanner |
| Feedback | Feedback launcher and update marks for the Feedback app |
| Fishing | Timed fish catalog, fishing windows and alerts, ocean voyage itinerary and reminders |
| Game | Game data access: `GameData`, `CharacterWatch`, Eorzea time, weather, nameplate stripping, player actions |
| GameChat | Game chat bridge: capture, inbox, tabs, archive, channels, send; plus the legacy `ChatLine` and on-disk `MessageArchive`, the phone emote (`PhoneEmoteController`), and the Linkpearl pop-out presence and hotkey |
| Games | Mini-game statistics and multiplayer game rooms |
| Geography | World, data center and region tables (`WorldGeography`) |
| Health | Wellness tracker models and store |
| Home | Home layout model: `AppInstaller`, `AppGate`, grid solver, tiles |
| Honorific | Nameplate titles (`NameplateTitleService`) and the Honorific plugin IPC bridge |
| Housing | Open housing plot listings via the Aetherphone housing service (a proxy serving PaissaDB data) and the China region source (house.ffxiv.cyou): districts, watches, reminders |
| Hunts | Hunt tracker: Faloop client and realtime feed, mob catalogs and lore, spawn windows, train routes |
| Input | `DragTracker` pointer gesture helper |
| Inventory | Inventory capture, model, and search |
| Jam | Shared listening sessions (Jam): host, guest and nearby modes, invites, drift control |
| Jobs | Gearset reading, job categories, custom colors |
| Localization | `Loc`/`L` string catalog behind the nine language JSONs |
| Lodestone | Lodestone character lookup and portrait service |
| Lyrics | Synced lyrics from LRCLIB: client, matching, LRC parsing |
| Maps | Map data and location sharing, plus the minimap reader, zone map textures, hunt map markers and the travel planner |
| Market | Market board service, item index, alerts |
| Media | Remote image caches and image processing |
| Message | Shared chat store base types |
| Moderation | Moderation notices, `SuspensionGate`, safety launcher |
| Muster | Meetup app stores, codes, and chat bridge |
| Net | `HttpService`, disk caches, throttles, retry gates |
| News | Lodestone news client |
| Notes | Note and reminder records |
| Notifications | `NotificationService`, router, channels, alarm and reminder tickers |
| Onboarding | Welcome tour: `OnboardingDirector`, guide steps, anchors |
| Photos | `PhotoLibrary`, screenshot import, PNG writer |
| Platform | File picker dialogs, the server info bar entry (`ServerBarEntry`), clipboard and game window helpers, `SupportInfo` |
| Playback | `PlaybackHub` coordinating radio and song playback |
| Radio | Internet radio client and player, plus community stations and shared radio rooms |
| Report | Central report popup service |
| Rolladeck | Rolladeck client: live DJs and open venues |
| Runtime | Framework thread plumbing: `FrameworkTicker`, `PendingFrameworkAction`, `PhoneVisibility`, `PollCadence` (refresh pacing) |
| Sharing | `ShareService` and share item types |
| Shell | The shell layer covered above |
| Shortcuts | Shortcuts app data: entries, macros, share codes, the runner, plugin command catalog, the custom icon library |
| Social | Shared social app models: feeds, reactions, mentions, tagging |
| Songs | Song search, audio streaming, playlists |
| Strats | Strats app data: guide manifest and guide stores, territory lookup |
| SystemMedia | Windows media sessions: PC media from other apps (`WindowsMediaSessions`) and publishing the phone's own playback to Windows (`WindowsMediaPublisher`) |
| Telephony | `CallHub` voice calls and call audio |
| Theme | `PhoneTheme`, accents, chassis metrics and geometry |
| Timers | Game timers: retainer ventures and workshop voyages (`GameTimers`, `TimerLedger`, `GameTimerCapture`) |
| Translation | `TranslationService`: batched content translation through the Aethernet API |
| Updates | Plugin update check against the manifest |
| Venues | Venue listing service and Lifestream bridge |
| Video | mpv video engine, in-world screen and its placement, AetherStream queue and library, watch-along session, chat bubble feed |
| Wallet | Currency reading, `WalletService`, and wallet history |
| Wallpapers | Wallpaper library, crops, image cache |
| YellowPages | Ads app stores, categories, chat bridge |

## Gotchas

- **The plugin constructor can run off the game's main thread.** Never read `IObjectTable.LocalPlayer` (or other live game state) in it. `Plugin` follows this rule: auto-open subscribes `OnAutoOpenTick` to `Framework.Update` and only reads `ObjectTable.LocalPlayer` there, and `DeviceStatus` defers its player lookup to `SyncTarget()`, called from `StatusBar.Draw`.
- **`Window.Size` scales by `UiScale.Global`, `Window.Position` does not.** Use `Global` here, not `Current`: the size handed to `Window.Size` already carries the phone zoom, so `Current` would apply it twice. Mixing these up puts the window in the wrong place at any UI scale other than 100% or any phone size other than 360. See the centering math in `PhoneWindow.PreDraw`.
- **An app that throws in `Draw` does not crash the plugin, but it logs every frame.** `ShellScreenPainter.PaintApp` catches per frame and paints a failure message, so a broken app looks "stuck" while flooding the log. Check the Dalamud log for `[shell] app-draw` lines.
- **Nothing inside the screen outdraws `DeviceChrome.SealScreen`.** It renders the corner mask and brightness veil on ImGui's foreground draw list, which sits above all windows; only `DeviceChrome.DrawLiveBand` follows it, and that paints the bezel band outside the screen. During an orientation turn the seal is skipped and the veil moves into the chrome child. If your overlay must be visible, it has to be drawn inside `ShellOverlayCoordinator.DrawOverlays` before `SealScreen`, and content must stay inside the screen rect.
- **`Configuration.Save()` is asynchronous from non-framework threads.** It fire-and-forgets onto the framework thread. When the plugin may be gone before that runs, or losing the change would hurt, use the synchronous `SaveNow()` as `PhoneWindow.PersistPositions` and `PhotosApp`'s album edits do.
- **A `FrameworkTicker` with an `AppGate` silently does nothing while its app is uninstalled.** If your periodic service "never runs", check the gate id passed to `AppInstaller.Gate` before debugging the timer.
- **The boot screen holds up to 60 seconds for fonts.** `BootSequence` waits at the emblem until `Plugin.Fonts.Ready`, capped by `BootTiming.FontWaitCapSeconds`. A long first boot usually means the font atlas is still building, not a hang.

## Related docs

- [Getting started](getting-started.md): build, load the dev plugin, Dalamud and ImGui primer
- [App framework](app-framework.md): the `IPhoneApp` contract, registry, navigation, skins, badges
- [Creating an app](creating-an-app.md): step-by-step tutorial building a new phone app from zero
- [UI toolkit](ui-toolkit.md): the Windows/Components widget library, typography, input handling
- [State and persistence](state-and-persistence.md): configuration, migrations, per-character data
- [Game integration](game-integration.md): Dalamud services, framework thread, Lumina sheets
- [Networking](networking.md): the Aethernet client, realtime signals, auth
- [Messaging and chat](messaging-and-chat.md): the shared chat stack, message model, Linkpearl bridge
- [Notifications](notifications.md): the client notification pipeline from creation to banner, sound, and deep link
