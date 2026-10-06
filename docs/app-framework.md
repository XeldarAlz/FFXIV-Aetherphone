# App framework

This is the reference for the contract every phone app lives by: the `IPhoneApp` interface, resuming and the other optional interfaces, how apps are registered and drawn, in-app navigation, theming, badges, availability, sharing, home screen placement, home widgets, Control Center tiles, cross-app launchers, polling, landscape, and first-open tours. Read it alongside [Creating an app](creating-an-app.md), which walks through building an app step by step; come back here whenever you need the exact semantics of a member or service.

Aetherphone renders with Dear ImGui, an immediate mode UI library: nothing is retained between frames, so every visible screen is redrawn from scratch every frame. An app is a plain C# class that the shell asks to draw itself while it is open. There is no client-server split inside the plugin; the separate Aethernet backend (a different repository) only enters this page through the feature-flag kill switch, and only its client side is documented here.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Apps/IPhoneApp.cs | The interface every app implements |
| src/Aetherphone/Core/Apps/IResumableApp.cs | Optional `OnResumed` hook for apps that keep their place across a close |
| src/Aetherphone/Core/Apps/AppRegistry.cs | Builds the one instance of every app |
| src/Aetherphone/Core/Apps/AppBundle.cs | Apps, widgets, widget actions, photo library, contacts, DM pop-outs and the video suite handed to the shell |
| src/Aetherphone/Core/Apps/AppStoreCatalog.cs | The App Store entry every registered app needs |
| src/Aetherphone/Core/Apps/PhoneContext.cs | Per-frame struct handed to `Draw` |
| src/Aetherphone/Core/Apps/ViewRouter.cs | In-app screen stack with slide transitions |
| src/Aetherphone/Core/Apps/NavigationStack.cs | Shell-level navigator between home and apps, and the resume window |
| src/Aetherphone/Core/Apps/INavigator.cs | The navigator surface apps see |
| src/Aetherphone/Core/Apps/AppAccents.cs | Single source of every app's accent color |
| src/Aetherphone/Core/Apps/RefreshCadence.cs | Tiny timer struct for periodic refresh in `Draw` |
| src/Aetherphone/Core/Apps/AppLandscape.cs | Lets the current app hold the phone in landscape |
| src/Aetherphone/Core/Aethernet/AppAvailability.cs | Server feature flags (the kill switch) |
| src/Aetherphone/Core/Sharing/ShareService.cs | Share sheet state and target matching |
| src/Aetherphone/Core/Sharing/ShareItem.cs | `ShareKind`, `ShareKindSet`, `ShareItem` |
| src/Aetherphone/Core/Home/HomeLayoutService.cs | Home pages, dock, install state, tile placement |
| src/Aetherphone/Core/Home/AppInstaller.cs | Install/uninstall facade over the layout service |
| src/Aetherphone/Core/Home/IHomeWidget.cs | The widget contract and `WidgetContext` |
| src/Aetherphone/Core/Home/WidgetRegistry.cs | Widget lookup and per-app availability |
| src/Aetherphone/Core/Home/WidgetActions.cs | Routes a widget tap or link into the target app |
| src/Aetherphone/Windows/Widgets/WidgetCatalog.cs | Builds the one instance of every widget |
| src/Aetherphone/Core/ControlCenter/IControlModule.cs | The Control Center tile contract |
| src/Aetherphone/Core/ControlCenter/ControlRegistry.cs | Builds every Control Center module |
| src/Aetherphone/Windows/Components/Skin/AppSkin.cs | Per-app widget painter (backdrop, cards, buttons, fields) |
| src/Aetherphone/Windows/Components/Skin/AppPalette.cs | Color set an `AppSkin` paints with |
| src/Aetherphone/Windows/Components/Skin/AppPalettes.cs | Preset palettes, one per themed app |
| src/Aetherphone/Core/Onboarding/TourRegistry.cs | First-open coachmark tours, one per app |
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
    bool HasBadge => false;
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
| `Glyph` | One- or two-character fallback text for the tile. `HomeTileView.DrawApp` (src/Aetherphone/Windows/Components/Chrome/HomeTileView.cs) tries the painted icon pair (`AppIconTile.TryDraw`) first, then `AppIconArt.TryDraw` on the accent tile, and only then draws the glyph. |
| `Accent` | Tile and highlight color. The default delegates to `AppAccents.For(Id)`; keep it that way and add your color to the `AppAccents` table instead of hardcoding one. Unknown ids get a gray fallback. |
| `BadgeCount` | Unread count shown on the home tile. Read every frame; return a cached field, never compute or allocate here. `0` means no badge. Always return the raw count here; the user's on/off preference is applied centrally, not by this getter. |
| `HasBadge` | Opts the app into the shared, user-toggleable badge switch (`Configuration.BadgeSettings`, default on), shown as a Badges switch on that app's own page in Settings (reached from Settings > Notifications and Badges, or Settings > Apps). Default `false`, so `BadgeCount => 0` apps need not override it. See [Notifications](notifications.md#hiding-a-badge). |
| `BadgeAsDot` | When true, a badge is drawn as a small dot instead of a number. `SettingsApp` uses it for the unseen-changelog marker. |
| `WantsTransparentScreen` | Skips the opaque screen fill so the app paints (or deliberately does not paint) its own background. `CameraApp` uses it to show the game world. |
| `WantsSystemTheme` | Opt into the user's Light/Dark theme. Default is false: apps get the dark theme regardless, because most apps paint their own gradient backdrop. See the theming section. |
| `TransparentViewport` | Returns a screen-space rectangle the chassis leaves unpainted, punching a hole to the game world. `PhoneShell.TransparentBand` feeds it to `DeviceChrome.DrawBody`. `CameraApp` returns its viewfinder rect. |
| `IsAvailable` | Whether the app exists right now. Default consults the server kill switch via `AppAvailability.IsEnabled(Id)`. Overriding this to `true` opts out of the kill switch; almost never what you want. |
| `AcceptedShares` | Bitmask of `ShareKind` values this app can receive. Gate it on state: `MessageApp` returns `ShareKindSet.Photo` only when signed in. |
| `ShareLabel` | Optional per-kind label on the share sheet tile. `SettingsApp` returns `L.Share.SetAsWallpaper` so the tile reads as an action, not an app name. |
| `OnShare` | Called when the user picks your app on the share sheet. Fires before the app opens; stash the item and consume it later (see sharing section). |
| `OnOpened` | Called when the app comes to the front. Also re-fires in cases listed below. A resumable app gets `OnResumed` instead when it resumes (see Resuming). Reset transient state and consume pending launcher intents here. |
| `OnClosed` | Called when the leave transition finishes, not the instant navigation changes. Flush drafts and clear selections; a non-resumable app also calls `router.Reset()` here. |
| `Draw` | Your whole UI, every frame, inside `context.Content`. |
| `Dispose` | Called once at plugin unload by `PhoneShell.Dispose`. Free textures, timers, subscriptions. |

