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
| src/Aetherphone/Core/FrameworkTicker.cs | Interval-throttled work on the game framework tick |
| src/Aetherphone/Windows/PhoneWindow.cs | The one borderless ImGui window the phone lives in |
| src/Aetherphone/Core/Shell/PhoneShell.cs | Per-frame orchestrator: chassis, content, chrome, overlays |
| src/Aetherphone/Core/Shell/ShellOverlayCoordinator.cs | Overlay z-order and pointer-capture arbitration |
| src/Aetherphone/Core/Shell/ShellScreenPainter.cs | Paints home or the active app into the screen rect |
| src/Aetherphone/Core/Apps/NavigationStack.cs | Which app is open, history, present/dismiss motion |
| src/Aetherphone/Core/Apps/IPhoneApp.cs | The contract every phone app implements |
| src/Aetherphone/Core/Apps/AppRegistry.cs | Builds the list of every app at boot |
| src/Aetherphone/Windows/Components/DeviceChrome.cs | Draws the physical phone body, glass, and screen |
| src/Aetherphone/Core/Theme/ChassisGeometry.cs | Body/Glass/Screen rectangles and corner radii |
| src/Aetherphone/Core/Rect.cs | The tiny rectangle struct the whole UI is measured in |
| src/Aetherphone/Configuration.cs | All persisted settings, one Dalamud plugin config object |
| src/Aetherphone/Core/ConfigMigrations.cs | Rewrites legacy type names in the config JSON before load |

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

1. `ConfigMigrations.Run(PluginInterface.ConfigFile)` rewrites legacy type names inside the raw config JSON, then `PluginInterface.GetPluginConfig()` deserializes `Configuration` and the `Migrate*` methods on it run (sounds, changelog, messages merge, character sessions, and more).
2. `InitializeLocalization()` picks a language (game client language, then OS language, then English) and loads the string catalog.
3. `Device = new DeviceStatus(...)` starts the battery/latency/signal sampler used by the status bar.
4. `PhoneServices.Build(...)` constructs every shared service (next section).
5. `Fonts = new FontService(...)` builds the Inter font atlas at every weight and size bucket.
6. `EmojiCatalog.Load()` runs, then the video subsystem comes up: `ScreenController`, `VideoPlayer`, `AetherStreamQueue`, `WatchAlongSession`, and `StreamSuggestionNotifier` are constructed, `OnVideoFrameworkUpdate` subscribes to `Framework.Update`, and `VideoDebugWindow` and `AetherStreamScreenWindow` are built.
7. `AppRegistry.BuildDefault(services, video, screenController, videoQueue, watchAlong, streamSuggestions, screenWindow)` constructs every app into an `AppBundle` (apps, home widgets, photo library).
8. `new PhoneShell(services, bundle)` and `new PhoneWindow(shell, Cfg)` build the UI, and the five windows (`PhoneWindow`, `UpdateChipWindow`, `PhotoWindow`, `VideoDebugWindow`, `AetherStreamScreenWindow`) are added to a Dalamud `WindowSystem`, the helper that tracks window open state and calls each window's draw methods.
9. Background services start: `PhoneEmoteController`, `TimerNotifier`, `CalendarReminderService`, `ClockAlarmService`, `ReminderService`, `ScreenshotImportService`, character session watchers, and `CallHub`.
10. Chat commands `/phone` and `/aetherphone` (see `AepConstants`), a server info bar entry (`IDtrBar`), and a context menu hook are registered.
11. `PluginInterface.UiBuilder.Draw += windowSystem.Draw` wires the whole UI into Dalamud's ImGui frame.

If any step throws, the catch block calls `TearDownPartialConstruction()` so a half-built plugin never leaks event subscriptions, then rethrows so Dalamud reports the load failure.

A handful of statics are exposed for hot paths that would otherwise thread a parameter through dozens of constructors: `Plugin.Cfg`, `Plugin.Fonts`, `Plugin.Wallpapers`, `Plugin.Device`, `Plugin.Updates`, `Plugin.PhotoWindow`, `Plugin.Instance`. Everything else flows through constructor injection.

## Service composition (PhoneServices.cs)

