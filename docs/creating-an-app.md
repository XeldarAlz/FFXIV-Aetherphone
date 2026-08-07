# Creating your own app

This tutorial walks you through building a new phone app from zero to working, using a tiny "Counter" app as the running example. Read it after [getting started](getting-started.md) (you can build the plugin and load it in game) and skim [the app framework](app-framework.md) alongside it for the concepts behind each step.

Two terms you will meet constantly:

- **Dalamud** is the plugin framework that hosts community plugins inside Final Fantasy XIV. Aetherphone is one such plugin.
- **Dear ImGui** is an immediate mode UI library. There is no retained widget tree: your draw code runs every frame, redraws everything, and reads input inline. A "button" is a rectangle you draw plus a click test you perform in the same call.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Apps/IPhoneApp.cs | The interface every phone app implements |
| src/Aetherphone/Core/Apps/AppRegistry.cs | Constructs every app instance in `BuildDefault` |
| src/Aetherphone/Core/Apps/AppBundle.cs | The apps, widgets, and photo library handed to the shell |
| src/Aetherphone/Core/Apps/PhoneContext.cs | Per-frame draw context: content rect, theme, navigator |
| src/Aetherphone/Core/Apps/AppAccents.cs | Maps app id to the home tile accent color |
| src/Aetherphone/Core/Apps/ViewRouter.cs | In-app screen stack with slide transitions |
| src/Aetherphone/Windows/Components/AppSkin.cs | Per-app palette plus common widgets (buttons, fields, chips) |
| src/Aetherphone/Windows/Components/AppPalettes.cs | The palette catalog apps feed into `AppSkin` |
| src/Aetherphone/Windows/Components/AppIconArt.cs | Icon resolution entry point with procedural fallbacks |
| src/Aetherphone/Windows/Components/AppIconTextures.cs | Loads `Icons/<id>.png` and draws it tinted |
| src/Aetherphone/Windows/Components/AppHeader.cs | Standard title bar with back button |
| src/Aetherphone/Windows/Components/Metrics.cs | Spacing, radius, and size tokens |
| src/Aetherphone/Core/Localization/L.cs | Source of truth for every user-facing string |
| src/Aetherphone/Apps/Calculator/CalculatorApp.cs | The minimal real app this tutorial copies from |
| tools/icon-generator/ | Regenerates `src/Aetherphone/Icons/*.png` from Tabler Icons |

Everything here is client side. The backend ("Aethernet") lives in a separate repository; a local app like Counter never touches it.

## Step 1: study a real app

Open src/Aetherphone/Apps/Calculator/CalculatorApp.cs. It is a small, self-contained app with no constructor dependencies and shows the whole anatomy:

- **Identity properties.** `Id => "calculator"`, `DisplayName => Loc.T(L.Apps.Calculator)`, `Glyph => "="`, `Accent => AppAccents.For("calculator")`, `BadgeCount => 0`. The id is a stable lowercase key used everywhere: accent lookup, icon file name, availability flags, navigation.
- **An `AppSkin` field.** `private readonly AppSkin ui = new(AppPalettes.Calculator);` bundles the app's palette (inks, backdrop gradient, card fills) with reusable widgets.
- **`Draw(in PhoneContext context)`.** Runs every frame while the app is open. It reads `UiScale.Current` (Dalamud's UI scale times the phone zoom; multiply every pixel constant by it), refreshes `ui.Theme` from the context, paints the backdrop, draws the header, then lays out content with plain rectangle math.
- **Hit testing.** Buttons are drawn shapes plus `UiInteract.Hover(min, max)` and `ImGui.IsMouseClicked(...)` checks. No retained state.
- **Empty lifecycle members.** `OnOpened`, `OnClosed`, and `Dispose` can be empty when there is nothing to set up or tear down.

Then skim src/Aetherphone/Apps/Notes/NotesApp.cs for the next tier: a `ViewRouter<NotesScreen>` for multiple screens, `Configuration` for persistence, and `WantsSystemTheme => true` so the app follows the phone's light/dark theme instead of shipping its own dark palette.

## Step 2: know the contract

src/Aetherphone/Core/Apps/IPhoneApp.cs is the ground truth. Trimmed to the members that have no default, it looks like this:

```csharp
internal interface IPhoneApp : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Glyph { get; }
    int BadgeCount { get; }

    void OnOpened();
    void OnClosed();
    void Draw(in PhoneContext context);
}
```

The full contract, including every defaulted member (sharing, transparency, availability), is listed in [the app framework](app-framework.md).

You must implement `Id`, `DisplayName`, `Glyph`, `BadgeCount`, `OnOpened`, `OnClosed`, `Draw`, and `Dispose` (from `IDisposable`). Everything else has a sensible default:

| Member | Default | Meaning |
| --- | --- | --- |
| `Accent` | `AppAccents.For(Id)` | Home tile color; unknown ids get a grey fallback |
| `BadgeAsDot` | `false` | `true` renders the badge as a dot instead of a number |
| `WantsTransparentScreen` | `false` | Skip the opaque screen fill behind the app |
| `WantsSystemTheme` | `false` | Receive the phone's light/dark theme in `context.Theme` |
| `TransparentViewport(screen, scale)` | `null` | Cut a see-through hole in the phone (the Camera app uses this) |
| `IsAvailable` | `AppAvailability.IsEnabled(Id)` | Server kill switch; unknown ids default to enabled |
| `AcceptedShares` | `ShareKindSet.None` | Which share sheet payloads the app accepts |
| `ShareLabel(kind)` | `null` | Custom share sheet caption per `ShareKind`, returned as a `LocString?` |
| `OnShare(item)` | empty | Receives the shared item |

`PhoneContext` (src/Aetherphone/Core/Apps/PhoneContext.cs) carries three things: `Content` (the `Rect` you may draw in, already inside the status bar and home indicator), `Theme` (a `PhoneTheme`), and `Navigation` (an `INavigator` for `Back()`, `GoHome()`, `Open(appId)`).

## Step 3: create the folder and class

One folder per app under src/Aetherphone/Apps. Create `src/Aetherphone/Apps/Counter/CounterApp.cs`:

```csharp
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Counter;

internal sealed class CounterApp : IPhoneApp
{
    public string Id => "counter";
    public string DisplayName => Loc.T(L.Apps.Counter);
    public string Glyph => "C";
    public Vector4 Accent => AppAccents.For("counter");
    public int BadgeCount => 0;

    private readonly AppSkin ui = new(AppPalettes.Calculator);
    private int count;

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        var scale = UiScale.Current;
        ui.Theme = context.Theme;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);
        AppHeader.Draw(context, DisplayName);

        var drawList = ImGui.GetWindowDrawList();
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, content.Center.Y - 40f * scale),
            count.ToString(Loc.Culture), ui.TitleInk, TextStyles.LargeTitle);

        var buttonWidth = 120f * scale;
        var buttonHeight = 40f * scale;
        var buttonTop = content.Center.Y + 20f * scale;
        var gap = Metrics.Space.Sm * scale;
        var minus = new Rect(new Vector2(content.Center.X - buttonWidth - gap, buttonTop),
            new Vector2(content.Center.X - gap, buttonTop + buttonHeight));
        var plus = new Rect(new Vector2(content.Center.X + gap, buttonTop),
            new Vector2(content.Center.X + buttonWidth + gap, buttonTop + buttonHeight));
        if (ui.PillButton(minus, "-", false))
        {
            count--;
        }

        if (ui.PillButton(plus, "+", true))
        {
            count++;
        }
    }

    public void Dispose()
    {
    }
}
```

`L.Apps.Counter` does not exist yet, so this file will not compile until you do step 6; write the localization entry in the same sitting.

The idioms, all copied from CalculatorApp and NotesApp:

- `SceneChrome.ScreenFrom(content, theme, scale)` expands the content rect back to the full screen so `ui.Backdrop(screen)` can paint the gradient edge to edge.
- `AppHeader.Draw(context, DisplayName)` renders the centered title and a back button that calls `context.Navigation.Back()` for you.
- `Typography` draws all text; never call `ImGui.Text` for styled copy. Styles come from the `TextStyles` ladder (see [the UI toolkit](ui-toolkit.md)). The sample fetches `ImGui.GetWindowDrawList()` and passes it to `Typography.DrawCentered` because the overloads without an `ImDrawListPtr` move the ImGui cursor, which has no place in a hand-laid-out `Draw`; CalculatorApp passes the draw list the same way.
- `AppSkin.PillButton` draws the shape, handles hover, and returns `true` on click, all in one call.
- Every layout constant is multiplied by `UiScale.Current`. `Metrics` tokens (`Metrics.Space`, `Metrics.Radius`, `Metrics.Size`) are unscaled values; scale them at the call site.

The example borrows `AppPalettes.Calculator` to stay short. A real app adds its own entry in src/Aetherphone/Windows/Components/AppPalettes.cs, either a static `AppPalette` (dark bespoke backdrop, like `AppPalettes.Calculator`) or a `PhoneTheme`-derived factory method plus `WantsSystemTheme => true` (like `AppPalettes.Notes(theme)`, refreshed each frame in `Draw` the way NotesApp does).

## Step 4: register it

Apps are constructed in exactly one place: `AppRegistry.BuildDefault(PhoneServices services)` in src/Aetherphone/Core/Apps/AppRegistry.cs. Add a using for your namespace and one line next to the other simple apps, before the `AppStoreApp` line:

```csharp
apps.Add(new CalculatorApp());
apps.Add(new CounterApp());
```

Apps with dependencies take them here as constructor arguments from `PhoneServices` (compare `new NotesApp(services.Configuration, services.Confirm)`). The finished list is wrapped in an `AppBundle` and handed to the shell; there is no other registration.

Who sees it after that:

- **Fresh installs** get every available app installed and placed automatically (`HomeLayoutService.SeedInstalled` in src/Aetherphone/Core/Home/HomeLayoutService.cs).
- **Existing users** keep their saved layout. Your new app is not force-installed; it shows up in the App Store app (its Today tab lists apps that are not installed yet) and lands on the home screen when the user installs it.
- Optionally add a `StoreEntry` for your id in src/Aetherphone/Apps/AppStore/AppStoreCatalog.cs so the store shows a real subtitle, description, and category instead of the generic fallback.

## Step 5: accent color and icon

**Accent.** Add your id to the dictionary in src/Aetherphone/Core/Apps/AppAccents.cs:

```csharp
["counter"] = new(0.36f, 0.62f, 0.96f, 1f),
```

Without an entry, `AppAccents.For` returns the grey fallback and your home tile looks unfinished.

**Icon.** The home tile (`HomeTileView.DrawApp` in src/Aetherphone/Windows/Components/HomeTileView.cs) calls `AppIconArt.TryDraw`, which resolves art in three steps:

1. `AppIconTextures.TryDraw` looks for `Icons/<Id>.png` next to the plugin assembly and draws it tinted. Source PNGs live in src/Aetherphone/Icons and are copied to the output by the csproj (`Icons\*.png`).
2. If no PNG exists, `AppIconArt` checks its `switch` of procedural vector icons. Only the mini-games use this path.
3. If both miss, the tile falls back to your `Glyph` letter.

So for a normal app: add a 256 px white-on-transparent PNG named `counter.png` to src/Aetherphone/Icons. The project generates these from Tabler Icons; add a `counter` entry mapping your id to a Tabler icon name in the `map` in tools/icon-generator/generate-app-icons.mjs and run `npm run build` there (see tools/icon-generator/README.md, and [assets and media](assets-and-media.md) for the wider asset story). Ship white art: the renderer tints it to the theme ink at draw time, so baked-in colors get multiplied away.

## Step 6: localize the name

`DisplayName` must come from the localization catalog, never a hardcoded literal. Two touch points:

1. Add the entry to the `Apps` class in src/Aetherphone/Core/Localization/L.cs. The `LocString` pairs a key with the English source text:

```csharp
public static readonly LocString Counter = new("app.counter", "Counter");
```

2. Add the `"app.counter"` key with its translation to all nine JSON catalogs in src/Aetherphone/Localization. Every key change lands in L.cs plus all nine files in the same commit, and a DEBUG launch warns about missing keys via `LocAudit`; the full sync workflow, copy rules, and plural handling are in [localization](localization.md).

Any other strings your screens show follow the same pattern: a `LocString` in the matching `L` group, `Loc.T(...)` at draw time.

## Step 7: optional level-ups

Each of these is one small addition; each links to the doc that owns the details.

- **More screens.** Give the app an enum of screens and a `ViewRouter<TScreen>` (src/Aetherphone/Core/Apps/ViewRouter.cs). Construct it with the root screen, cache the `RouterDraw<TScreen>` delegate in a field, then call `router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView)` from `Draw` and `router.Push`, `router.Pop`, `router.Reset` to navigate. NotesApp is the template. Details in [the app framework](app-framework.md).
- **Persist state.** Take `Configuration` (src/Aetherphone/Configuration.cs) as a constructor argument, pass `services.Configuration` in AppRegistry, add a property for your data, and call `configuration.Save()` after mutations, exactly as NotesApp does with `configuration.Notes`. Details in [state and persistence](state-and-persistence.md).
- **Post a notification.** Take `NotificationService` (`services.Notifications`) and call `notifications.Notify(new PhoneNotification(Id, title, body, DateTime.Now, Accent));`. See src/Aetherphone/Apps/Announcements/AnnouncementsStore.cs for a real call, and [notifications](notifications.md) for channels, sounds, and deep links.
- **Badge count.** Return a live number from `BadgeCount` (compare `AnnouncementsApp`: `store.UnreadCount`), or set `BadgeAsDot => true` for a dot. The getter runs every frame the home screen is visible, so keep it a cheap field or property read.
- **Accept shares.** Declare `AcceptedShares => ShareKindSet.Photo`, implement `OnShare(in ShareItem item)` to stash `item.LocalPath`, and optionally `ShareLabel(ShareKind kind)` for a custom caption. SettingsApp (src/Aetherphone/Apps/Settings/SettingsApp.cs) is a compact example. Note the order in `ShareService`: `OnShare` fires first, then the navigator opens your app, so stash the payload and consume it on the next `Draw` or in `OnOpened`.

## Add a Settings page

[CONTRIBUTING.md](../CONTRIBUTING.md) pitches a Settings page as a typical good first issue, and it is a smaller job than a whole app. Pages implement `ISettingsPage` (src/Aetherphone/Apps/Settings/ISettingsPage.cs), not `IPhoneApp`:

```csharp
internal interface ISettingsPage
{
    string Title { get; }
    string Summary { get; }
    FontAwesomeIcon Icon { get; }
    Vector4 Tint { get; }
    bool ShowsBadge => false;
    bool OwnsChrome => false;
    string? GuideAnchor => null;
    void Draw(in PhoneContext context, Rect body);
}
```

`Title`, `Summary`, `Icon`, and `Tint` feed the page's row on the Settings root screen: the tinted icon tile, the label, and the grey side text (`string.Empty` is a fine `Summary`). The three defaulted members can stay defaulted for a first page.

The steps:

1. Create your page class in src/Aetherphone/Apps/Settings/Pages. ImmersionPage (src/Aetherphone/Apps/Settings/Pages/ImmersionPage.cs) is the closest real template: toggle cards plus hint text, backed by `Configuration`.
2. Register it in the `SettingsApp` constructor (src/Aetherphone/Apps/Settings/SettingsApp.cs): construct the page and add it to one of the `ISettingsPage[]` arrays inside the `groups` array (each one becomes a `SettingsGroup`, optionally with a footer `LocString`). That is the only registration; `RootSettingsPage` draws one `SettingsRow.Link` row per page and calls `ISettingsNavigator.Open(page)` on tap, which pushes your page onto the Settings `ViewRouter`.
3. Fill in `Draw`. `SettingsApp.DrawPage` renders the `AppHeader` with your `Title` and a back button before calling you (unless you set `OwnsChrome => true`), so you only draw inside the `body` rect. The house style: wrap content in `using (AppSurface.Begin(body))` for a scrollable surface, then compose `SettingsSection.Header`, `GroupCard.Begin(theme, rowCount)` with one `card.NextRow()` per row and `card.End()` after, and `SettingsSection.Hint` for footer text.
4. Localize `Title`, `Summary`, and every label exactly as in step 6, in the `Settings` group of L.cs.

A minimal page with one persisted toggle. The `L.Settings` entries and the `Configuration` property are yours to add first (step 6 and the persist-state level-up cover both):

```csharp
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ExamplePage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Example);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Star;
    public Vector4 Tint => new(0.36f, 0.62f, 0.96f, 1f);
    private readonly Configuration configuration;

    public ExamplePage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Example), theme);
            var card = GroupCard.Begin(theme, 1);
            var enabled = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ExampleToggle),
                configuration.ExampleEnabled, theme);
            card.End();
            if (enabled != configuration.ExampleEnabled)
            {
                configuration.ExampleEnabled = enabled;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.ExampleHint), theme);
        }
    }
}
```

Note that `SettingsRow.Bool` returns the new value, not "was it clicked": compare it against the stored value and call `configuration.Save()` only on change, as the skeleton does.

## Pre-PR checklist

- `dotnet build Aetherphone.sln --configuration Release` succeeds (this is what CI builds).
- `dotnet test Aetherphone.sln` passes; add tests under src/Aetherphone.Tests if your app has testable logic (see [testing and release](testing-and-release.md)).
- Tested in game: load the dev plugin, run `/phone`, open the app, click through every screen. `/phone test` posts a sample notification (posted under the Linkpearl app id, "messages") to sanity-check the notification pipeline.
- Localization: `LocString` entries in L.cs, keys present in all nine JSONs, no `[Loc]` warnings in a DEBUG launch.
- Accent entry in AppAccents.cs and an icon PNG named exactly after your `Id`.
- Style matches [conventions](conventions.md): explicit accessibility keywords, braces on every branch, early returns, no LINQ in per-frame code, no em dash characters in any copy.

## Gotchas

- **`BadgeCount` has no default.** The interface defaults most flags but not `BadgeCount`; forgetting `public int BadgeCount => 0;` is a compile error that surprises people who skimmed the flag list.
- **Never cache `Loc.T` results at construction:** a string stored in a constructor stays frozen when the user switches languages, so keep `DisplayName` and friends as arrow properties and pass `LocString` (not translated `string`) across constructor boundaries. Full story: [localization](localization.md).
- **Existing users will not see your app on their home screen.** Saved layouts only install what they already list; only fresh installs seed everything. Your app appears in the App Store for them, and `INavigator.Open(appId)` silently no-ops for apps the user has not installed.
- **Draw exceptions are swallowed per frame.** `ShellScreenPainter.PaintApp` wraps `app.Draw` in a try/catch, logs `[shell] app-draw <id> threw`, and paints a generic failure message. If your app renders as a single sad sentence, check the Dalamud log; nothing will crash loudly for you.
- **`Typography.Draw` and `Typography.DrawCentered` overloads without an `ImDrawListPtr` move the ImGui cursor:** inside hand-laid-out surfaces, always pass `ImGui.GetWindowDrawList()` explicitly. Full story: [UI toolkit](ui-toolkit.md).
- **Icon PNGs must be white on transparent and named `<Id>.png` exactly.** `AppIconTextures` tints the texture with the ink color via `AddImage`; colored pixels get multiplied, and a mismatched file name means the letter-glyph fallback ships instead.
- **`OnOpened` is not once-per-visit:** it re-fires when an already-open app is opened again (deep links depend on this), so make `OnOpened` and `OnClosed` idempotent. Full story: [notifications](notifications.md).

## Related docs

- [Getting started](getting-started.md): build, load the dev plugin, dev loop
- [Architecture](architecture.md): where apps sit in the frame loop
- [App framework](app-framework.md): the contract, registry, navigation, badges, sharing in depth
- [UI toolkit](ui-toolkit.md): Typography, Metrics, UiInteract, and the widget library
- [State and persistence](state-and-persistence.md): Configuration and per-character data
- [Localization](localization.md): L.cs, the nine JSONs, copy rules
- [Notifications](notifications.md): channels, deep links, sounds, badges
- [Assets and media](assets-and-media.md): icons, fonts, sounds, generator tools
- [Testing and release](testing-and-release.md): tests, CI, versioning
- [Conventions](conventions.md): code style and performance rules