### Lifecycle details worth knowing

All of this is in `NavigationStack` (src/Aetherphone/Core/Apps/NavigationStack.cs):

- `OnOpened` fires when your app is presented, and again if `OpenApp` is called while your app is already the current app. Notification deep links depend on this re-fire, so `OnOpened` must be idempotent. Full story: [Notifications](notifications.md). A resumable app can get `OnResumed` in either case instead (next section).
- Pressing back into a previous app calls `OnOpened` on the app being returned to, or `OnResumed` if that app is resumable.
- `OnClosed` fires from `FinalizeMotion` when the present/dismiss animation settles. During a present, the app that just went underneath gets `OnClosed`; during a dismiss, the leaving app does.
- `SuspensionGate` can block `OpenApp` entirely for suspended accounts; in that case your app never opens.

### Resuming: IResumableApp

An app that should come back where the user left it implements `IResumableApp` (src/Aetherphone/Core/Apps/IResumableApp.cs) instead of `IPhoneApp`. It adds one member:

```csharp
internal interface IResumableApp : IPhoneApp
{
    void OnResumed();
}
```

The social and messaging apps (Message, Linkpearl, Chirper, Aethergram, Velvet), Music, Settings, Notes, Market, Venues and Health implement it today; grep for `IResumableApp` for the current set. The rules, all in `NavigationStack`:

- Every close stamps the time. The resume window is `NavigationStack.ResumeWindowMilliseconds`, ten minutes from that close.
- Whenever the app is opened (home icon, `Open(appId)`, a deep link, or `OpenApp` on the app that is already current), the navigator calls `OnResumed` instead of `OnOpened` if the app closed within the window. Otherwise it calls `OnOpened` as usual.
- `Back()` into a resumable app always calls `OnResumed`, with no window check.
- A resumed visit keeps its scroll position: only a fresh open starts a new visit stamp (`AppVisits`), and `AppSurface.Begin` scrolls back to the top only on a new visit.
- The App Switcher (src/Aetherphone/Core/Shell/AppSwitcher.cs) lists the current app plus every resumable app that closed within the window and is still installed and available, most recent first (`NavigationStack.CollectOpen`). Swiping a card away calls `Forget`, which drops the close stamp, so the next open is a fresh `OnOpened`.

What this asks of the app:

- Reset in `OnOpened` only. `NotesApp` and `SettingsApp` call `router.Reset()` in `OnOpened` and leave the stack alone in `OnClosed`, so a resume lands on the same screen.
- Consume launcher intents in both `OnOpened` and `OnResumed` (or in `Draw`). `MessageApp` routes both through one `RefreshAndConsumeLaunch` method; a target that only consumes in `OnOpened` silently drops deep links that arrive during a resume.
- Keep `OnResumed` cheap: a refresh and the pending intents, nothing that rebuilds state the user expects to find.

### Optional interfaces

Beyond `IResumableApp`, a few small interfaces in src/Aetherphone/Core/Apps/ let shell surfaces reach into an app without a direct reference:

| Interface | What it gives the app |
| --- | --- |
| `ITabRouteTarget` (ITabRouteTarget.cs) | `OpenTab(string tab)`, called by a widget's `WidgetRoute.Tab(appId, intent)` before the app opens. Store the request in a `PendingTab` field (`pendingTab.Request(tab)`) and claim it in `Draw` with `pendingTab.Take(route)`, which returns true once. Clock, Dailies, Notes and several others implement it. |
| `ISpotlightPages`, `ISpotlightNotes`, `ISpotlightConversations`, `ISpotlightStoreApps`, `ISpotlightFights`, `ISpotlightVenues` (ISpotlightTarget.cs) | Spotlight search hooks. `SpotlightIndex` binds the first registered app that implements each one, so each has a single owner today (Settings, Notes, Message, App Store, Strats, Venues). `WidgetActions` reuses `ISpotlightNotes` and `ISpotlightVenues` for note and venue widget routes. |

## Registration: AppRegistry and AppBundle

`AppRegistry.BuildDefault(PhoneServices services, VideoSuite videoSuite, AetherStreamScreenWindow screenWindow, LinkpearlPopouts linkpearlPopouts)` constructs every app with its dependencies, builds the widget catalog, and returns an `AppBundle`:

```csharp
internal sealed class AppBundle
{
    public required IReadOnlyList<IPhoneApp> Apps { get; init; }
    public required WidgetRegistry Widgets { get; init; }
    public required WidgetActions WidgetActions { get; init; }
    public required PhotoLibrary Photos { get; init; }
    public required Telephony.ContactBook Contacts { get; init; }
    public required IMessagePopouts MessagePopouts { get; init; }
    public required VideoSuite Video { get; init; }
}
```