`PhoneServices` is not a service locator or DI container. It is a single composition root: one class with `required` init-only properties, built exactly once by the static `PhoneServices.Build(...)` factory inside the `Plugin` constructor. `Build` news up every service in dependency order (notification pipeline, HTTP and caches, Aethernet session and API, market, music, telephony, and so on) and returns the filled object.

Consumers never "look up" services at runtime. `AppRegistry.BuildDefault(...)` passes each app exactly the services it needs through its constructor, and `PhoneShell` does the same for shell components. `PhoneServices.Dispose()` tears everything down in reverse dependency order when the plugin unloads.

`PhoneVisibility` (src/Aetherphone/Core/PhoneVisibility.cs) deserves a note: it is a one-field indirection that services use to ask "is the phone on screen right now?". The `Plugin` constructor binds it to the window state:

```csharp
services.Visibility.Bind(() => phoneWindow is { IsOpen: true, IsMinimized: false });
```

Services built before the window exists can hold `PhoneVisibility` and probe it later, which breaks what would otherwise be a construction-order cycle.

## Frame loop and threads

Two Dalamud callbacks drive everything:

- **`IFramework.Update`** fires once per game tick on the game's framework (main) thread. This is where anything that reads game memory must run: `IObjectTable`, `IClientState` player data, condition flags. Background services (activity tracking, alarms, inventory capture, health sampling) live here.
- **`PluginInterface.UiBuilder.Draw`** fires once per rendered frame while ImGui is building its draw data. `Plugin` subscribes `windowSystem.Draw` here, which calls `PhoneWindow.PreDraw`, `Draw`, and `PostDraw`. All ImGui calls must happen inside this callback.

`FrameworkTicker` (src/Aetherphone/Core/FrameworkTicker.cs) is the standard way to do periodic work on the framework thread. It subscribes to `Framework.Update`, skips ticks until `intervalMilliseconds` has elapsed, and honors an `AppGate` (src/Aetherphone/Core/Home/AppGate.cs) so work for an uninstalled app never runs:

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

`ActivityTracker`, `HealthTracker`, `InventoryCaptureService`, `TimerNotifier`, `ReminderService`, `ClockAlarmService`, and `CalendarReminderService` all run on `FrameworkTicker` instances.

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
- Minimized mode swaps the size to `MinimizeTransition.MinimizedSize`, a fixed 78 by 152 puck that deliberately ignores the phone zoom, and lerps the position between the saved maximized and minimized spots while the morph runs.
- Landscape (requested through `AppLandscape` by the camera app and the AetherStream theater mode) animates a blend between the portrait size and its transpose in `OrientedSize()`; the content is transposed, never rotated.
- Separate maximized and minimized positions persist in `Configuration.MaximizedPosition` / `MinimizedPosition` via `PersistPositions()`.
- `Draw()` pushes the base font, reserves the full content region with `ImGui.Dummy`, wraps the region in a `Rect`, and hands it to `shell.Draw(device)`. Apart from the four other windows below, nothing else in the codebase talks to the `Window` API.

`UpdateChipWindow` (src/Aetherphone/Windows/UpdateChipWindow.cs) is a small chip shown under the phone when a plugin update is available.

`PhotoWindow` (src/Aetherphone/Windows/PhotoWindow.cs) is the photo pop-out: an ordinary resizable Dalamud window that shows one image fitted to its content region. `PhotoZoomView` draws the button that opens it (leftmost in the control row), every fullscreen photo viewer returns that click to its caller, and the caller hands `Plugin.PhotoWindow.Open` a `Func<IDalamudTextureWrap?>` plus the `IPhoneApp` it came from. The texture source means the window re-resolves from the cache every frame instead of holding a wrap that eviction could free; the app supplies the window title, read as `DisplayName` every frame so it follows a language switch. It sizes itself to the image aspect the first frame the texture resolves, then leaves the size alone.

`VideoDebugWindow` (src/Aetherphone/Windows/VideoDebugWindow.cs) is the video subsystem's decode debug panel, and `AetherStreamScreenWindow` (src/Aetherphone/Windows/AetherStreamScreenWindow.cs) is a resizable pop-out that mirrors the in-world AetherStream screen while media is playing.

## The shell layer (Core/Shell)

