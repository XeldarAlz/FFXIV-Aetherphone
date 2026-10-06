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
| src/Aetherphone/Core/Apps/AppBundle.cs | The apps, widgets, and shared services handed to the shell |
| src/Aetherphone/Core/Apps/AppStoreCatalog.cs | The App Store entry every app needs |
| src/Aetherphone/Core/Apps/PhoneContext.cs | Per-frame draw context: content rect, theme, navigator |
| src/Aetherphone/Core/Apps/AppAccents.cs | Maps app id to the home tile accent color |
| src/Aetherphone/Core/Apps/ViewRouter.cs | In-app screen stack with slide transitions |
| src/Aetherphone/Windows/Components/Skin/AppSkin.cs | Per-app palette plus common widgets (buttons, fields, chips) |
| src/Aetherphone/Windows/Components/Skin/AppPalettes.cs | The palette catalog apps feed into `AppSkin` |
| src/Aetherphone/Windows/Components/Chrome/AppIconTile.cs | Draws the painted icon pair in the chosen appearance |
| src/Aetherphone/Windows/Components/Chrome/AppIconArt.cs | Fallback art on the accent tile when an id has no painted pair |
| src/Aetherphone/Windows/Components/Layout/AppHeader.cs | Title bars: the collapsing large title and the inline bar with a back button |
| src/Aetherphone/Windows/Components/Primitives/Metrics.cs | Spacing, radius, and size tokens |
| src/Aetherphone/Core/Localization/L.cs | Source of truth for every user-facing string |
| src/Aetherphone/Apps/Calculator/CalculatorApp.cs | The minimal real app this tutorial copies from |
| tools/icon-generator/ | Generates the painted `src/Aetherphone/Icons/` pairs from Phosphor glyphs |

Everything here is client side. The backend ("Aethernet") lives in a separate repository; a local app like Counter never touches it.

## Step 1: study a real app

Open src/Aetherphone/Apps/Calculator/CalculatorApp.cs. It is a compact app split into partial files by area (display, keypad, keyboard, history sheet); its only constructor dependencies are `Configuration`, which stores the calculation history, and `ConfirmService` for the Clear History prompt. It shows the whole anatomy:

- **Identity properties.** `Id => "calculator"`, `DisplayName => Loc.T(L.Apps.Calculator)`, `Glyph => "="`, `Accent => AppAccents.For("calculator")`, `BadgeCount => 0`. The id is a stable lowercase key used everywhere: accent lookup, icon file name, availability flags, navigation.
- **An `AppSkin` field.** `private readonly AppSkin ui = new(AppPalettes.Calculator);` bundles the app's palette (inks, backdrop gradient, card fills) with reusable widgets.
- **`Draw(in PhoneContext context)`.** Runs every frame while the app is open. It reads `UiScale.Current` (Dalamud's UI scale times the phone zoom; multiply every pixel constant by it), refreshes `ui.Theme` from the context, paints the backdrop, then lays out the display, keypad and history button with plain rectangle math.
- **Hit testing.** Buttons are drawn shapes plus `UiInteract.Hover(min, max)` and `UiInteract.Click(min, max, hovered)`, which fires on release. Retained state is small: a `Spring` per key for the press animation, a few springs for the display, and the history `Sheet`.
- **Lifecycle members.** `OnOpened` resets per-visit state (the history sheet, the copy menu), `OnClosed` saves pending history, and `Dispose` stays empty because there is nothing to release.

Then skim src/Aetherphone/Apps/Notes/NotesApp.cs for the next tier: a `ViewRouter<NotesScreen>` for multiple screens, the collapsing large title, `Configuration` for persistence, `WantsSystemTheme => true` so the app follows the phone's light/dark theme instead of shipping its own dark palette, and `IResumableApp` so it reopens where the user left it.

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
| `HasBadge` | `false` | Opts into the shared, user-toggleable badge switch, shown as a Badges switch on this app's own page in Settings; apps that always return `BadgeCount => 0` can skip it |
| `BadgeAsDot` | `false` | `true` renders the badge as a dot instead of a number |
| `WantsTransparentScreen` | `false` | Skip the opaque screen fill behind the app |
| `WantsSystemTheme` | `false` | Receive the phone's light/dark theme in `context.Theme` |
| `TransparentViewport(screen, scale)` | `null` | Cut a see-through hole in the phone (the Camera app uses this) |
| `IsAvailable` | `AppAvailability.IsEnabled(Id)` | Server kill switch; unknown ids default to enabled |
| `AcceptedShares` | `ShareKindSet.None` | Which share sheet payloads the app accepts |
| `ShareLabel(kind)` | `null` | Custom share sheet caption per `ShareKind`, returned as a `LocString?` |
| `OnShare(item)` | empty | Receives the shared item |

A few optional interfaces add behavior on top: `IResumableApp` (an `OnResumed` hook so the app reopens where the user left it and appears in the App Switcher), `ITabRouteTarget` (widgets can open a specific tab), and the `ISpotlight*` hooks for Spotlight search. A first app needs none of them; see [optional interfaces](app-framework.md#optional-interfaces) when you do.

`PhoneContext` (src/Aetherphone/Core/Apps/PhoneContext.cs) carries three things: `Content` (the `Rect` you may draw in, already inside the status bar and home indicator), `Theme` (a `PhoneTheme`), and `Navigation` (an `INavigator` for `Back()`, `GoHome()`, `Open(appId)`).

## Step 3: create the folder and class

One folder per app under src/Aetherphone/Apps. Create `src/Aetherphone/Apps/Counter/CounterApp.cs`:

```csharp
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

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
    private CachedText countLabel;

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
            CountLabel(), ui.TitleInk, TextStyles.LargeTitle);

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

    private string CountLabel() =>
        countLabel.IsCurrent(count) ? countLabel.Value : countLabel.Store(count, count.ToString(Loc.Culture));

    public void Dispose()
    {
    }
}
```

`L.Apps.Counter` does not exist yet, so this file will not compile until you do step 6; write the localization entry in the same sitting.

The idioms, drawn from CalculatorApp and NotesApp:

- `SceneChrome.ScreenFrom(content, theme, scale)` expands the content rect back to the full screen so `ui.Backdrop(screen)` can paint the gradient edge to edge.
- `AppHeader.Draw(context, DisplayName)` renders a compact centered title and a back button that calls `context.Navigation.Back()` for you. It keeps a one-screen sample short; most shipped apps use the collapsing large title instead (`AppHeader.BeginLargeTitle` and `EndLargeTitle`, with NotesApp as the template), described in [the app framework](app-framework.md#the-header-and-the-back-button).
- `Typography` draws all text; never call `ImGui.Text` for styled copy. Styles come from the `TextStyles` ladder (see [the UI toolkit](ui-toolkit.md)). The sample fetches `ImGui.GetWindowDrawList()` and passes it to `Typography.DrawCentered` because the overloads without an `ImDrawListPtr` move the ImGui cursor, which has no place in a hand-laid-out `Draw`; CalculatorApp passes the draw list the same way everywhere (`DrawKey`, `DrawLive` and the history rows).
- `CachedText` keeps the formatted number until `count` changes (and refreshes itself when the language changes), so `Draw` does not allocate a new string every frame. Cache every formatted string this way.
- `AppSkin.PillButton` draws the shape, handles hover, and returns `true` on click, all in one call.
- Every layout constant is multiplied by `UiScale.Current`. `Metrics` tokens (`Metrics.Space`, `Metrics.Radius`, `Metrics.Size`) are unscaled values; scale them at the call site.

The example borrows `AppPalettes.Calculator` to stay short. A real app adds its own entry in src/Aetherphone/Windows/Components/Skin/AppPalettes.cs. Most entries are one line over the accent you add in step 5: `public static readonly AppPalette Counter = For("counter");` for an accent-tinted dark gradient, or `Neutral(AppAccents.For("counter"))` for a graphite one like `AppPalettes.Calculator`. An app drawn on plain system surfaces uses a `PhoneTheme`-derived factory method plus `WantsSystemTheme => true` instead (like `AppPalettes.Notes(theme)`, refreshed each frame in `Draw` the way NotesApp does).

## Step 4: register it

Apps are constructed in exactly one place: `AppRegistry.BuildDefault` in src/Aetherphone/Core/Apps/AppRegistry.cs. Its signature takes four parameters (`PhoneServices`, the `VideoSuite` and screen window the AetherStream app needs, and the Linkpearl pop-out manager); you never touch those for a new app. Add a using for your namespace and one line next to the other simple apps, before the `AppStoreApp` line:

```csharp
apps.Add(new CalculatorApp(services.Configuration, services.Confirm));
apps.Add(new CounterApp());
```

Apps with dependencies take them here as constructor arguments from `PhoneServices` (compare `new NotesApp(services.Configuration, services.Confirm)`). The finished list is wrapped in an `AppBundle` and handed to the shell. Construction happens only here; the App Store entry below, the accent in step 5, and the strings in step 6 complete the registration.

**App Store entry.** Every app needs a `StoreEntry` in src/Aetherphone/Core/Apps/AppStoreCatalog.cs. `StoreEntry(LocString Name, LocString Subtitle, LocString Body, StoreCategory Category)` takes the app name plus a subtitle and a description from `L.StoreCopy` (you add both strings in step 6):

```csharp
["counter"] = new(L.Apps.Counter, L.StoreCopy.CounterSub, L.StoreCopy.CounterBody, StoreCategory.Utilities),
```

`AppStoreCatalogTests` fails until every app type has exactly one entry, and each category must keep between three and eight apps, so pick a category with room.

Who sees it after that:

- **Fresh installs** get every available app installed (`HomeLayoutService.SeedInstalled` in src/Aetherphone/Core/Home/HomeLayoutService.cs). Placement comes from the curated `DefaultFirstPageApps` and `DefaultSecondPageApps` lists in the same file; an app on neither list lands after them on the last page, in display-name order. Add your id to one of the lists if it needs a set spot; `AccentRingTests` checks that the second page never puts like colors side by side.
- **Existing users** keep their saved layout. Your new app is not force-installed; it shows up in the App Store app (its Today tab lists apps that are not installed yet) and lands on the home screen when the user installs it.

## Step 5: accent color and icon

**Accent.** Add your id to the dictionary in src/Aetherphone/Core/Apps/AppAccents.cs, picking a token from the accent ring:

```csharp
["counter"] = AccentRing.Azure,
```

Do not write a raw color literal here. `AccentRing` (src/Aetherphone/Core/Theme/AccentRing.cs) is a ring of fourteen named hues tuned to a shared tile luminance (`TileLuminance`, 0.285) so a white glyph stays readable on every tile, and src/Aetherphone.Tests/AccentRingTests.cs runs every id in the dictionary through a 3.0:1 white-glyph contrast floor (plus checks that the ring hues stay apart and the default second home page never puts like colors side by side): an untuned literal fails `dotnet test`. Every existing entry uses an `AccentRing.*` token (or a `BrandAccents.*` token for the three brand-locked social apps), so a new app reuses whichever ring token fits it.

Without an entry, `AppAccents.For` returns the grey fallback and your home tile looks unfinished.

**Icon.** The home tile (`HomeTileView.DrawApp` in src/Aetherphone/Windows/Components/Chrome/HomeTileView.cs) resolves art in three steps:

1. `AppIconTile.TryDraw` draws the painted pair `Icons/<Id>.png` and `Icons/<Id>.fg.png` when both exist, in the appearance the user picked. Source files live in src/Aetherphone/Icons and are copied to the output by the csproj (`Icons\*.png`).
2. If either file is missing, the tile draws your accent with `AppIconArt` on top: a stencil PNG if one exists, or a procedural icon for the ids its `switch` lists.
3. If both miss, the tile falls back to your `Glyph` letter.

So for a normal app: add a painted pair named `counter.png` and `counter.fg.png` to src/Aetherphone/Icons. To generate one, add a `counter` entry to the `map` in tools/icon-generator/generate-painted-icons.mjs (`icon("<phosphor-name>", "colour", "<Hue>")`, using the same hue as your accent) and run `node generate-painted-icons.mjs counter` there (see tools/icon-generator/README.md). For a hand-painted icon, point the artist at the [icon spec](https://aetherphone.net/icon-spec/) and the [icon checker](https://aetherphone.net/icon-checker/). Always ship both files together, and see [assets and media](assets-and-media.md) for the wider asset story.

## Step 6: localize the name

`DisplayName` must come from the localization catalog, never a hardcoded literal, and the App Store entry from step 4 needs its two strings. Two touch points:

1. Add the name to the `Apps` class and the two store strings to the `StoreCopy` class in src/Aetherphone/Core/Localization/L.cs. Each `LocString` pairs a key with the English source text:

```csharp
public static readonly LocString Counter = new("app.counter", "Counter");
```

```csharp
public static readonly LocString CounterSub = new("storeCopy.counterSub", "Tap to count");
public static readonly LocString CounterBody = new("storeCopy.counterBody",
    "A big number and two buttons, for counting anything you like.");
```

2. Add the `"app.counter"`, `"storeCopy.counterSub"`, and `"storeCopy.counterBody"` keys with their translations to all nine JSON catalogs in src/Aetherphone/Localization. Every key change lands in L.cs plus all nine files in the same commit. Two nets catch drift: a DEBUG launch warns about missing keys via `LocAudit`, and `LocalizationParityTests` in src/Aetherphone.Tests fails `dotnet test` outright when any catalog is missing a key declared in L.cs or carries a key L.cs no longer declares. The full sync workflow, copy rules, and plural handling are in [localization](localization.md).

Any other strings your screens show follow the same pattern: a `LocString` in the matching `L` group, `Loc.T(...)` at draw time.

## Step 7: optional level-ups

Each of these is one small addition; each links to the doc that owns the details.

- **More screens.** Give the app an enum of screens and a `ViewRouter<TScreen>` (src/Aetherphone/Core/Apps/ViewRouter.cs). Construct it with the root screen, cache the `RouterDraw<TScreen>` delegate and a `back = () => router.Pop();` delegate in fields, then call `router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView)` from `Draw` and `router.Push`, `router.Pop`, `router.Reset` to navigate. Each screen draws the large title and passes the back delegate on pushed screens. NotesApp is the template. Details and a full two-screen sample in [the app framework](app-framework.md#the-header-and-the-back-button).
- **Persist state.** Take `Configuration` (src/Aetherphone/Configuration.cs) as a constructor argument, pass `services.Configuration` in AppRegistry, add a property for your data, and call `configuration.Save()` after mutations, exactly as NotesApp does with `configuration.Notes`. Details in [state and persistence](state-and-persistence.md).
- **Post a notification.** Take `NotificationService` (`services.Notifications`) and call `notifications.Notify(new PhoneNotification(Id, title, body, DateTime.Now, Accent));`. See src/Aetherphone/Apps/Announcements/AnnouncementsStore.cs for a real call. Also register a channel for your id in `NotificationChannels.All` (src/Aetherphone/Core/Notifications/NotificationChannels.cs), for example `new("counter", L.Apps.Counter, AppAccents.For("counter")),`: without one, your app's page in Settings shows no Allow Notifications, Banners, or Sounds switches, so users cannot mute it. Posts from an app the user has not installed are dropped. See [notifications](notifications.md) for channels, sounds, and deep links.
- **Badge count.** Return a live number from `BadgeCount` (compare `AnnouncementsApp`: `store.UnreadCount`), or set `BadgeAsDot => true` for a dot. The getter runs every frame the home screen is visible, so keep it a cheap field or property read; the user's on/off preference is applied by the caller, not the getter. Also override `HasBadge => true`, or the badge always shows and your app's page in Settings never gets a Badges switch. See [Notifications](notifications.md#hiding-a-badge).
- **Accept shares.** Declare `AcceptedShares => ShareKindSet.Photo`, implement `OnShare(in ShareItem item)` to stash `item.LocalPath`, and optionally `ShareLabel(ShareKind kind)` for a custom caption. SettingsApp (src/Aetherphone/Apps/Settings/SettingsApp.cs) is a compact example. Note the order in `ShareService`: `OnShare` fires first, then the navigator opens your app, so stash the payload and consume it in `Draw` (as SettingsApp and AethergramApp do), which works whether the app opened fresh or resumed. Consuming in `OnOpened` alone misses shares into a resumable app.
- **Resume where the user left off.** Implement `IResumableApp` instead of `IPhoneApp` and add `public void OnResumed()`. Reopened within ten minutes of closing, or reached with Back, the app gets `OnResumed` instead of `OnOpened`, and it shows up in the App Switcher. Reset your router in `OnOpened` only, and consume launcher intents in both methods. Details in [the app framework](app-framework.md#resuming-iresumableapp).
- **First-open tour.** Add a coachmark tour for your id in `TourRegistry` (src/Aetherphone/Core/Onboarding/), put its copy in `L.Onboarding`, and report the rectangles its steps point at with `UiAnchors.Report(key, rect)` while you draw (CalculatorApp reports its keypad, display, answer, and tape). Then add a row for it to the pinned table in `TourRegistryTests`. Details in [the app framework](app-framework.md#first-open-tour).

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
    bool IsHidden => false;
    string? GuideAnchor => null;
    ReadOnlySpan<SettingsEntry> Entries => ReadOnlySpan<SettingsEntry>.Empty;
    void Draw(in PhoneContext context, Rect body);
}
```

`Title`, `Summary`, `Icon`, and `Tint` feed the page's row on the Settings root screen: the tinted icon tile, the label, and the grey side text (`string.Empty` is a fine `Summary`). The five defaulted members:

- `Entries` lists the settings on your page that Settings search should find. Each `SettingsEntry` wraps the same `LocString` your row draws (optionally with a section label), so a search for it shows a result that opens your page and highlights the row. Declare it on every new page.
- `ShowsBadge` puts a badge on the page's row (`ChangelogPage` uses it for unseen notes).
- `OwnsChrome` skips the shared title bar so the page draws its own.
- `IsHidden` keeps the page off the root list, out of Settings search, and out of Spotlight (Linked Devices stays hidden until it is unlocked).
- `GuideAnchor` reports the row's rectangle under that key for the Settings tour.

The steps:

1. Create your page class in src/Aetherphone/Apps/Settings/Pages. GeneralPage (src/Aetherphone/Apps/Settings/Pages/GeneralPage.cs) is the closest real template: toggle cards plus hint text, backed by `Configuration`, with a `Searchable` array behind `Entries`.
2. Register it in the `SettingsApp` constructor (src/Aetherphone/Apps/Settings/SettingsApp.cs): construct the page and add it to one of the bare `ISettingsPage[]` arrays inside the `groups` array. There is no group type and no footer text; each inner array simply renders as one grouped card on the root screen (`RootSettingsPage.DrawGroups`). That is the only registration: the same array feeds Settings search and Spotlight, and `RootSettingsPage` draws one `SettingsRow.Link` row per page and calls `ISettingsNavigator.Open(page)` on tap, which pushes your page onto the Settings `ViewRouter`.
3. Fill in `Draw`. Unless you set `OwnsChrome => true`, `SettingsApp.DrawPage` wraps your page in the collapsing large title (`AppHeader.BeginLargeTitle` and `EndLargeTitle`) with your `Title` and a back control, and hands you the `body` rect below the title band. The title collapses as your `AppSurface` scrolls, so wrap content in `using (AppSurface.Begin(body))`, then compose `SettingsSection.Header`, `GroupCard.Begin(theme, rowCount)` with one `card.NextRow()` per row and `card.End()` after, and `SettingsSection.Hint` for footer text.
4. Localize `Title`, `Summary`, and every label exactly as in step 6, in the `Settings` group of L.cs.

A minimal page with one persisted toggle. The `L.Settings` entries and the `Configuration` property are yours to add first (step 6 and the persist-state level-up cover both):

```csharp
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ExamplePage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.ExampleToggle),
    };

    public string Title => Loc.T(L.Settings.Example);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Star;
    public Vector4 Tint => new(0.36f, 0.62f, 0.96f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
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

`FontAwesomeIcon.Star` is already declared in `IconPlan.FontAwesomeCodepoints` (src/Aetherphone/Core/IconPlan.cs). If your page draws a `FontAwesomeIcon` nothing else uses yet, add its codepoint there, or `IconFontCoverageTests` fails.

## Pre-PR checklist

- `dotnet build Aetherphone.sln --configuration Release` succeeds (this is what CI builds).
- `dotnet test Aetherphone.sln` passes; add tests under src/Aetherphone.Tests if your app has testable logic (see [testing and release](testing-and-release.md)).
- Tested in game: load the dev plugin and run its command. A Release build answers `/phone`; a Debug build is named AetherphoneDev and answers `/phonedev` (or `/aetherphonedev`) instead, per the DEBUG gate in src/Aetherphone/Core/AepConstants.cs. Open the app, click through every screen. Appending `test` to the command (`/phone test` or `/phonedev test`) posts a sample notification (posted under the Linkpearl app id, "messages") to sanity-check the notification pipeline.
- Localization: `LocString` entries in L.cs (the name and the two `StoreCopy` strings), keys present in all nine JSONs, no `[Loc]` warnings in a DEBUG launch.
- Registration: an accent entry in AppAccents.cs, a `StoreEntry` in AppStoreCatalog.cs, and the painted icon pair `<Id>.png` plus `<Id>.fg.png` in src/Aetherphone/Icons.
- Style matches [conventions](conventions.md): explicit accessibility keywords, braces on every branch, early returns, no LINQ or per-frame allocations in `Draw`, no em dash characters in any copy.

## Gotchas

- **`BadgeCount` has no default.** The interface defaults most flags but not `BadgeCount`; forgetting `public int BadgeCount => 0;` is a compile error that surprises people who skimmed the flag list.
- **Never cache `Loc.T` results at construction:** a string stored in a constructor stays frozen when the user switches languages, so keep `DisplayName` and friends as arrow properties and pass `LocString` (not translated `string`) across constructor boundaries. Full story: [localization](localization.md).
- **Existing users will not see your app on their home screen.** Saved layouts only install what they already list; only fresh installs seed everything. Your app appears in the App Store for them, and `INavigator.Open(appId)` silently no-ops for apps the user has not installed.
- **Draw exceptions are swallowed per frame.** `ShellScreenPainter.PaintApp` wraps `app.Draw` in a try/catch, logs `[shell] app-draw <id> threw`, and paints a generic failure message. If your app renders as a single sad sentence, check the Dalamud log; nothing will crash loudly for you.
- **`Typography.Draw` and `Typography.DrawCentered` overloads without an `ImDrawListPtr` move the ImGui cursor:** inside hand-laid-out surfaces, always pass `ImGui.GetWindowDrawList()` explicitly. Full story: [UI toolkit](ui-toolkit.md).
- **Ship the painted icon pair, named exactly after your `Id`.** `<Id>.png` is the finished, opaque tile and `<Id>.fg.png` its foreground; `AppIconTile` draws them as painted only when both exist. A lone `<Id>.png` is treated as a white-on-transparent stencil instead: `AppIconTextures` tints it with the ink color via `AddImage` on top of your accent tile, so a painted icon missing its `.fg.png` renders wrong. A mismatched file name ships the letter-glyph fallback.
- **`OnOpened` is not once-per-visit:** it re-fires when an already-open app is opened again (deep links depend on this), so make `OnOpened` and `OnClosed` idempotent. A resumable app gets `OnResumed` instead in those cases. Full story: [notifications](notifications.md) and [the app framework](app-framework.md#resuming-iresumableapp).

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