To add an app, construct it in `BuildDefault` and add it to the `apps` list, then give it a `StoreEntry` in `AppStoreCatalog` (src/Aetherphone/Core/Apps/AppStoreCatalog.cs). `AppStoreCatalogTests` fails when an app type has no catalog entry, and each store category must hold three to eight apps; [Creating an app](creating-an-app.md#step-4-register-it) walks through both steps. Order in the list does not drive home placement, which is `HomeLayoutService`'s job; it only shows through in surfaces that iterate the registry in order, such as the share sheet's row of target tiles. `PhoneServices` (src/Aetherphone/Core/PhoneServices.cs) is the service container; take only what you need through your constructor. `Plugin.cs` wires the bundle into `PhoneShell` at boot.

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
    void OpenAppFrom(IPhoneApp app, Rect origin, LaunchOrigin kind);
    void Open(string appId);
    void Back();
    void GoHome();
}
```

`Open(appId)` is the safe way to jump to another app: it refuses if the target is not installed (`AppInstaller.IsInstalled`) or not available. `Back()` returns to the previous app in the history stack, or home. Apps rarely call these directly; the back chevron does it for them.

`OpenAppFrom` zooms the app out of `origin`. `LaunchOrigin` (src/Aetherphone/Core/Apps/LaunchOrigin.cs) says what that rectangle is: `Icon` for a home icon, which the opening card grows out of, or `Surface` for anything else, such as a widget, where the card fades in instead. Home icons pass `Icon`; `WidgetActions` passes `Surface`.

### ViewRouter: the in-app layer

`ViewRouter<TView>` (src/Aetherphone/Core/Apps/ViewRouter.cs) is a stack of views of any type you choose: an enum for simple apps (`ClockApp`), a route struct or interface for bigger ones (`SettingsApp` stacks `ISettingsPage`).

- `Push(view)` slides the new screen in from the right; `Push(view, false)` skips the animation.
- `Pop()` slides back; returns false at the root.
- `Pop(false)` pops instantly with no animation. Use it for **reactive pops**: when the pop happens because the data behind the current screen changed or vanished (a deleted photo, a submitted form), not because the user pressed back. An animated pop keeps drawing the outgoing view during the slide, and a view whose backing data is gone must not be drawn again. `PhotosApp`, `MusterApp`, and `YellowPagesApp` are full of this pattern.
- `Replace(view)` swaps the top view in place with no animation, for a screen that turns into another (a sent feedback form becoming its confirmation, a new message becoming the thread).
- `Reset()` drops everything above the root. A non-resumable app calls it in both `OnOpened` and `OnClosed` so it always reopens at its root screen; an `IResumableApp` calls it in `OnOpened` only, so a resume lands where the user left.
- `Draw(area, background, deltaSeconds, drawView)` renders the current view, compositing both views with a parallax slide during transitions. `drawView` is a `RouterDraw<TView>` delegate; store it in a field once in the constructor. The router caches its own layer painters, so with a cached `drawView` a frame of `Draw` allocates nothing.
- `Current`, `Depth`, and `TryGetView(index, out view)` read the stack, for example to title the back control after the previous screen.

### The header and the back button

Most screens use the collapsing large title from `AppHeader` (src/Aetherphone/Windows/Components/Layout/AppHeader.cs):

1. Before the body, call `var navBar = AppHeader.BeginLargeTitle(context, depth > 1);`. The second argument reserves the inline row above the title that holds the back control, so pushed screens pass true (`SettingsApp` passes `depth > 1` exactly like this).
2. Draw the body inside `using (AppSurface.Begin(navBar.Body))`. The surface extends under the bar and reserves the title band, so content scrolls beneath it, and the collapse follows the surface's scroll offset directly.
3. After the body, call `AppHeader.EndLargeTitle(in navBar, context, id, title, NavBarStyle.From(ui), buttons, backTitle, onBack)`. `buttons` is a span of at most two `NavBarButton` glass circles (`ReadOnlySpan<NavBarButton>.Empty` for none, or a preallocated array); the return value is the pressed button index or -1.

The back control appears only when `onBack` is not null. Pass a cached delegate that pops your router on pushed screens, and `null` at the root, where the user leaves the app with the home gesture. `backTitle` labels the control with the previous screen's title; take it from the router with `router.TryGetView(depth - 2, out var previous)`. Settings, Notes and Clock are good references.

`AppHeader.Draw(in PhoneContext context, string title, Action? onBack = null)` is the older inline title bar, still used by a few screens (Casino, Games). Its chevron invokes `onBack` if you passed one, otherwise `context.Navigation.Back()`, which leaves the app.

Cache the back delegate once in the constructor (`back = () => router.Pop();`, as `ClockApp` does), the same way you cache the `RouterDraw` delegate, so the header adds no per-frame allocation. A two-screen app wired this way:

```csharp
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Recipes;

internal sealed class RecipeApp : IPhoneApp
{
    private enum RecipeScreen : byte
    {
        List,
        Detail,
    }

    private readonly AppSkin ui = new(AppPalettes.Market);
    private readonly ViewRouter<RecipeScreen> router = new(RecipeScreen.List);
    private readonly RouterDraw<RecipeScreen> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public RecipeApp()
    {
        drawView = DrawView;
        back = () => router.Pop();
    }

    public string Id => "recipes";
    public string DisplayName => Loc.T(L.Apps.Recipes);
    public string Glyph => "R";
    public int BadgeCount => 0;

    public void OnOpened() => router.Reset();

