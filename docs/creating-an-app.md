# Creating your own app

This tutorial builds a small phone app from an empty folder to a tile on the home screen, using a "Counter" app (a big number and two buttons) as the running example, and ends with a recipe for adding a Settings page. Read it once you can build the plugin and load it in game ([Getting started](getting-started.md)). [App framework](app-framework.md) explains the concepts behind each step.

Two terms come up on every page:

- **Dalamud** is the plugin framework that hosts community plugins inside Final Fantasy XIV. Aetherphone is one of those plugins.
- **Dear ImGui** is an immediate mode UI library. Nothing is kept between frames: your draw code runs every frame, paints everything, and reads input as it goes. A button is a rectangle you paint plus a click test you run in the same call.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Apps/IPhoneApp.cs | The interface every app implements |
| src/Aetherphone/Core/Apps/AppRegistry.cs | Constructs every app, in `BuildDefault` |
| src/Aetherphone/Core/Apps/AppStoreCatalog.cs | The App Store entry every app needs |
| src/Aetherphone/Core/Apps/AppAccents.cs | Maps an app id to its home tile color |
| src/Aetherphone/Core/Localization/L.cs | Every user-visible string, with its English text |
| src/Aetherphone/Localization/ | The nine translation catalogs |
| src/Aetherphone/Apps/Calculator/CalculatorApp.cs | A compact real app to copy from |
| src/Aetherphone/Apps/Dailies/DailiesApp.cs | A real app with the same screen shape as Counter |
| tools/icon-generator/ | Paints the home screen icon pair |

Everything in this tutorial is client side. Counter never talks to the Aethernet server.

## Step 1: read a real app

Open src/Aetherphone/Apps/Calculator/CalculatorApp.cs. It is split into partial files by area (display, keypad, keyboard, history sheet) and shows the whole anatomy of an app:

- **Identity.** `Id => "calculator"` is a stable lowercase key. The accent, the icon file names, the home layout and navigation all look the app up by it, so it never changes once shipped. `DisplayName` comes from the localization catalog.
- **A skin.** One `AppSkin` field holds the app's palette (inks, backdrop gradient, card fills) and paints the common controls.
- **Draw.** It runs every frame while the app is on screen and lays out the screen with rectangle math.
- **Lifecycle.** `OnOpened` resets per-visit state, `OnClosed` saves pending history, and `Dispose` is empty because nothing needs releasing.

Then skim src/Aetherphone/Apps/Dailies/DailiesApp.cs, whose `Draw` has the shape Counter uses: an `AppSkin` backdrop, the collapsing large title, and a scrolling body. src/Aetherphone/Apps/Notes/NotesApp.cs shows the next tier: several screens, saved data, and the phone's light and dark theme.

## Step 2: know the contract

src/Aetherphone/Core/Apps/IPhoneApp.cs is the contract. The members without a default are the ones you must write, plus `Dispose` from `IDisposable`:

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

`Glyph` is one or two characters drawn on the tile when the app has no icon art. `BadgeCount` is the unread number on the tile; return 0 if the app has none. Every other member (accent, badges, sharing, transparency, availability) has a default you can leave alone for now. [App framework](app-framework.md) covers them and the optional interfaces, such as resuming where the user left off.

`Draw` receives a `PhoneContext` with three things: `Content`, the rectangle you may draw in (already clear of the status bar and home indicator); `Theme`, the phone colors for this app; and `Navigation`, which opens other apps or goes back.

## Step 3: create the class

Each app gets one folder under src/Aetherphone/Apps. Create the file src/Aetherphone/Apps/Counter/CounterApp.cs (new file) with the identity and lifecycle first:

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
    private readonly AppSkin ui = new(AppPalettes.Calculator);
    private int count;
    private CachedText countLabel;

    public string Id => "counter";
    public string DisplayName => Loc.T(L.Apps.Counter);
    public string Glyph => "C";
    public int BadgeCount => 0;

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Dispose()
    {
    }