`PhoneShell.Draw(Rect device)` is the per-frame orchestrator. In order it: advances the minimize morph (and short-circuits into `MinimizeMorphView` when the phone is a puck), applies the notification shake offset, steps day/night wallpaper blending, computes the chassis, draws the phone body, advances `LoadingScreen`/`NavigationStack`/banner/calls, handles the three physical side buttons (minimize/close, do-not-disturb, position lock), asks `ShellOverlayCoordinator.Assess` who owns the pointer, draws the screen content, then the chrome, then the overlays.

The shell's cast, all in `src/Aetherphone/Core/Shell/`:

| Type | Role |
| --- | --- |
| `PhoneShell` | Owns and sequences everything below |
| `HomeScreen` | App grid: pages, dock, folders, widgets, edit mode |
| `StatusBar` | Clock, island cutout, signal/battery icons at the screen top |
| `ControlCenter` | Pull-down panel of control tiles plus the notification center |
| `DynamicIsland` | Live call and music activity pill at the top of the screen |
| `LoadingScreen` | Boot animation that gates the UI (wraps `BootSequence`) |
| `ShellScreenPainter` | Paints home or one app into the screen rect |
| `ShellTransitionRenderer` | Composites the app open/close motion |
| `ShellOverlayCoordinator` | Decides overlay visibility, z-order, and pointer capture |
| `MinimizeTransition` / `MinimizeMorphView` | Phone-to-puck collapse state and rendering |
| `RateLimitPill` | Small pill shown when the backend rate limiter pushes back |
| `ShortcutRunPill` | Progress and stop button for the running shortcut, plus its outcome |
| `CoinEarnPill` | Pill under the island announcing coins just earned |
| `CoinEarnFloats` | Floating coin bursts drawn over the screen when coins land |

### Loading screen

When the window opens full size, `PhoneShell.OnOpened` calls `loading.BeginSession()` (reopening into the minimized puck skips the boot). `LoadingScreen` wraps `BootSequence` (src/Aetherphone/Core/Animation/BootSequence.cs), which plays the power-on animation and, at the emblem hold, waits until `Plugin.Fonts.Ready` reports every font handle built (capped at `BootTiming.FontWaitCapSeconds`, 60 seconds). The result: the UI never renders app content with placeholder glyphs on first open. `FontService.OnLanguageChanged` calls `loading.Show()` to replay the short variant while the atlas rebuilds for a new language.

### Overlays and z-order

The overlay model is plain ImGui: **later draw calls appear on top**, so the call order in `ShellOverlayCoordinator.DrawOverlays` is the z-order. From bottom to top: notification banner, dynamic island, shortcut run pill, coin earn pill, rate limit pill, incoming call overlay, control center, share sheet, report overlay, confirm overlay, onboarding director, conduct gate, ban overlay, coin earn floats. Three special cases sit outside that list:

- While `LoadingScreen.IsActive`, `DrawOverlays` draws the boot screen and returns early, so nothing else can appear above it.
- While the account setup flow is active, `SetupOverlay` draws first and, once past boot, the frame short-circuits to just the ban and confirm overlays above it; everything else is skipped.
- `DeviceChrome.SealScreen` always runs last. It draws the screen corner mask and the brightness veil on `ImGui.GetForegroundDrawList()`, which renders above every ImGui window, so no content can ever poke outside the rounded screen.

The banner and the three pills all sit in the same strip under the island, so they take turns rather than stack: the notification banner wins, then `ShortcutRunPill`, then `CoinEarnPill`, and `RateLimitPill` draws only when all three are hidden. `ShortcutRunPill` is the only one of the four that takes input (its stop button), so it joins the banner in the pointer-capture term that `Assess` folds into `IslandCaptures`.

`Assess` runs before content each frame and returns a `ShellOverlayState` (`Busy`, `ShieldBase`, and friends). `PhoneShell` wraps content drawing in `InputShield.Engage(...)` (src/Aetherphone/Core/Animation/InputShield.cs) so that when any overlay owns the pointer, the layers underneath stop reacting to hover and clicks even though they still draw.

### Transitions

`NavigationStack` owns app open/close motion: `BeginPresent`/`BeginDismiss` drive a spring toward a cover value, `IsTransitioning` flips true, and `PhoneShell.DrawContent` delegates to `ShellTransitionRenderer`, which paints the outgoing and incoming layers (home zoom for home-to-app, vertical slide-over for app-to-app) via `ShellScreenPainter`. When the spring settles, `FinalizeMotion` fires `OnClosed` on the app that left. The phone-to-puck minimize is a separate state machine (`MinimizeTransition`, phases `None`, `Collapsing`, `Minimized`, `Expanding`) rendered by `MinimizeMorphView`.