    public void OnClosed() => router.Reset();

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, context.Theme, UiScale.Current));
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(RecipeScreen screen, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, depth > 1);
        using (AppSurface.Begin(navBar.Body))
        {
            if (screen == RecipeScreen.List)
            {
                DrawList();
            }
            else
            {
                SettingsSection.Hint(Loc.T(L.Recipes.DetailHint), theme);
            }
        }

        var title = screen == RecipeScreen.List ? DisplayName : Loc.T(L.Recipes.Today);
        AppHeader.EndLargeTitle(in navBar, context, "recipes.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, depth > 1 ? DisplayName : string.Empty, depth > 1 ? back : null);
    }

    private void DrawList()
    {
        var card = GroupCard.Begin(ui, 1);
        if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Recipes.Today), string.Empty, theme))
        {
            router.Push(RecipeScreen.Detail);
        }

        card.End();
    }

    public void Dispose()
    {
    }
}
```

(`L.Apps.Recipes` and the `L.Recipes` entries stand in for real localization keys; see [Localization](localization.md).)

## Theming: AppSkin, AppPalette, AppAccents

Most apps have a bespoke dark gradient look composed from three pieces:

- **`AppPalette`** (src/Aetherphone/Windows/Components/Skin/AppPalette.cs): a readonly struct of colors, from `Accent` and ink tiers (`TitleInk`, `BodyInk`, `MutedInk`, `HeaderInk`, `HeadingInk`) to backdrop gradient stops (`BackdropTop`/`BackdropBottom`, `BloomTop`/`BloomBottom`) and surfaces (`CardFill`, `CardStroke`, `FieldSurface`, `HoverTint`, `Hairline`, `HoverWash`).
- **`AppPalettes`** (src/Aetherphone/Windows/Components/Skin/AppPalettes.cs): the preset catalog, one palette per themed app (`AppPalettes.Chirper`, `AppPalettes.Velvet`, ...). Most presets are one line over the app's accent: `For("id")` for an accent-tinted gradient, or `Neutral(AppAccents.For("id"))` for a graphite one. Some are functions: `AppPalettes.JobsFor(accent)` derives a palette from a job color, `AppPalettes.Notes(theme)` and `AppPalettes.Calendar(theme)` derive from the system theme.
- **`AppSkin`** (src/Aetherphone/Windows/Components/Skin/AppSkin.cs): the painter. Construct it once with your palette (`private readonly AppSkin ui = new(AppPalettes.Fishing);`), assign `ui.Theme = context.Theme` each frame, then use `ui.Backdrop`, `ui.Body`, `ui.Card`, `ui.PillButton`, `ui.Field`, `ui.SectionHeading`, and friends. The wider widget library is covered in [UI toolkit](ui-toolkit.md).

Accent colors have exactly one source: `AppAccents.For(id)` (src/Aetherphone/Core/Apps/AppAccents.cs), a frozen dictionary from app id to `Vector4`. Home tiles, the share sheet, and palettes all resolve through it. Add your app's entry there.

### Light and dark scope

`ThemeProvider.ForApp(bool wantsSystemTheme)` (src/Aetherphone/Core/Theme/ThemeProvider.cs) returns the user-selected Light/Dark/Auto theme only when `wantsSystemTheme` is true; every other app receives the dark theme unconditionally, because custom gradient backdrops are designed against dark chrome. Only apps that draw on plain system surfaces opt in: `SettingsApp`, `NotesApp`, `CalendarApp`, `MapsApp`, `PhotosApp`, and `AnnouncementsApp` set `WantsSystemTheme => true`. If your app uses an `AppPalettes` gradient, leave the default.

## Badges

`HomeTileView.DrawApp` (src/Aetherphone/Windows/Components/Chrome/HomeTileView.cs) reads `BadgeCount` (and `BadgeAsDot`) every frame the home screen is visible and draws `AppBadge` in the tile corner when the count is positive and the app's badge is not turned off (`HasBadge`/`Configuration.IsAppBadgeEnabled`, see [Notifications](notifications.md#hiding-a-badge)). Folder tiles sum the numeric counts of the apps inside; a dot is shown only if no numeric badge exists but some member wants a dot.

Real examples: `MessageApp` returns `store.UnreadTotal + calls.UnseenMissed`, `AnnouncementsApp` returns `store.UnreadCount`, `DailiesApp` returns `tracker.Outstanding`, a sum over counters the tracker already maintains. All of them are cheap reads of already-maintained counters; none of them query anything inside the getter, and none of them check the user's on/off preference themselves.

## Availability and the server kill switch

`AppAvailability` (src/Aetherphone/Core/Aethernet/AppAvailability.cs) lets the Aethernet backend remotely disable apps (used to hide the server-backed social apps during incidents). Client behavior:

- A region gate runs first: on a Chinese game client, `Enabled` returns false for any id in `UnavailableInChina` before any flag lookup, ahead of even the `AlwaysAvailable` list. The list is currently empty, so no app is region-gated.
- The default `IsAvailable` calls `AppAvailability.IsEnabled(Id)`, which lazily refreshes a flag dictionary from the backend's `/flags` endpoint every 5 minutes (retrying after 60 seconds on failure) and persists the last answer in `Configuration.AppFlags` so it survives restarts and offline sessions.
- `"appstore"`, `"settings"`, and `"announcements"` are `AlwaysAvailable` and cannot be switched off by the server.
- `"muster"`, `"coin"`, and `"casino"` are `HiddenUntilLaunched`: absent until the server explicitly flags them on. Other unknown ids default to enabled.
- An unavailable app disappears from the home screen: `HomeLayoutService` skips tiles for unavailable apps at load, `HomeScreen.Draw` calls `EnsureCurrent` every frame to react to flag flips, and `NavigationStack.Open` refuses to open one. When a hidden app becomes available and was never seen before, `EnsureCurrent` auto-installs it onto the home screen; an app the user uninstalled stays uninstalled.

## Receiving shares

The share flow (all client-side, src/Aetherphone/Core/Sharing/ShareService.cs and src/Aetherphone/Windows/Components/Sheets/ShareSheet.cs):

1. A source app offers an item: `PhotosApp` calls `share.Offer(new ShareItem(ShareKind.Photo, path, Id))` from its viewer.
2. `ShareService.Offer` rebuilds the target list: every app that is not the source, is installed and available, and whose `AcceptedShares` contains the kind.
3. `ShareSheet` slides up over the screen and draws one tile per target, labeled with `ShareLabel(kind)` when provided, else `DisplayName`.
4. When the user picks a target, `ShareService.Pick` calls `target.OnShare(item)` and then `navigator.Open(target.Id)`.

Two consequences for the receiving app:

- **`OnShare` runs before the app opens.** `Pick` invokes `OnShare` first, and opening the app is what triggers `OnOpened` (or `OnResumed` for a resumable app). So `OnShare` must only stash the item (`AethergramApp` stores `item.LocalPath` in a `pendingSharedPhoto` field) and the pending value must survive whatever resets `OnOpened` performs. Consume it after open, when your stores are ready; consuming from `Draw`, as `AethergramApp` and `SettingsApp` do, works for both kinds of open.
- **`ShareItem` carries a local file path**, not bytes: `ShareKind Kind`, `string LocalPath`, `string SourceAppId`. `ShareKind` currently has one value, `Photo`.

Dismissing the sheet (`ShareService.Dismiss`) clears `Pending` but intentionally leaves `Targets` populated so the closing animation can keep drawing the tiles.

## Home screen placement

`HomeLayoutService` (src/Aetherphone/Core/Home/HomeLayoutService.cs) owns what appears on the home screen:

- The grid is 4 columns (`Columns`) by 5 to 8 rows (`MinRows`/`MaxRows`, default 6), plus a dock of up to 4 apps (`DockCapacity`).
- Pages hold `HomeTile` items: an app, a shortcut, a folder of apps and/or shortcuts, a widget, or a Smart Stack of up to ten same-size widgets (`HomeTile.StackCapacity`). Layout is persisted as `HomeLayout` (src/Aetherphone/Core/Home/HomeLayout.cs) with per-item `Column`/`Row`, the `Installed` app list, the `Known` list (apps the user has ever seen), and the `Dock`.
- Placement is free-form and sticky: each tile keeps its saved `GridCell`, and the solver (`HomeGridSolver`) only assigns cells to tiles that have none or that conflict. Removing a tile leaves a hole; the grid never auto-compacts.
- `Installed` decides which apps exist on the phone. First run installs every available app and lays them out from three curated lists: `DefaultDockApps` for the dock, `DefaultFirstPageApps` for the first page (after the seeded widget), and `DefaultSecondPageApps` for the second. Any installed app missing from those lists is appended to the last page in `DisplayName` order. Installing via the App Store app (`AppStoreApp`, id `"appstore"`) appends a tile to the last page. `MandatoryApps` (`"appstore"`, `"settings"`, `"announcements"`, `"messages"` for Linkpearl, `"camera"`, `"photos"`, `"notifications"`) cannot be uninstalled, show no remove badge in edit mode, come back on the next load if a saved layout lacks them, and appear in the App Store as Built in (Open, no Remove).
- `AppInstaller` (src/Aetherphone/Core/Home/AppInstaller.cs) is the facade other systems use: `IsInstalled` (which also folds in availability), `Install`, `Uninstall`, and `Gate(appId)` returning an `AppGate` that background services (alarm timers, reminders) check before emitting notifications for an app that may be uninstalled.

State persistence details live in [State and persistence](state-and-persistence.md).

## Home widgets

Widgets are the live tiles on the home screen. Most apps ship at least one (clocks, alarms and timers, the calendar, weather, dailies and resets, currencies, hunts, now playing, people and chats, notes, quick toggles, and more); `WidgetCatalog` is the full list. A widget is a class implementing `IHomeWidget` (src/Aetherphone/Core/Home/IHomeWidget.cs):

```csharp
internal interface IHomeWidget : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    string AppId { get; }
    WidgetSizeSet Sizes { get; }
    IReadOnlyList<WidgetOption> Options => WidgetOption.None;
    void Draw(in WidgetContext context);
    WidgetRoute Target(in WidgetContext context) => WidgetRoute.App(AppId);
    float Relevance(string config) => 0f;
}
```

- `Id` names the widget itself (`"calendar.month"`); one app can ship several. `DisplayName` titles the gallery entry and `Description` is the one-line caption under it; both are `Loc.T(...)` arrow properties.
- `AppId` ties the widget to its owning app, and the link does real work: `WidgetRegistry.IsAvailable` answers by asking the app's `IsAvailable`, the gallery only offers widgets of installed, available apps, the default `Target` opens that app, and `HomeLayoutService` drops saved widget tiles whose app is uninstalled or unavailable.
- `Sizes` is a flag set of the sizes you support. On the 4-column home grid, `Small` occupies 2x2 cells, `Medium` 4x2, and `Large` 4x4 (`WidgetSizes` in src/Aetherphone/Core/Home/WidgetSize.cs). Branch on `context.Size` inside `Draw`.
- `Options` lists the per-tile settings a user can change with Edit Widget. The default is none.
- `Target` decides where a tap on the tile goes. The default opens the app at its root; override it to deep-link (see the widget kit below).
- `Relevance` scores the widget from 0 to 1 for one tile's config. A Smart Stack with Smart Rotate on checks its members periodically while the user leaves it alone and turns to a member that leads the visible one by a clear margin; the gallery's featured rail sorts by it too. The default 0 never asks for attention.

`Draw` receives a `WidgetContext`, a readonly struct in the same file. Unlike an app's `Draw`, a widget paints directly onto the given draw list inside `Bounds`; there is no router and no `PhoneContext`. Its fields:

| Field | Meaning |
| --- | --- |
| `DrawList`, `Bounds`, `Scale` | Where to paint and the UI scale to multiply every unit by |
| `Theme`, `Size`, `Delta` | The phone theme, the size being drawn, and the frame time for animation |
| `Opacity` | The home screen's fades (page motion, edit transitions) |
| `Mode`, `Tint` | The icon appearance the user picked (`FullColor`, `Dark`, `Tinted`, `Clear`) and its tint color |
| `Interactive` | Whether this draw may take input; false in previews |
| `Preview` | True when the gallery draws the widget as a preview |
| `InstanceKey` | Unique per placed tile (and per preview size); key per-tile state on it |
| `Config` | This tile's option values |
| `Actions` | The shared `WidgetActions`, for looking up an app or opening a route |

Rules that follow from how it is called:

- The same `Draw` renders the placed tile and the gallery preview, at whatever rectangle the caller picked, so lay out relative to `Bounds` and never assume grid pixel sizes. When you have no live data (logged out, nothing loaded yet), check `Preview` and draw sample content so the gallery still sells the widget; `DailiesWidget` does this.
- `Opacity` must reach every color you draw or your widget will pop while everything around it fades. Colors from `WidgetInk` already carry it.
- Honor `Mode`. Hardcoded colors look wrong in Tinted and Clear; take inks from `WidgetInk.From(context)` and pass your accent through `ink.Accent(color)`.
- Keep per-tile state keyed by `InstanceKey` (`WidgetStates<T>` does the bookkeeping), because the same widget instance draws every placed copy.

### Widget kit

The shipped widgets compose from a small kit in src/Aetherphone/Windows/Widgets/:

- **Background.** Backgrounds are the widget's job; `HomeGridRenderer` paints nothing behind a resting widget tile (the one exception is a drag, where it drops an `Elevation.Floating` shadow under the tile it is carrying). Start `Draw` with `WidgetChrome.Container(context)` for the standard card in the current mode, or `WidgetChrome.Container(context, top, bottom)` for a gradient (Tinted and Clear fall back to the standard glass). Every container uses `WidgetChrome.Radius(scale)`, which keeps every widget's corners in one family. `WidgetChrome.Header(context, ink, appId, label, accent)` draws the app glyph plus caption row and returns its bottom edge, and `WidgetChrome.Message` draws an empty or signed-out state.
- **Ink and layout.** `WidgetInk.From(context)` gives mode-aware `Primary`, `Secondary`, `Tertiary`, `Separator` and `Fill` colors, all multiplied by `Opacity`. `WidgetMetrics.Content(context)` is the inset content rectangle for the current size, `WidgetMetrics.Below(context, top)` the space under a header, and `Gutter`/`RowGap` the spacing units.
- **Text.** `WidgetText.Eyebrow`, `EyebrowFit` and `EyebrowMarquee` draw the small tracked uppercase caption, and `WidgetText.Draw` fits a line to a width; styles come from `WidgetType`. Cache formatted strings in a `CachedText` field keyed on the value; it also refreshes itself when the language or clock format changes.
- **Options.** A `WidgetOption(key, label, choices, defaultValue)` with `WidgetChoice(value, label)` entries (or a callback that fills the choices, for dynamic lists) appears in `WidgetEditSheet`. The values persist per tile as a `key=value` string in `HomeItem.WidgetConfig`; read them with `WidgetConfig.Get(context.Config, key, fallback)`.
- **Routes.** `Target` returns a `WidgetRoute`: `WidgetRoute.App(appId)`, `WidgetRoute.Tab(appId, intent)` for an `ITabRouteTarget`, `WidgetRoute.To(appId, kind, argument)` for the launcher-backed kinds in `WidgetRouteKind` (conversation, direct user, calls, Linkpearl, Muster, market search, radio station, announcement, note, new note, venue, notification), and `WidgetRoute.MarketItem` or `WidgetRoute.Hunt`. On a tap, `WidgetHost.OpenTarget` asks for the route and `WidgetActions.Open` fires the matching launcher, then calls `OpenAppFrom(app, origin, LaunchOrigin.Surface)`.
- **Controls.** `WidgetControls.Button`, `Toggle`, `Link` and `Pressable` give a widget its own tappable parts. Each takes a `controlId` unique within the widget and registers a hit area with `WidgetHits` while `context.Interactive` is true; a press on a control runs the control on release instead of opening the app. `Button` returns true once the press lands, `Toggle` returns the new value (assign it back), and `Link` opens its own `WidgetRoute`.

Widgets live beside their app in `src/Aetherphone/Apps/<App>/Widgets/` (namespace `Aetherphone.Apps.<App>.Widgets`) and take their data from Core services through the constructor. The CI "Verify layering" step lets only `AppRegistry.cs` and `WidgetCatalog.cs` under Core and Windows import `Aetherphone.Apps`, so shared widget code belongs in the kit, not in an app folder.

Registration mirrors apps: `WidgetCatalog.Build` (src/Aetherphone/Windows/Widgets/WidgetCatalog.cs) constructs every widget once at boot, passing each the services it needs, and returns the `WidgetRegistry` that travels in `AppBundle.Widgets`. To ship a widget, construct it there; there is no per-app hook. `PhoneShell.Dispose` disposes every widget alongside the apps at unload.

How one lands on the home screen: long-press the home screen to enter edit mode, then press the add button, which opens `WidgetGallery` (src/Aetherphone/Core/Shell/Home/WidgetGallery.cs). The gallery root has a search field, a featured rail, a Smart Stack card, and a list of apps with widgets; picking an app opens a detail page that swipes through each of its widgets at each supported size as live previews. Its add button calls `HomeLayoutService.AddWidget(widget, size, pageIndex)`, which appends a `HomeTile.ForWidget` tile to the current page, or adds a Smart Stack. The same widget can be placed more than once; each tile gets its own key. In edit mode, tapping a placed widget opens `WidgetContextMenu`: the size choices, Edit Widget (`WidgetEditSheet`, shown only when the widget has `Options`), Edit Stack (`StackEditSheet`, for stacks), and Remove. A corner `WidgetResizeHandle` also resizes a widget with more than one size in place. The saved layout stores, per tile, the widget id, size, instance key and option config, plus the members, visible index and Smart Rotate flag of a stack. The first-run layout seeds one widget, the medium Skywatcher forecast.

A minimal widget, modeled on the Calendar month widget (src/Aetherphone/Apps/Calendar/Widgets/MonthWidget.cs):

```csharp
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Recipes.Widgets;