```

Then add the drawing, in the same class:

```csharp
    private string CountLabel() =>
        countLabel.IsCurrent(count) ? countLabel.Value : countLabel.Store(count, count.ToString(Loc.Culture));

    public void Draw(in PhoneContext context)
    {
        var scale = UiScale.Current;
        ui.Theme = context.Theme;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, context.Theme, scale));
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            DrawBody(scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "counter.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }
```

The body paints the number and two buttons at the scroll surface's cursor, then reserves the space it used so the surface knows its height:

```csharp
    private void DrawBody(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var labelHeight = Typography.LineHeight(TextStyles.LargeTitle);
        Typography.DrawCentered(drawList, new Vector2(origin.X + width * 0.5f, origin.Y + labelHeight * 0.5f),
            CountLabel(), ui.TitleInk, TextStyles.LargeTitle);
        var top = origin.Y + labelHeight + Metrics.Space.Lg * scale;
        var bottom = top + Button.Height(ButtonSize.Large) * scale;
        var half = (width - Metrics.Space.Md * scale) * 0.5f;
        var minus = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + half, bottom));
        var plus = new Rect(new Vector2(origin.X + width - half, top), new Vector2(origin.X + width, bottom));
        if (Button.Draw(minus, "-", ui.Ink, ButtonStyle.Gray))
        {
            count--;
        }

        if (Button.Draw(plus, "+", ui.Ink))
        {
            count++;
        }

        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }
}
```

The file does not compile until step 6 adds `L.Apps.Counter`, so do the localization in the same sitting. What each piece does:

- `SceneChrome.ScreenFrom` grows the content rectangle back to the full screen so `ui.Backdrop` paints the gradient edge to edge.
- `AppHeader.BeginLargeTitle` and `EndLargeTitle` draw the collapsing large title that almost every screen uses. The body goes in between, inside `AppSurface.Begin`, which gives it touch-style scrolling and lets the title collapse as it scrolls. The `false` skips the top row that a pushed screen keeps for its back control; a root screen has none.
- `ScrollLayout.StableContentWidth()` is the width to lay rows out in inside a scroll surface. It stays the same whether or not a scrollbar shows, so the layout does not jitter.
- All text goes through `Typography` with a style from `TextStyles`. Pass the draw list, as here: the overloads without one move the ImGui cursor and shift everything after them.
- `CachedText` keeps the formatted number until `count` changes (or the language does), so `Draw` does not build a new string every frame.
- `Button.Draw` paints the button, handles hover and press, and returns true once on the click.
- Every size is a `Metrics` token or a component height multiplied by `UiScale.Current`, so the layout follows Dalamud's UI scale and the user's phone size.

The sample borrows the Calculator palette to stay short. A real app adds its own line to src/Aetherphone/Windows/Components/Skin/AppPalettes.cs, usually `public static readonly AppPalette Counter = For("counter");` for a gradient tinted by the accent you pick in step 5. [Design accents](design-accents.md) lists the other palette shapes.

## Step 4: register it

Apps are constructed in one place: `AppRegistry.BuildDefault` in src/Aetherphone/Core/Apps/AppRegistry.cs. Add `using Aetherphone.Apps.Counter;` at the top and one line next to the other small apps:

```csharp
apps.Add(new CalculatorApp(services.Configuration, services.Confirm));
apps.Add(new CounterApp());
```

An app with dependencies takes them as constructor arguments from `services` (compare `new NotesApp(services.Configuration, services.Confirm)`).

Every app also needs an App Store entry in src/Aetherphone/Core/Apps/AppStoreCatalog.cs, with a name, a subtitle and a description (you write the two `StoreCopy` strings in step 6):

```csharp
["counter"] = new(L.Apps.Counter, L.StoreCopy.CounterSub, L.StoreCopy.CounterBody, StoreCategory.Utilities),
```

`AppStoreCatalogTests` fails until every app has exactly one entry, and each category must hold between three and eight apps, so pick a category with room.

## Step 5: pick an accent and an icon

Add your id to the dictionary in src/Aetherphone/Core/Apps/AppAccents.cs with a token from the accent ring:

```csharp
["counter"] = AccentRing.Azure,
```

Use a ring token, never a hand-picked color: reviewers check this, because the tests alone do not catch every off-ring color. [Design accents](design-accents.md) explains the ring, what the tests check, and how to pick a token that does not clash with its neighbors. Without an entry the tile uses the neutral slate fallback.

The home tile looks for a painted icon pair, src/Aetherphone/Icons/counter.png plus counter.fg.png. You can skip the icon while you work: without the pair, the tile paints your accent and draws `Glyph` on it. To generate a pair, add an entry to the `map` in tools/icon-generator/generate-painted-icons.mjs, using the same hue as your accent:

```js
counter: icon("plus-minus", "colour", "Azure"),
```

Then run the generator for your id. It needs Node.js, and `npm install` fetches its image library the first time:

```sh
cd tools/icon-generator
npm install
node generate-painted-icons.mjs counter
```

Symbol names come from https://phosphoricons.com. For a hand-painted icon, give the artist the [icon spec](https://aetherphone.net/icon-spec/) and the [icon checker](https://aetherphone.net/icon-checker/). Always ship both files together; [Assets and media](assets-and-media.md) covers the pipeline.

## Step 6: localize the strings

Every string the user sees is a `LocString` in src/Aetherphone/Core/Localization/L.cs, read with `Loc.T` at draw time. Add the name to the `Apps` group (after `Calculator`) and the two store strings to the `StoreCopy` group (after `CalculatorBody`):

```csharp
public static readonly LocString Counter = new("app.counter", "Counter");
```

```csharp
public static readonly LocString CounterSub = new("storeCopy.counterSub", "Tap to count");
public static readonly LocString CounterBody = new("storeCopy.counterBody",
    "A big number and two buttons, for counting anything you like.");
