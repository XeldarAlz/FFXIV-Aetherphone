# App framework

This is the reference for the contract every phone app lives by: the `IPhoneApp` interface, how apps are registered and drawn, in-app navigation, theming, badges, availability, sharing, home screen placement, home widgets, Control Center tiles, cross-app launchers, and polling. Read it alongside [Creating an app](creating-an-app.md), which walks through building an app step by step; come back here whenever you need the exact semantics of a member or service.

Aetherphone renders with Dear ImGui, an immediate mode UI library: nothing is retained between frames, so every visible screen is redrawn from scratch every frame. An app is a plain C# class that the shell asks to draw itself while it is open. There is no client-server split inside the plugin; the separate Aethernet backend (a different repository) only enters this page through the feature-flag kill switch, and only its client side is documented here.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Apps/IPhoneApp.cs | The interface every app implements |
| src/Aetherphone/Core/Apps/AppRegistry.cs | Builds the one instance of every app |
| src/Aetherphone/Core/Apps/AppBundle.cs | Bundle of apps, widgets, and the photo library |
| src/Aetherphone/Core/Apps/PhoneContext.cs | Per-frame struct handed to `Draw` |
| src/Aetherphone/Core/Apps/ViewRouter.cs | In-app screen stack with slide transitions |
| src/Aetherphone/Core/Apps/NavigationStack.cs | Shell-level navigator between home and apps |
| src/Aetherphone/Core/Apps/INavigator.cs | The navigator surface apps see |
| src/Aetherphone/Core/Apps/AppAccents.cs | Single source of every app's accent color |
| src/Aetherphone/Core/Apps/RefreshCadence.cs | Tiny timer struct for periodic refresh in `Draw` |
| src/Aetherphone/Core/Aethernet/AppAvailability.cs | Server feature flags (the kill switch) |
| src/Aetherphone/Core/Sharing/ShareService.cs | Share sheet state and target matching |
| src/Aetherphone/Core/Sharing/ShareItem.cs | `ShareKind`, `ShareKindSet`, `ShareItem` |
| src/Aetherphone/Core/Home/HomeLayoutService.cs | Home pages, dock, install state, tile placement |
| src/Aetherphone/Core/Home/AppInstaller.cs | Install/uninstall facade over the layout service |
| src/Aetherphone/Core/Home/IHomeWidget.cs | The widget contract and `WidgetContext` |
| src/Aetherphone/Core/Home/WidgetRegistry.cs | Widget lookup and per-app availability |
| src/Aetherphone/Windows/Widgets/WidgetCatalog.cs | Builds the one instance of every widget |
| src/Aetherphone/Core/ControlCenter/IControlModule.cs | The Control Center tile contract |
| src/Aetherphone/Core/ControlCenter/ControlRegistry.cs | Builds every Control Center module |
| src/Aetherphone/Windows/Components/AppSkin.cs | Per-app widget painter (backdrop, cards, buttons, fields) |
| src/Aetherphone/Windows/Components/AppPalette.cs | Color set an `AppSkin` paints with |
| src/Aetherphone/Windows/Components/AppPalettes.cs | Preset palettes, one per themed app |
| src/Aetherphone/Core/Shell/ShellScreenPainter.cs | Calls your `Draw` each frame, catches exceptions |

## How an app fits into the phone

Every app is instantiated exactly once, at plugin boot, inside `AppRegistry.BuildDefault` (src/Aetherphone/Core/Apps/AppRegistry.cs). The resulting list travels in an `AppBundle` to `PhoneShell` (src/Aetherphone/Core/Shell/PhoneShell.cs), which owns them for the life of the plugin and calls `Dispose` on each one when the plugin unloads.

While your app is the current app, `ShellScreenPainter.PaintApp` (src/Aetherphone/Core/Shell/ShellScreenPainter.cs) runs every frame:

1. It picks a `PhoneTheme` with `ThemeProvider.ForApp(app.WantsSystemTheme)`.
2. Unless `WantsTransparentScreen` is true, it fills the screen with the theme's `AppBackground`.
3. It computes the content rectangle (screen minus the theme's side padding and the top and bottom zones) and calls `app.Draw(new PhoneContext(contentRect, content, navigation))`.
4. If `Draw` throws, the exception is logged and a generic failure message is drawn instead. The next frame calls `Draw` again.

## The IPhoneApp contract

The whole interface, verbatim from src/Aetherphone/Core/Apps/IPhoneApp.cs. This is the authoritative copy of the listing; other docs link here instead of repeating it.

```csharp
internal interface IPhoneApp : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Glyph { get; }
    Vector4 Accent => AppAccents.For(Id);
    int BadgeCount { get; }
    bool BadgeAsDot => false;
    bool WantsTransparentScreen => false;
    bool WantsSystemTheme => false;
    Rect? TransparentViewport(Rect screen, float scale) => null;
    bool IsAvailable => AppAvailability.IsEnabled(Id);
    ShareKindSet AcceptedShares => ShareKindSet.None;
    LocString? ShareLabel(ShareKind kind) => null;
    void OnShare(in ShareItem item)
    {
    }

    void OnOpened();
    void OnClosed();
    void Draw(in PhoneContext context);
}
```

Members with a `=>` body are C# default interface implementations: you only override them when you need non-default behavior.

| Member | What it means |
| --- | --- |
| `Id` | Stable lowercase identifier (`"clock"`, `"chirper"`). Keys everything: accents, layout persistence, availability flags, deep links. Never rename it once shipped. |
| `DisplayName` | Label under the home tile and in the app header. Real apps return `Loc.T(...)` so it follows the phone language (see [Localization](localization.md)). |
| `Glyph` | One- or two-character fallback text drawn on the tile when no icon texture exists for `Id`. `HomeTileView.DrawApp` (src/Aetherphone/Windows/Components/HomeTileView.cs) tries `AppIconArt.TryDraw` first and falls back to the glyph. |
| `Accent` | Tile and highlight color. The default delegates to `AppAccents.For(Id)`; keep it that way and add your color to the `AppAccents` table instead of hardcoding one. Unknown ids get a gray fallback. |
| `BadgeCount` | Unread count shown on the home tile. Read every frame; return a cached field, never compute or allocate here. `0` means no badge. |
| `BadgeAsDot` | When true, a badge is drawn as a small dot instead of a number. `SettingsApp` uses it for the unseen-changelog marker. |
| `WantsTransparentScreen` | Skips the opaque screen fill so the app paints (or deliberately does not paint) its own background. `CameraApp` uses it to show the game world. |
| `WantsSystemTheme` | Opt into the user's Light/Dark theme. Default is false: apps get the dark theme regardless, because most apps paint their own gradient backdrop. See the theming section. |
| `TransparentViewport` | Returns a screen-space rectangle the chassis leaves unpainted, punching a hole to the game world. `PhoneShell.TransparentBand` feeds it to `DeviceChrome.DrawBody`. `CameraApp` returns its viewfinder rect. |
| `IsAvailable` | Whether the app exists right now. Default consults the server kill switch via `AppAvailability.IsEnabled(Id)`. Overriding this to `true` opts out of the kill switch; almost never what you want. |
| `AcceptedShares` | Bitmask of `ShareKind` values this app can receive. Gate it on state: `MessageApp` returns `ShareKindSet.Photo` only when signed in. |
| `ShareLabel` | Optional per-kind label on the share sheet tile. `SettingsApp` returns `L.Share.SetAsWallpaper` so the tile reads as an action, not an app name. |
| `OnShare` | Called when the user picks your app on the share sheet. Fires before `OnOpened`; stash the item and consume it later (see sharing section). |
| `OnOpened` | Called when the app comes to the front. Also re-fires in cases listed below. Reset transient state and consume pending launcher intents here. |
| `OnClosed` | Called when the leave transition finishes, not the instant navigation changes. Flush drafts, clear selections, `router.Reset()`. |
| `Draw` | Your whole UI, every frame, inside `context.Content`. |
| `Dispose` | Called once at plugin unload by `PhoneShell.Dispose`. Free textures, timers, subscriptions. |

### Lifecycle details worth knowing

All of this is in `NavigationStack` (src/Aetherphone/Core/Apps/NavigationStack.cs):

- `OnOpened` fires when your app is presented, and again if `OpenApp` is called while your app is already the current app. Notification deep links depend on this re-fire, so `OnOpened` must be idempotent. Full story: [Notifications](notifications.md).
- Pressing back into a previous app calls `OnOpened` on the app being returned to.
- `OnClosed` fires from `FinalizeMotion` when the present/dismiss animation settles. During a present, the app that just went underneath gets `OnClosed`; during a dismiss, the leaving app does.
- `SuspensionGate` can block `OpenApp` entirely for suspended accounts; in that case your app never opens.

## Registration: AppRegistry and AppBundle

`AppRegistry.BuildDefault(PhoneServices services)` constructs every app with its dependencies and returns an `AppBundle`:

```csharp
internal sealed class AppBundle
{
    public required IReadOnlyList<IPhoneApp> Apps { get; init; }
    public required WidgetRegistry Widgets { get; init; }
    public required PhotoLibrary Photos { get; init; }
}
```

To add an app, construct it in `BuildDefault` and add it to the `apps` list. Order in the list does not drive home placement, which is `HomeLayoutService`'s job; it only shows through in surfaces that iterate the registry in order, such as the share sheet's row of target tiles. `PhoneServices` (src/Aetherphone/Core/PhoneServices.cs) is the service container; take only what you need through your constructor. `Plugin.cs` wires the bundle into `PhoneShell` at boot.

## PhoneContext: what Draw receives

```csharp
internal readonly struct PhoneContext
{
    public readonly Rect Content;
    public readonly PhoneTheme Theme;
    public readonly INavigator Navigation;
}
```

- `Content` is the rectangle you may draw in, already inset from the physical screen by the theme's side padding and top/bottom zones (`ShellScreenPainter.ContentRect`). `Rect` is the project's own rectangle type (src/Aetherphone/Core/Rect.cs).
- `Theme` is the `PhoneTheme` chosen for your app (dark, or the user's choice if `WantsSystemTheme` is true).
- `Navigation` is the shell navigator, described next.

The struct is rebuilt every frame; do not cache it across frames. Caching `context.Theme` and `context.Navigation` into fields at the top of `Draw` for use by helper methods within the same frame is the established pattern (`ClockApp.Draw` does exactly this).

## Navigation

There are two layers, and they never mix:

1. **Between apps and home**: `NavigationStack`, seen by apps as `INavigator`.
2. **Between screens inside one app**: a private `ViewRouter<TView>` the app owns.

### INavigator: the shell layer

```csharp
internal interface INavigator
{
    bool AtHome { get; }
    bool IsAvailable(string appId);
    void OpenApp(IPhoneApp app);
    void OpenAppFrom(IPhoneApp app, Rect origin);
    void Open(string appId);
    void Back();
    void GoHome();
}
```

`Open(appId)` is the safe way to jump to another app: it refuses if the target is not installed (`AppInstaller.IsInstalled`) or not available. `Back()` returns to the previous app in the history stack, or home. Apps rarely call these directly; the back chevron does it for them.

### ViewRouter: the in-app layer

`ViewRouter<TView>` (src/Aetherphone/Core/Apps/ViewRouter.cs) is a stack of views of any type you choose: an enum for simple apps (`ClockApp`), a route struct or interface for bigger ones (`SettingsApp` stacks `ISettingsPage`).

- `Push(view)` slides the new screen in from the right; `Push(view, false)` skips the animation.
- `Pop()` slides back; returns false at the root.
- `Pop(false)` pops instantly with no animation. Use it for **reactive pops**: when the pop happens because the data behind the current screen changed or vanished (a deleted photo, a submitted form), not because the user pressed back. An animated pop keeps drawing the outgoing view during the slide, and a view whose backing data is gone must not be drawn again. `PhotosApp`, `MusterApp`, and `YellowPagesApp` are full of this pattern.
- `Reset()` drops everything above the root. Call it in both `OnOpened` and `OnClosed` so the app always reopens at its root screen.
- `Draw(area, background, deltaSeconds, drawView)` renders the current view, compositing both views with a parallax slide during transitions. `drawView` is a `RouterDraw<TView>` delegate; store it in a field once in the constructor so you do not allocate a delegate every frame.

### The back button

`AppHeader.Draw(in PhoneContext context, string title, Action? onBack = null)` (src/Aetherphone/Windows/Components/AppHeader.cs) draws the title and the back chevron. When clicked it invokes `onBack` if you passed one, otherwise `context.Navigation.Back()`, which leaves the app. The standard wiring is a cached delegate that pops the router while it has depth, so the chevron walks your screen stack first and exits the app only from the root:

```csharp
internal sealed class RecipeApp : IPhoneApp
{
    private enum RecipeScreen : byte
    {
        List,
        Detail,
    }

    public string Id => "recipes";
    public string DisplayName => Loc.T(L.Apps.Recipes);
    public string Glyph => "R";
    public int BadgeCount => 0;

    private readonly AppSkin ui = new(AppPalettes.Market);
    private readonly ViewRouter<RecipeScreen> router = new(RecipeScreen.List);
    private readonly RouterDraw<RecipeScreen> drawView;

    public RecipeApp()
    {
        drawView = DrawView;
    }

    public void OnOpened() => router.Reset();

    public void OnClosed() => router.Reset();

    public void Draw(in PhoneContext context)
    {
        ui.Theme = context.Theme;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, UiScale.Current);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(RecipeScreen screen, Rect area, int depth)
    {
        ui.Body(area);
    }

    public void Dispose()
    {
    }
}
```

(`L.Apps.Recipes` stands in for a real localization key; see [Localization](localization.md).)

## Theming: AppSkin, AppPalette, AppAccents

Most apps have a bespoke dark gradient look composed from three pieces:

- **`AppPalette`** (src/Aetherphone/Windows/Components/AppPalette.cs): a readonly struct of colors, from `Accent` and ink tiers (`TitleInk`, `BodyInk`, `MutedInk`, `HeaderInk`, `HeadingInk`) to backdrop gradient stops (`BackdropTop`/`BackdropBottom`, `BloomTop`/`BloomBottom`) and surfaces (`CardFill`, `CardStroke`, `FieldSurface`, `HoverTint`).
- **`AppPalettes`** (src/Aetherphone/Windows/Components/AppPalettes.cs): the preset catalog, one palette per themed app (`AppPalettes.Chirper`, `AppPalettes.Velvet`, ...). Some are functions: `AppPalettes.JobsFor(accent)` derives a palette from a job color, `AppPalettes.Notes(theme)` and `AppPalettes.Calendar(theme)` derive from the system theme.
- **`AppSkin`** (src/Aetherphone/Windows/Components/AppSkin.cs): the painter. Construct it once with your palette (`private readonly AppSkin ui = new(AppPalettes.Fishing);`), assign `ui.Theme = context.Theme` each frame, then use `ui.Backdrop`, `ui.Body`, `ui.Card`, `ui.PillButton`, `ui.Field`, `ui.SectionHeading`, and friends. The wider widget library is covered in [UI toolkit](ui-toolkit.md).

Accent colors have exactly one source: `AppAccents.For(id)` (src/Aetherphone/Core/Apps/AppAccents.cs), a frozen dictionary from app id to `Vector4`. Home tiles, the share sheet, and palettes all resolve through it. Add your app's entry there.

### Light and dark scope

`ThemeProvider.ForApp(bool wantsSystemTheme)` (src/Aetherphone/Core/Theme/ThemeProvider.cs) returns the user-selected Light/Dark/Auto theme only when `wantsSystemTheme` is true; every other app receives the dark theme unconditionally, because custom gradient backdrops are designed against dark chrome. Only apps that draw on plain system surfaces opt in: `SettingsApp`, `NotesApp`, `CalendarApp`, `NotificationsApp`, `MapsApp`, and `LinkpearlApp` set `WantsSystemTheme => true`. If your app uses an `AppPalettes` gradient, leave the default.

## Badges

`HomeTileView.DrawApp` reads `BadgeCount` (and `BadgeAsDot`) every frame the home screen is visible and draws `AppBadge` in the tile corner when the count is positive. Folder tiles sum the numeric counts of the apps inside; a dot is shown only if no numeric badge exists but some member wants a dot.

Real examples: `MessageApp` returns `store.UnreadTotal + calls.UnseenMissed`, `AnnouncementsApp` returns `store.UnreadCount`, `DailiesApp` gates its count behind a configuration toggle. All of them are cheap reads of already-maintained counters; none of them query anything inside the getter.

## Availability and the server kill switch

`AppAvailability` (src/Aetherphone/Core/Aethernet/AppAvailability.cs) lets the Aethernet backend remotely disable apps (used to hide the server-backed social apps during incidents). Client behavior:

- The default `IsAvailable` calls `AppAvailability.IsEnabled(Id)`, which lazily refreshes a flag dictionary from the backend's `/flags` endpoint every 5 minutes (retrying after 60 seconds on failure) and persists the last answer in `Configuration.AppFlags` so it survives restarts and offline sessions.
- `"appstore"`, `"settings"`, and `"announcements"` are `AlwaysAvailable` and cannot be switched off.
- `"muster"` is `HiddenUntilLaunched`: absent until the server explicitly flags it on. Unknown ids default to enabled.
- An unavailable app disappears from the home screen: `HomeLayoutService` skips tiles for unavailable apps at load, `HomeScreen.Draw` calls `EnsureCurrent` every frame to react to flag flips, and `NavigationStack.Open` refuses to open one. When a hidden app becomes available and was never seen before, `EnsureCurrent` auto-installs it onto the home screen; an app the user uninstalled stays uninstalled.

## Receiving shares

The share flow (all client-side, src/Aetherphone/Core/Sharing/ShareService.cs and src/Aetherphone/Windows/Components/ShareSheet.cs):

1. A source app offers an item: `PhotosApp` calls `share.Offer(new ShareItem(ShareKind.Photo, path, Id))` from its viewer.
2. `ShareService.Offer` rebuilds the target list: every app that is not the source, is installed and available, and whose `AcceptedShares` contains the kind.
3. `ShareSheet` slides up over the screen and draws one tile per target, labeled with `ShareLabel(kind)` when provided, else `DisplayName`.
4. When the user picks a target, `ShareService.Pick` calls `target.OnShare(item)` and then `navigator.Open(target.Id)`.

Two consequences for the receiving app:

- **`OnShare` runs before `OnOpened`.** `Pick` invokes `OnShare` first, and opening the app is what triggers `OnOpened`. So `OnShare` must only stash the item (`AethergramApp` stores `item.LocalPath` in a `pendingSharedPhoto` field) and the pending value must survive whatever resets `OnOpened` performs. Consume it after open, when your stores are ready.
- **`ShareItem` carries a local file path**, not bytes: `ShareKind Kind`, `string LocalPath`, `string SourceAppId`. `ShareKind` currently has one value, `Photo`.

Dismissing the sheet (`ShareService.Dismiss`) clears `Pending` but intentionally leaves `Targets` populated so the closing animation can keep drawing the tiles.

## Home screen placement

`HomeLayoutService` (src/Aetherphone/Core/Home/HomeLayoutService.cs) owns what appears on the home screen:

- The grid is 4 columns (`Columns`) by 5 to 8 rows (`MinRows`/`MaxRows`, default 6), plus a dock of up to 4 apps (`DockCapacity`).
- Pages hold `HomeTile` items: an app, a shortcut, a folder of apps and/or shortcuts, or a widget. Layout is persisted as `HomeLayout` (src/Aetherphone/Core/Home/HomeLayout.cs) with per-item `Column`/`Row`, the `Installed` app list, the `Known` list (apps the user has ever seen), and the `Dock`.
- Placement is free-form and sticky: each tile keeps its saved `GridCell`, and the solver (`HomeGridSolver`) only assigns cells to tiles that have none or that conflict. Removing a tile leaves a hole; the grid never auto-compacts.
- `Installed` decides which apps exist on the phone. First run seeds it with every available app; installing via the App Store app (`AppStoreApp`, id `"appstore"`) appends a tile to the last page. `MandatoryApps` (`"appstore"`, `"settings"`, `"announcements"`) cannot be uninstalled.
- `AppInstaller` (src/Aetherphone/Core/Home/AppInstaller.cs) is the facade other systems use: `IsInstalled` (which also folds in availability), `Install`, `Uninstall`, and `Gate(appId)` returning an `AppGate` that background services (alarm timers, reminders) check before emitting notifications for an app that may be uninstalled.

State persistence details live in [State and persistence](state-and-persistence.md).

## Home widgets

Widgets are the large live tiles on the home screen: the clock faces, the calendar, the photos rotation, the Skywatcher forecast, daily resets, and the activity rings. A widget is a class implementing `IHomeWidget` (src/Aetherphone/Core/Home/IHomeWidget.cs):

```csharp
internal interface IHomeWidget : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string AppId { get; }
    WidgetSizeSet Sizes { get; }
    void Draw(in WidgetContext context);
}
```

- `Id` names the widget itself (`"clock.faces"`); one app can ship several. `DisplayName` titles the gallery entry.
- `AppId` ties the widget to its owning app, and the link does real work: `WidgetRegistry.IsAvailable` answers by asking the app's `IsAvailable`, tapping the placed widget opens the app (`WidgetRegistry.AppFor` feeds `OpenAppFrom`), and `HomeLayoutService` drops saved widget tiles whose app is uninstalled or unavailable.
- `Sizes` is a flag set of the sizes you support. On the 4-column home grid, `Small` occupies 2x2 cells, `Medium` 4x2, and `Large` 4x4 (`WidgetSizes` in src/Aetherphone/Core/Home/WidgetSize.cs). Branch on `context.Size` inside `Draw`.

`Draw` receives a `WidgetContext`, a readonly struct in the same file carrying `DrawList`, `Bounds`, `Theme`, `Size`, `Scale`, `Delta`, and `Opacity`. Unlike an app's `Draw`, a widget paints directly onto the given draw list inside `Bounds`; there is no router and no `PhoneContext`. Two rules follow from how it is called:

- The same `Draw` renders both the placed tile and the gallery preview, at whatever rectangle the caller picked, so lay out relative to `Bounds` and never assume grid pixel sizes.
- `Opacity` carries the home screen's fades (page motion, edit transitions). Multiply it into every color you draw or your widget will pop while everything around it fades.

Backgrounds are the widget's job; `HomeGridRenderer` paints nothing behind a resting widget tile (the one exception is a drag, where it drops an `Elevation.Floating` shadow under the tile it is carrying). Start `Draw` with `WidgetChrome.Card` (src/Aetherphone/Windows/Widgets/WidgetChrome.cs) for the standard frosted squircle, or `WidgetChrome.Tinted` for a gradient fill; both use the shared `WidgetChrome.Radius`, which keeps every widget's corners in one family. `WidgetChrome.Eyebrow` (and `EyebrowMarquee` for text that may not fit) draws the small tracked uppercase caption the shipped widgets use.

Registration mirrors apps: `WidgetCatalog.Build` (src/Aetherphone/Windows/Widgets/WidgetCatalog.cs) constructs every widget once at boot and returns the `WidgetRegistry` that travels in `AppBundle.Widgets`. To ship a widget, construct it there; there is no per-app hook. `PhoneShell.Dispose` disposes every widget alongside the apps at unload.

How one lands on the home screen: long-press the home screen to enter edit mode, then press the add button, which opens `WidgetGallery` (src/Aetherphone/Core/Shell/Home/WidgetGallery.cs). The gallery lists every widget whose app is available, calls your `Draw` as a live preview, and shows size chips when you support more than one size; its add button calls `HomeLayoutService.AddWidget(widget, size, pageIndex)`, which appends a `HomeTile.ForWidget` tile to the current page. The same widget can be placed more than once; each tile gets its own key. In edit mode, tapping a placed widget opens `WidgetSizeMenu` to resize or remove it. The saved layout stores the widget id and its serialized size per tile, and the first-run layout seeds one widget, the medium Skywatcher forecast.

A minimal widget:

```csharp
internal sealed class RecipeWidget : IHomeWidget
{
    public string Id => "recipes.today";
    public string DisplayName => Loc.T(L.Apps.Recipes);
    public string AppId => "recipes";
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Card(context.DrawList, context.Bounds, context.Scale, context.Opacity);
        var origin = context.Bounds.Min + new Vector2(16f, 14f) * context.Scale;
        WidgetChrome.Eyebrow(context.DrawList, origin, Loc.T(L.Apps.Recipes), context.Theme.TextMuted,
            context.Scale, context.Opacity);
    }

    public void Dispose()
    {
    }
}
```

## Control Center tiles

The Control Center (opened with a tap on the band at the top of the screen) is a sibling system with its own contract: each tile is an `IControlModule` (src/Aetherphone/Core/ControlCenter/IControlModule.cs) with `Id`, `GalleryLabel`, `GalleryIcon`, a `Sizes` list of supported `ControlSpan` values, a `DefaultSpan`, and `Draw(in ControlModuleContext context)`. `ControlModuleContext` looks like `WidgetContext` with a few changes: the rectangle is named `Rect` instead of `Bounds`, `Span` replaces `Size`, there is no `Delta`, and it adds an `Interactive` flag you must check before reacting to input. Spans on the 4-column grid (`ControlSpans` in src/Aetherphone/Core/ControlCenter/ControlSpan.cs): `Small` 1x1, `Wide` 2x1, `Tall` 1x2, `Large` 2x2, `Bar` 4x1.

You rarely implement the interface directly. src/Aetherphone/Core/ControlCenter/Modules/ has the reusable shapes: `ToggleModule` (id, icon, label, a `Func<bool>` for state and an `Action` on press; also used for one-shot launchers like the camera and settings tiles, whose state always reads false), `SliderModule` (brightness, volume), `MediaModule`, and `AccentModule`. Every module is constructed in the `ControlRegistry` constructor (src/Aetherphone/Core/ControlCenter/ControlRegistry.cs); adding a tile is usually one more `Add(new ToggleModule(...))` line there.

`ControlLayoutService` (src/Aetherphone/Core/ControlCenter/ControlLayoutService.cs) owns which modules are on the grid, in what order and at what span, and solves placement with the same `HomeGridSolver` the home screen uses. Long-press a tile or press the customize button to edit: dragging reorders, the resize handle cycles through the module's `Sizes`, the remove badge takes a tile off, and the add button opens `ControlGallery`, which lists the modules currently off the grid and places an added one at its `DefaultSpan`. The layout persists in `Configuration.ControlPanel` as module ids, spans, and an `Enabled` list. Install semantics are pinned by tests (src/Aetherphone.Tests/ControlLayoutServiceInstallTests.cs): a fresh install puts every module on the grid, a module shipped in an update after the user's layout was saved stays off the grid until the user adds it, and adds and removes survive restarts.

## Cross-app launchers

Apps never call into each other directly. To deep-link, one app writes an intent into a small launcher service, opens the target by id, and the target consumes the intent in `OnOpened`. The launchers live in src/Aetherphone/Core/Apps/ and are created in `PhoneServices`:

| Launcher | Intent |
| --- | --- |
| `DmLauncher` | Open `MessageApp` (id `"message"`) to a user, a conversation, or the Calls tab (`RequestUser`, `RequestConversation`, `RequestCalls`) |
| `GramDmLauncher` | Open an Aethergram DM to a user, optionally with a prefilled draft |
| `SocialLauncher` | Per-app `SocialDeepLink`: a `SocialLinkKind` (`Profile`, `Post`, or `Requests`) plus an id, keyed by target app id |
| `VelvetLauncher` | Open Velvet to a user |

Each follows the same request/consume shape: `Request...` stores the pending value, `TryConsume...` returns it exactly once and clears it. `MessageApp.OnOpened` is the canonical consumer: it checks `TryConsumeCalls`, then `TryConsumeConversation`, then `TryConsumeUser`, and routes accordingly. If your new app needs to be a deep-link target, add a launcher in this style rather than exposing methods on the app class.

## RefreshCadence: polling from Draw

Immediate mode has no timers of its own, so periodic work is driven from `Draw` with `RefreshCadence` (src/Aetherphone/Core/Apps/RefreshCadence.cs):

```csharp
internal struct RefreshCadence
{
    public bool Advance(float deltaSeconds, float intervalSeconds);
    public void Reset();
}
```

`Advance` accumulates frame time and returns true once the interval has elapsed; you then do the work and call `Reset`. `FishingApp` refreshes its voyage table every 5 seconds this way, and `TimersApp` and `ActivityApp` use the same pattern. Keep it for cheap local recomputation; network polling belongs in stores with their own cadence (see [Networking](networking.md)).

## Gotchas

- **`OnOpened` re-fires on an already-open app.** `NavigationStack.OpenApp` calls `NotifyOpened` even when the app is already current, and `Back()` calls `OnOpened` on the app you return to. Make `OnOpened` idempotent and cheap.
- **`OnShare` arrives before `OnOpened`.** Stash the shared item in a field that survives your `OnOpened` reset, and consume it afterwards. Clearing all pending state at the top of `OnOpened` will eat the share.
- **`Pop(true)` redraws the view you are leaving.** During the slide, `ViewRouter.Draw` keeps calling your draw delegate for the outgoing view. If the pop was caused by the underlying data disappearing, use `Pop(false)` or the outgoing screen will draw against data that no longer exists.
- **`BadgeCount` and `IsAvailable` are read every frame on the home screen.** Return cached values. `IsAvailable`'s default is already cheap (a dictionary lookup that occasionally schedules a background fetch); do not replace it with anything that does IO.
- **`OnClosed` is late.** It fires when the close animation settles (`NavigationStack.FinalizeMotion`), so one more `Draw` or several can happen after the user initiated the close. Do not treat the close gesture itself as your last frame.
- **Exceptions in `Draw` do not close the app.** `ShellScreenPainter.PaintApp` catches per frame, logs, and draws a failure message, then calls you again next frame. A throwing `Draw` becomes a log-spamming error screen, not a crash, so watch the log during development.
- **Missing accent means a gray tile.** `AppAccents.For` returns its fallback for unknown ids. Registering the app without adding an `AppAccents` entry is the usual cause of a colorless icon.
- **`RouterDraw` delegates allocate if created inline.** Passing a method group or lambda directly to `router.Draw` every frame allocates a delegate per frame. Cache it in a field in the constructor, as every shipped app does.

## Related docs

- [Architecture](architecture.md): plugin boot, services, the frame loop, and how the shell hosts everything described here
- [Creating an app](creating-an-app.md): step-by-step tutorial that applies this contract
- [UI toolkit](ui-toolkit.md): the Windows/Components widget library, typography, metrics, and input handling
- [State and persistence](state-and-persistence.md): Configuration, migrations, and where the home layout, the Control Center layout, and app flags are stored
- [Notifications](notifications.md): notification channels, deep links, and how they interact with badges and launchers
- [Localization](localization.md): `L` keys and `Loc.T`, required for `DisplayName` and all user-facing copy
- [Networking](networking.md): the Aethernet client behind availability flags and the social apps
- [Games framework](games-framework.md): the mini-game layer hosted inside the Games app