internal sealed class RecipeWidget : IHomeWidget
{
    private CachedText weekday;

    public string Id => "recipes.today";
    public string DisplayName => Loc.T(L.Apps.Recipes);
    public string Description => Loc.T(L.Recipes.WidgetDescription);
    public string AppId => "recipes";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        WidgetText.EyebrowFit(context.DrawList, content.Min, Weekday(DateTime.Today), content.Width,
            ink.Accent(AppAccents.For(AppId)), scale);
        var titleTop = content.Min.Y + WidgetText.EyebrowHeight() + WidgetMetrics.Gutter * scale;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, titleTop), Loc.T(L.Recipes.Today), ink.Primary,
            WidgetType.Title, content.Width);
    }

    private string Weekday(DateTime today)
    {
        var key = today.Ticks;
        return weekday.IsCurrent(key) ? weekday.Value : weekday.Store(key, today.ToString("dddd", Loc.Culture));
    }

    public void Dispose()
    {
    }
}
```

## Control Center tiles

The Control Center is opened by tapping the band at the top of the screen, and is disabled in landscape mode.

The Control Center is a sibling system with its own contract: each tile is an `IControlModule` (src/Aetherphone/Core/ControlCenter/IControlModule.cs) with `Id`, `GalleryLabel`, `GalleryIcon`, a `Sizes` list of supported `ControlSpan` values, a `DefaultSpan`, and `Draw(in ControlModuleContext context)`. `ControlModuleContext` looks like `WidgetContext` with a few changes: the rectangle is named `Rect` instead of `Bounds`, `Span` replaces `Size`, there is no `Delta` and none of the widget mode, preview or config fields, and it adds `Expansion` (0 to 1, with an `Expanded` shortcut) for the detail view a long press opens. Like widgets, check `Interactive` before reacting to input. Spans on the 4-column grid (`ControlSpans` in src/Aetherphone/Core/ControlCenter/ControlSpan.cs): `Small` 1x1, `Wide` 2x1, `Tall` 1x2, `Large` 2x2, `Bar` 4x1.

You rarely implement the interface directly. src/Aetherphone/Core/ControlCenter/Modules/ has the reusable shapes: `ToggleModule` (id, icon, label, a `Func<bool>` for state and an `Action` on press; also used for one-shot launchers like the camera and settings tiles, whose state always reads false), `ClusterModule` (one Large tile of round toggles; the default cluster holds the `dnd`, `silent`, `calls` and `idle` toggles listed in `ControlDefaults.ClusterMembers`, each of which is also registered as its own `ToggleModule`), `SliderModule` (brightness, volume), `MediaModule`, `AccentModule`, and `CoinModule` (the coin balance tile that opens the Coin app). Every module is constructed in the `ControlRegistry` constructor (src/Aetherphone/Core/ControlCenter/ControlRegistry.cs); adding a tile is usually one more `Add(new ToggleModule(...))` line there. Registering a module only makes it available: to ship it on a new phone out of the box, add a row to `ControlDefaults.Layout` (src/Aetherphone/Core/ControlCenter/ControlDefaults.cs) as well, otherwise it waits in the gallery.

`ControlLayoutService` (src/Aetherphone/Core/ControlCenter/ControlLayoutService.cs) owns which modules are on the grid, in what order and at what span, and solves placement with the same `HomeGridSolver` the home screen uses. Press the customize button in the Control Center header to edit (long-pressing a tile opens its expanded view instead): dragging reorders, the resize handle cycles through the module's `Sizes`, the remove badge takes a tile off, and the add button opens `ControlGallery`, which lists the modules currently off the grid and places an added one at its `DefaultSpan`. The layout persists in `Configuration.ControlPanel` as module ids, spans, an `Enabled` list, and a `Version`. A fresh install (and `Reset`) seeds the grid from `ControlDefaults.Layout`, an ordered list of module ids and spans: the toggles cluster and the media tile (both Large), the brightness and volume sliders (Tall), the lock and settings tiles (Small), and the accent row (Wide), packed hole free into four rows. Modules outside that list start in the gallery. A save older than `ControlLayoutService.LayoutVersion` is migrated once on load: the four standalone toggles fold into the cluster tile. Install semantics are pinned by tests (src/Aetherphone.Tests/ControlLayoutServiceInstallTests.cs): a fresh install ships the default layout in order at the default spans and nothing else, a module shipped in an update after the user's layout was saved stays off the grid until the user adds it, and adds and removes survive restarts.

## Cross-app launchers

Apps never call into each other directly. To deep-link, one app writes an intent into a small launcher service, opens the target by id, and the target consumes the intent when it comes to the front. Every launcher is created in `PhoneServices` (src/Aetherphone/Core/PhoneServices.cs), which is the full list. The cross-app ones in src/Aetherphone/Core/Apps/ are in the table below; the rest sit beside the subsystem they open, for example `LinkpearlLauncher` (Core/GameChat), `MarketLauncher` (Core/Market), `HuntsLauncher` (Core/Hunts) and `AetherStreamLauncher` (Core/Video). `NotificationsApp` and the shell's `NotificationRouter` consume nearly the full set to route notification taps, and widget routes reuse them through `WidgetActions`.

| Launcher | Intent |
| --- | --- |
| `DmLauncher` | Open `MessageApp` (id `"message"`) to a user, a conversation, or the Calls tab (`RequestUser`, `RequestConversation`, `RequestCalls`) |
| `GramDmLauncher` | Open an Aethergram DM to a user, optionally with a prefilled draft |
| `SocialLauncher` | Per-app `SocialDeepLink`: a `SocialLinkKind` (`Profile`, `Post`, or `Requests`) plus an id, keyed by target app id |
| `VelvetLauncher` | Open Velvet to a user |
| `SettingsLauncher` | Open Settings to the Notifications, Privacy or Calls page (`Request(SettingsPageKind)`), or to one app's own page (`RequestAppNotifications(appId)`) |

Each follows the same request/consume shape: `Request...` stores the pending value, `TryConsume...` returns it exactly once and clears it. Two helpers in Core/Apps give a new launcher that shape for free: `LaunchIntent` holds one pending string and `LaunchFlag` one pending bool (`VelvetLauncher` is a single `LaunchIntent`). `MessageApp` is the canonical consumer: from both `OnOpened` and `OnResumed` it checks `TryConsumeCalls`, then `TryConsumeConversation`, then `TryConsumeUser`, and routes accordingly. A resumable target must consume in both places, or in `Draw` as `SettingsApp` does, because a resumed open skips `OnOpened`. If your new app needs to be a deep-link target, add a launcher in this style rather than exposing methods on the app class.

## RefreshCadence: polling from Draw

Immediate mode has no timers of its own, so periodic work is driven from `Draw` with `RefreshCadence` (src/Aetherphone/Core/Apps/RefreshCadence.cs):

```csharp
internal struct RefreshCadence
{
    public bool Advance(float deltaSeconds, float intervalSeconds);
    public void Reset();
}
```

`Advance` accumulates frame time and returns true once the interval has elapsed; you then do the work and call `Reset`. `FishingApp` refreshes its voyage table every 5 seconds this way, and `AetherStreamApp` uses the same pattern for its clipboard and nearby-screen checks. Keep it for cheap local recomputation; network polling belongs in stores with their own cadence (see [Networking](networking.md)).

## Landscape

An app can turn the phone sideways while it is the current app. `AppLandscape` (src/Aetherphone/Core/Apps/AppLandscape.cs) holds one app id at a time: call `AppLandscape.Request(Id)` to take it, `AppLandscape.Release(Id)` to give it back (and always in `OnClosed`), and `AppLandscape.Held(Id)` to branch your layout. While the current app holds it, the shell rotates the device (see [Architecture](architecture.md)) and dismisses the Control Center and the App Switcher. Camera, Games, Strats and the AetherStream theater mode use it. The turn takes a moment, so lay out for landscape only when `Held(Id)` and `context.Content.IsLandscape()` are both true, as `GamesApp` does.

## First-open tour

Most user-facing apps play a short coachmark tour the first time they open. Tours are data in `TourRegistry` (src/Aetherphone/Core/Onboarding/TourRegistry.cs and its per-area partial files): `Add(tours, appId, version, steps)` with `GuideStep` factories such as `Intro`, `Point`, `TryTap` and `TryUntil`, whose copy lives in `L.Onboarding`. Steps point at UI by anchor key, so the app reports the matching rectangles while it draws with `UiAnchors.Report(key, rect)`; `CalculatorApp` reports `calculator.keypad`, `calculator.display`, `calculator.answer` and `calculator.tape`. `OnboardingDirector` starts the tour when the app opens, unless tutorials are off or the user already completed that version; bumping the version replays it for everyone. `TourRegistryTests` (src/Aetherphone.Tests/TourRegistryTests.cs) pins every shipped tour's version and step count, so changing a tour fails the test until you update its row; give a new tour a row too (and bump the table's expected count).

## Gotchas

- **`OnOpened` re-fires on an already-open app.** `NavigationStack.OpenApp` calls `NotifyOpened` even when the app is already current, and `Back()` calls `OnOpened` on the app you return to. Make `OnOpened` idempotent and cheap. A resumable app gets `OnResumed` in these cases instead (always on `Back()`, within the resume window otherwise).
- **A resume skips `OnOpened`.** An `IResumableApp` that consumes launcher intents or shares only in `OnOpened` drops the ones that arrive during a resume. Consume in both `OnOpened` and `OnResumed`, or in `Draw`.
- **`OnShare` arrives before the app opens.** Stash the shared item in a field that survives your `OnOpened` reset, and consume it afterwards. Clearing all pending state at the top of `OnOpened` will eat the share.
- **`Pop(true)` redraws the view you are leaving.** During the slide, `ViewRouter.Draw` keeps calling your draw delegate for the outgoing view. If the pop was caused by the underlying data disappearing, use `Pop(false)` or the outgoing screen will draw against data that no longer exists.
- **`BadgeCount` and `IsAvailable` are read every frame on the home screen.** Return cached values. `IsAvailable`'s default is already cheap (a dictionary lookup that occasionally schedules a background fetch); do not replace it with anything that does IO.
- **`OnClosed` is late.** It fires when the close animation settles (`NavigationStack.FinalizeMotion`), so one more `Draw` or several can happen after the user initiated the close. Do not treat the close gesture itself as your last frame.
- **Exceptions in `Draw` do not close the app.** `ShellScreenPainter.PaintApp` catches per frame, logs, and draws a failure message, then calls you again next frame. A throwing `Draw` becomes a log-spamming error screen, not a crash, so watch the log during development.
- **Missing accent means a gray tile.** `AppAccents.For` returns its fallback for unknown ids. Registering the app without adding an `AppAccents` entry is the usual cause of a colorless icon.
- **Missing store entry fails the tests.** `AppStoreCatalogTests` requires a `StoreEntry` for every app type.
- **`RouterDraw` delegates allocate if created inline.** Passing a method group or lambda directly to `router.Draw` every frame allocates a delegate per frame. Cache it in a field in the constructor, as every shipped app does; the same goes for the back delegate you hand the header.

## Related docs

- [Architecture](architecture.md): plugin boot, services, the frame loop, and how the shell hosts everything described here
- [Creating an app](creating-an-app.md): step-by-step tutorial that applies this contract
- [UI toolkit](ui-toolkit.md): the Windows/Components widget library, typography, metrics, and input handling
- [State and persistence](state-and-persistence.md): Configuration, migrations, and where the home layout, the Control Center layout, and app flags are stored
- [Notifications](notifications.md): notification channels, deep links, and how they interact with badges and launchers
- [Localization](localization.md): `L` keys and `Loc.T`, required for `DisplayName` and all user-facing copy
- [Networking](networking.md): the Aethernet client behind availability flags and the social apps
- [Games framework](games-framework.md): the mini-game layer hosted inside the Games app