## How an app gets drawn each frame

The full path from Dalamud to one app, every frame while that app is open:

1. Dalamud fires `UiBuilder.Draw`, which runs `windowSystem.Draw`.
2. `WindowSystem` calls `PhoneWindow.Draw`, which computes the device `Rect` and calls `PhoneShell.Draw(device)`.
3. `PhoneShell.Draw` computes `ChassisGeometry`, draws the body, and calls `DrawContent`.
4. Not transitioning: `ShellScreenPainter.PaintCurrent` checks `NavigationStack.AtHome`. Home paints wallpaper, scrim, and `HomeScreen.Draw`; otherwise `PaintApp` runs for `navigation.Current`.
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

`PhoneContext` (src/Aetherphone/Core/Apps/PhoneContext.cs) is everything an app receives: the content `Rect` it may draw in, the resolved `PhoneTheme`, and an `INavigator` for navigation. `IPhoneApp` is deliberately small: identity (`Id`, `DisplayName`, `Glyph`), `BadgeCount`, lifecycle (`OnOpened`, `OnClosed`), share hooks, and `Draw`. The registry in `AppRegistry.BuildDefault` is a plain ordered list; there is no dynamic discovery. See [App framework](app-framework.md) for the contract in depth, and [Creating an app](creating-an-app.md) for a tutorial that builds one from zero.