```

Then add the three keys to all nine catalogs in src/Aetherphone/Localization, at the same positions as in L.cs: `app.counter` right after `app.calculator`, and the two store keys right after `storeCopy.calculatorBody`. en.json gets the English text and the other eight get a checked translation:

```json
"app.counter": "Counter",
```

`LocalizationParityTests` fails `dotnet test` when a catalog misses a key from L.cs or keeps a key L.cs does not declare. L.cs and the nine files change in the same commit. [Localization](localization.md) has the workflow and the copy rules.

## Step 7: see it in game

Build and load the Debug plugin as [Getting started](getting-started.md) describes. Where Counter shows up depends on the profile:

- **A profile that has never opened the phone** installs every app. Counter lands on the last home page, sorted by name among the apps the default layout does not place.
- **A profile with a saved home layout** keeps it, so Counter is not installed. Open the App Store app: the Today tab lists it under "New to Your Phone". Press GET and the tile is added to your last page.

Once installed, `/phonedev open counter` opens it from chat; the command refuses an app that is not installed. Settings, Appearance, Reset Home Screen Layout also reseeds the layout with every app, at the cost of your own arrangement.

## Step 8: level up

Each addition is small and links to the doc that owns it.

- **More screens.** Give the app an enum of screens and a `ViewRouter`. NotesApp is the template, and [App framework](app-framework.md) has a two-screen sample.
- **Save state.** Take `Configuration` in the constructor (pass `services.Configuration`), add a property for your data, and call `configuration.Save()` after a change. See [State and persistence](state-and-persistence.md).
- **Notifications.** Take `services.Notifications`, post through it, and register a channel for your id so users can mute it. See [Notifications](notifications.md).
- **A badge.** Return a live number from `BadgeCount` and override `HasBadge => true` so Settings offers a switch for it. The getter runs every frame on the home screen, so return a cached field.
- **A home widget or a Control Center tile.** See [Widgets and Control Center](widgets-and-control-center.md); its examples extend Counter.
- **A first-open tour.** Add a tour for your id in a TourRegistry partial file in src/Aetherphone/Core/Onboarding, with its copy in `L.Onboarding`, and report the rectangle each step points at while you draw, for example `UiAnchors.Report("counter.buttons", new Rect(minus.Min, plus.Max))`. Then add a row for `"counter"` to the `Expected` table in src/Aetherphone.Tests/TourRegistryTests.cs and raise the count in `NoTourIsRegisteredBeyondTheExpectedSet` by one, or `dotnet test` fails.

## Add a Settings page

A Settings page is a smaller job than an app and a common first contribution ([Contributing](../CONTRIBUTING.md)). A page implements `ISettingsPage` (src/Aetherphone/Apps/Settings/ISettingsPage.cs):

- `Title`, `Summary`, `Icon` and `Tint` fill the page's row on the Settings root screen. `Summary` is the grey side text; `string.Empty` is fine.
- `Draw(in PhoneContext context, Rect body)` draws the page into `body`, below the title. Settings wraps the page in the collapsing large title and a back control for you.
- `Entries` lists the strings Settings search should find on the page. Declare it on every new page.
- The other members have defaults: a row badge, hiding the page, drawing your own title bar, and an anchor for the Settings tour.

To add one:

1. Create the page class in src/Aetherphone/Apps/Settings/Pages. GeneralPage.cs is the closest real template.
2. Construct it in the `SettingsApp` constructor (src/Aetherphone/Apps/Settings/SettingsApp.cs) and add it to one of the inner arrays of `groups`. Each inner array is one card on the root screen, and the same list feeds Settings search and Spotlight.
3. Add `Title` and every label to the `Settings` group of L.cs and to the nine catalogs, as in step 6.

A minimal page with one saved switch. `L.Settings.Example`, `ExampleToggle`, `ExampleHint` and the `Configuration.ExampleEnabled` property are yours to add first:

```csharp
internal sealed class ExamplePage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable = { new(L.Settings.ExampleToggle) };
    private readonly Configuration configuration;

    public ExamplePage(Configuration configuration) => this.configuration = configuration;

    public string Title => Loc.T(L.Settings.Example);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Star;
    public Vector4 Tint => AccentRing.Azure;
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * UiScale.Current));
            var card = GroupCard.Begin(theme, 1);
            var enabled = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ExampleToggle),
                configuration.ExampleEnabled, theme, hint: Loc.T(L.Settings.ExampleHint));
            card.End();
            if (enabled == configuration.ExampleEnabled)
            {
                return;
            }

            configuration.ExampleEnabled = enabled;
            configuration.Save();
        }
    }
}
```

The file needs the usings for `Aetherphone.Core`, `Aetherphone.Core.Apps`, `Aetherphone.Core.Localization`, `Aetherphone.Core.Theme`, `Aetherphone.Windows.Components`, `Dalamud.Bindings.ImGui` and `Dalamud.Interface`. `SettingsRow.Bool` returns the new value, not "was clicked", so compare it with the stored value and save only on a change. The `hint` argument puts a question mark icon next to the label that explains the switch; [UI toolkit](ui-toolkit.md) has the rules for rows, cards and hints. If the page draws a `FontAwesomeIcon` nothing else uses yet, add its codepoint to src/Aetherphone/Core/IconPlan.cs or `IconFontCoverageTests` fails.

## Pre-PR checklist

- `dotnet build Aetherphone.sln -c Release` finishes with zero warnings; CI treats every warning as an error.
- `dotnet test Aetherphone.sln` passes. Add tests under src/Aetherphone.Tests for logic worth testing ([Testing and release](testing-and-release.md)).
- You opened the app in game and went through every screen.
- The name and both store strings are in L.cs and in all nine catalogs.
- The app has an accent in AppAccents.cs, a store entry in AppStoreCatalog.cs, and either both icon files or neither.
- The code follows [Conventions](conventions.md): explicit accessibility keywords, braces on every branch, early returns, no LINQ or per-frame allocations in `Draw`, and no em dashes in any copy.

## Gotchas

- **`BadgeCount` has no default.** Leaving it out is a compile error that surprises people who read the list of defaulted members.
- **Never translate in a constructor.** A string from `Loc.T` stored at construction keeps the language it was created in after the user switches. Keep `DisplayName` and friends as arrow properties, and pass `LocString` values, not strings, between classes.
- **A thrown exception in `Draw` does not crash anything.** The shell catches it, logs `[shell] app-draw <id> threw`, and draws a short failure message instead of your app. If your app shows one sad sentence, read the Dalamud log.
- **`OnOpened` can fire while the app is already open.** Opening the current app again (a notification tap, for example) calls it again, so make it safe to repeat. [App framework](app-framework.md) has the full lifecycle.
- **Name the icon files exactly after `Id`.** A lone counter.png without counter.fg.png is read as a white stencil and tinted, so a painted icon missing its partner renders wrong.

## Related docs

- [Getting started](getting-started.md): build, load the dev plugin, chat commands
- [App framework](app-framework.md): the contract, lifecycle, navigation, theming, sharing
- [Widgets and Control Center](widgets-and-control-center.md): home widgets and Control Center tiles
- [UI toolkit](ui-toolkit.md): which component to use for what, and the design rules
- [Design accents](design-accents.md): the accent ring and palettes
- [Localization](localization.md): L.cs, the nine catalogs, copy rules
- [Testing and release](testing-and-release.md): tests and CI