Apps are opened through `NavigationStack.Open(appId)` (string id, checks `AppInstaller.IsInstalled` and `IsAvailable`) or `OpenApp`/`OpenAppFrom` (direct reference, used by the home grid to zoom from a tile's `Rect`). `Back()` pops the history stack; `GoHome()` clears it. `SuspensionGate` (src/Aetherphone/Core/Moderation/SuspensionGate.cs) can veto opening socially-connected apps for suspended accounts.

## Device chrome and screen geometry

All layout is done in absolute screen coordinates using `Rect` (src/Aetherphone/Core/Rect.cs), a `readonly record struct` of `Min`/`Max` vectors with `Width`, `Height`, `Size`, `Center`, `Inset`, `Translate`, and `Contains`. There is no layout engine: parents compute child rects and pass them down.

`ChassisGeometry.Device(window, theme, scale)` turns the window rect into three nested, pixel-snapped rects with matching corner radii: `Body` (the metal frame), `Glass` (the bezel), and `Screen` (where content lives). `DeviceChrome` (src/Aetherphone/Windows/Components/DeviceChrome.cs) renders them as squircles, plus the side button hit rects (`SideButtonRect`, `MuteButtonRect`, `LockButtonRect`), the wallpaper, and `SealScreen`.

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
2. The `Migrate*` instance methods on `Configuration` (called from the `Plugin` constructor) reshape deserialized data: merging legacy app ids on the home layout, upgrading sound tokens, moving account tokens into per-character sessions, and so on. Each is guarded by a boolean flag so it runs once.

See [State and persistence](state-and-persistence.md) for per-character data and media storage.

## Core directory map

One line per subfolder of `src/Aetherphone/Core/`. Root-level files not listed here: `AepConstants.cs` (name, commands, URLs), `AepLog.cs` (logging wrapper), `PollCadence.cs` and `RealtimeSignalBus.cs` (refresh pacing and realtime fan-out), `AudioOutputFactory.cs`, `FontService.cs`, `FrameworkTicker.cs`, `NamePlateStripper.cs`, `PhoneServices.cs`, `PhoneVisibility.cs`, `Rect.cs`, `SupportInfo.cs`, `ConfigMigrations.cs`.

| Folder | What lives there |
| --- | --- |
| Activity | Play-session tracking, activity rings, goals, EXP ledger |
| Aethernet | HTTP client, session, and typed API surface for the backend |
| Animation | Easing, springs, boot sequence, input shield, kinetic scrolling |
| Announcements | Deep-link launcher state for the admin Announcements app |
| Apps | App contracts and plumbing: `IPhoneApp`, `AppRegistry`, `NavigationStack`, launchers |
| Calendar | Custom calendar event records |
| Casino | Casino game stores and rules: rooms, tables, rounds, spins, per-game rules, the verifier |
| Changelog | In-app changelog entries and version data |
| Clock | Alarm and world clock records |
| Coins | Coin wallet: balance and ledger store, catalog, earn notifier, game session tracker |
| Collections | Collectible catalog service and unlock models |
| Conduct | Per-app conduct rules acknowledgement gate |
| Confirm | `ConfirmService` behind the shell confirmation dialog |
| Contacts | In-game friend list reading and actions |
| ControlCenter | Control tile registry, layout, and gallery data (drawing is in Shell) |
| Crypto | End-to-end encryption: key vault, conversation keys, envelope codec |
| Dailies | Daily and weekly checklist catalog and stores |
| Device | `DeviceStatus`: battery, latency, and signal sampling for the status bar |
| Emoji | Twemoji catalog, atlas images, and text scanner |
| Emote | `PhoneEmoteController`: plays the in-game phone emote while the phone is up |
| Game | Game data access: `GameData`, `CharacterWatch`, Eorzea time, retainers |
| GameChat | Game chat bridge: capture, inbox, tabs, archive, channels, send |
| Games | Mini-game statistics store |
| Health | Wellness tracker models and store |
| Home | Home layout model: `AppInstaller`, `AppGate`, grid solver, tiles |
| Housing | Open housing plot listings via PaissaDB and the China region source (house.ffxiv.cyou): districts, watches, reminders |
| Input | `DragTracker` pointer gesture helper |
| Inventory | Inventory capture, model, and search |
| Jobs | Gearset reading, job categories, custom colors |
| Linkpearl | Legacy chat records: `ChatLine` and the on-disk `MessageArchive`; the chat bridge moved to GameChat |
| Localization | `Loc`/`L` string catalog behind the nine language JSONs |
| Lodestone | Lodestone character lookup and portrait service |
| Maps | Map data and location sharing |
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
| Platform | File picker dialogs |
| Playback | `PlaybackHub` coordinating radio and song playback |
| Radio | Internet radio client and player |
| Report | Central report popup service |
| Sharing | `ShareService` and share item types |
| Shell | The shell layer covered above |
| Shortcuts | Shortcuts app data: entries, macros, share codes, the runner, plugin command catalog |
| Social | Shared social app models: feeds, reactions, mentions, tagging |
| Songs | Song search, audio streaming, playlists |
| Telephony | `CallHub` voice calls and call audio |
| Theme | `PhoneTheme`, accents, chassis metrics and geometry |
| Updates | Plugin update check against the manifest |
| Venues | Venue listing service and Lifestream bridge |
| Video | mpv video engine, in-world screen, AetherStream queue, watch-along session |
| Wallet | Currency reading |
| Wallpapers | Wallpaper library, crops, image cache |
| YellowPages | Ads app stores, categories, chat bridge |

## Gotchas

- **The plugin constructor can run off the game's main thread.** Never read `IObjectTable.LocalPlayer` (or other live game state) in it. `Plugin` follows this rule: auto-open subscribes `OnAutoOpenTick` to `Framework.Update` and only reads `ObjectTable.LocalPlayer` there, and `DeviceStatus` defers its player lookup to `SyncTarget()`, called from `StatusBar.Draw`.
- **`Window.Size` scales by `UiScale.Global`, `Window.Position` does not.** Use `Global` here, not `Current`: the size handed to `Window.Size` already carries the phone zoom, so `Current` would apply it twice. Mixing these up puts the window in the wrong place at any UI scale other than 100% or any phone size other than 360. See the centering math in `PhoneWindow.PreDraw`.
- **An app that throws in `Draw` does not crash the plugin, but it logs every frame.** `ShellScreenPainter.PaintApp` catches per frame and paints a failure message, so a broken app looks "stuck" while flooding the log. Check the Dalamud log for `[shell] app-draw` lines.
- **Nothing outdraws `DeviceChrome.SealScreen`.** It renders the corner mask and brightness veil on ImGui's foreground draw list, which sits above all windows. If your overlay must be visible, it has to be drawn inside `ShellOverlayCoordinator.DrawOverlays` before `SealScreen`, and content must stay inside the screen rect.
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
