# Widgets and Control Center

This doc covers the two kinds of small live tile the phone shows outside an app: home screen widgets and Control Center tiles. It explains how each is drawn, the rules they follow, and how to add one, with examples that extend the Counter app from [Creating an app](creating-an-app.md). Both contracts are smaller cousins of the app contract in [App framework](app-framework.md).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Home/IHomeWidget.cs | The widget contract and the `WidgetContext` it draws with |
| src/Aetherphone/Windows/Widgets/WidgetCatalog.cs | Constructs every widget, at boot |
| src/Aetherphone/Windows/Widgets/ | The widget kit: container, inks, text, controls, per-tile state |
| src/Aetherphone/Core/Home/WidgetRoute.cs | Where a tap on a widget goes |
| src/Aetherphone/Apps/Calendar/Widgets/MonthWidget.cs | The smallest real widget |
| src/Aetherphone/Core/ControlCenter/IControlModule.cs | The Control Center tile contract and its context |
| src/Aetherphone/Core/ControlCenter/ControlRegistry.cs | Constructs every Control Center module |
| src/Aetherphone/Core/ControlCenter/ControlDefaults.cs | The grid a new install starts with |
| src/Aetherphone/Core/ControlCenter/Modules/ | Reusable tile shapes (toggle, slider, cluster, media) |
| src/Aetherphone/Windows/Components/Chrome/ControlTile.cs | Paint helpers for Control Center tiles |
| src/Aetherphone.Tests/ControlLayoutServiceInstallTests.cs | Pins the default grid and install behavior |

## Home widgets

A widget is a class that implements `IHomeWidget`. One instance draws every copy the user placed, plus its previews in the widget gallery. On the four-column home grid a widget comes in up to three sizes: small (2 by 2 cells), medium (4 by 2) and large (4 by 4). Users place one by long-pressing the home screen, pressing the add button, and picking a widget and size in the gallery.

A widget has no router and no `PhoneContext`. Its `Draw` receives a `WidgetContext`: the draw list and rectangle, the scale and theme, the size being drawn, an opacity, the user's icon appearance, whether it may take input, whether it is a preview, a key unique to the placed tile, and the tile's option values.

`AppId` ties the widget to its app, and the link does real work: the gallery only offers widgets of installed, available apps, a saved tile disappears when its app is uninstalled, and a tap opens that app by default. `Id` names the widget itself (`"calendar.month"`), so one app can ship several.

### Rules every widget follows

- **Lay out from the rectangle you are given.** The same `Draw` paints the placed tile and the previews at whatever size the caller picked, so never assume grid pixel sizes. Branch on `context.Size` for size-specific layouts.
- **Paint your own background.** The home screen draws nothing behind a resting widget. Start `Draw` with `WidgetChrome.Container(context)`, which paints the standard card for the current appearance.
- **Carry the opacity into every color.** The home screen fades widgets during page moves and edit mode. Inks from `WidgetInk.From(context)` already carry it; run your own colors through `ink.Fade`.
- **Honor the icon appearance.** Hard-coded colors look wrong in the Tinted and Clear appearances. Take inks from `WidgetInk` and pass your accent through `ink.Accent(color)`.
- **Key per-tile state on `InstanceKey`.** One instance draws every copy, so a field holds state for all of them. `WidgetStates<T>` keeps one state object per tile.
- **Draw sample content in previews.** When `context.Preview` is true and there is no live data (signed out, nothing loaded), draw believable sample content so the gallery still sells the widget.
- **Cache formatted strings.** A `CachedText` field keyed on the value refreshes itself when the language or clock format changes; the kit's `WidgetText.Number` and `Countdown` helpers use it.

### The widget kit

The shipped widgets compose from src/Aetherphone/Windows/Widgets:

| Piece | Use it for |
| --- | --- |
| `WidgetChrome` | The container, the app glyph and caption header row, empty and signed-out messages, rings and bars |
| `WidgetInk` | Mode-aware primary, secondary and tertiary inks, fills and separators, all faded by opacity |
| `WidgetMetrics` | The inset content rectangle for the current size, the space below a header, gutters |
| `WidgetText` and `WidgetType` | Fitted lines, tracked uppercase eyebrows, tabular numbers, and the widget type styles |
| `WidgetControls` | Buttons, toggles and links inside a widget |
| `WidgetStates` | Per-tile state keyed on `InstanceKey` |

### Taps, controls and options

`Target` decides where a tap on the tile goes; the default opens the app at its root. Return `WidgetRoute.Tab(appId, tab)` for an app that implements `ITabRouteTarget`, or `WidgetRoute.To(appId, kind, argument)` for a deep link such as a conversation or a note. The tap fires the matching launcher and opens the app out of the tile.

`WidgetControls` gives a widget its own tappable parts. Each takes a control id unique within the widget and registers a hit area only while `context.Interactive` is true, so previews stay inert. A press on a control runs it on release instead of opening the app: `Button` returns true once, `Toggle` returns the new value, and `Link` opens its own route.

`Options` lists per-tile settings shown under Edit Widget, each a `WidgetOption` with a key, a label and choices; read the saved value with `WidgetConfig.Get(context.Config, key, fallback)`. `Relevance` scores the widget from 0 to 1: a Smart Stack (several same-size widgets in one tile) with Smart Rotate on turns to a member that clearly leads, and the gallery's featured rail sorts by it.

### Adding a widget

1. Create the class in src/Aetherphone/Apps/<App>/Widgets, with the namespace `Aetherphone.Apps.<App>.Widgets`. Take data from Core services through the constructor.
2. Construct it in `WidgetCatalog.Build` (src/Aetherphone/Windows/Widgets/WidgetCatalog.cs), passing the services it needs, and add a using for its namespace at the top of that file. There is no other registration.
3. Put `DisplayName` and `Description` (the gallery caption) in L.cs and the nine catalogs, as for any string ([Localization](localization.md)).
4. In game, add the widget from the gallery at each size it supports.

Only WidgetCatalog.cs and AppRegistry.cs may import app namespaces from Core and Windows (a CI guard), so code shared by several widgets belongs in the kit.

### Example: a Counter widget

A small widget that shows the count and a plus button. It assumes Counter keeps its count in a `Configuration.CounterValue` property, as the "Save state" level-up in [Creating an app](creating-an-app.md) describes, and that `L.Counter.WidgetDescription` exists:

```csharp
internal sealed class CounterWidget : IHomeWidget
{
    private const int PlusControl = 1;
    private const int PreviewCount = 12;

    private readonly Configuration configuration;
    private CachedText countText;

    public CounterWidget(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public string Id => "counter.tally";
    public string DisplayName => Loc.T(L.Apps.Counter);
    public string Description => Loc.T(L.Counter.WidgetDescription);
    public string AppId => "counter";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small;

    public void Dispose()
    {
    }
```

`Draw` paints the container and header, the number in the widget hero style, and a round accent button in the corner:

```csharp
    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var accent = AppAccents.For(AppId);
        var top = WidgetChrome.Header(context, ink, AppId, DisplayName, accent);
        var area = WidgetMetrics.Below(context, top);
        var value = context.Preview ? PreviewCount : configuration.CounterValue;
        WidgetText.Draw(context.DrawList, area.Min, WidgetText.Number(ref countText, value), ink.Primary,
            WidgetType.Hero(context.Size), area.Width);
        var radius = WidgetMetrics.ControlLarge * 0.5f * context.Scale;
        var center = area.Max - new Vector2(radius, radius);
        if (WidgetControls.Button(context, ink, PlusControl, center, WidgetMetrics.ControlLarge, FontAwesomeIcon.Plus,
                accent))
        {
            configuration.CounterValue++;
            configuration.Save();
        }
    }
}
```

The file needs usings for Aetherphone.Core.Apps, Core.Home, Core.Localization, Windows.Widgets and Dalamud.Interface. Register it with `widgets.Add(new CounterWidget(phone.Configuration));` in `WidgetCatalog`. A tap on the number opens Counter; a tap on the button only counts.

## Control Center tiles

The Control Center opens when the user taps or pulls down the band at the top of the phone screen, and closes from the band at the bottom. It does not open while Camera is the current app or while an app holds the phone in landscape. Its grid has four columns, and each tile is an `IControlModule` with an id, a gallery label and icon, the spans it supports, a default span, and `Draw`.

The spans are Small (1 by 1), Wide (2 by 1), Tall (1 by 2), Large (2 by 2) and Bar (4 by 1). `Draw` receives a `ControlModuleContext` with the draw list, the tile rectangle, the theme, the span, the scale, an opacity, whether it may take input, and an expansion amount: a long press opens a tile's expanded view, and `context.Expanded` is true once it is mostly open.

The customize button in the Control Center header enters edit mode: tiles can be dragged, resized through their spans, and removed, and the add button opens a gallery of the modules not on the grid. The layout is saved in the plugin settings ([State and persistence](state-and-persistence.md)).

### Reusable modules

You rarely implement `IControlModule` yourself. src/Aetherphone/Core/ControlCenter/Modules holds the shapes the shipped tiles use:

| Module | Shape |
| --- | --- |
| `ToggleModule` | An icon and label that reads a `Func<bool>` for its state and runs an `Action` on press. Also used for one-tap launchers whose state is always false |
| `SliderModule` | A vertical slider, such as brightness and volume |
| `ClusterModule` | One large tile of round toggles built from registered toggle modules |
| `MediaModule` | Now playing with transport buttons |
| `AccentModule` | A row of accent color swatches |

Paint helpers for a custom module live in ControlTile.cs.

### Adding a tile

1. Construct the module in the `ControlRegistry` constructor. For a switch or a launcher this is one `Add(new ToggleModule(...))` line; the label is a `LocString`.
2. Registering makes the tile available in the gallery. Existing users never get a new tile on their grid automatically; they add it from the gallery.
3. To put it on the grid of new installs, add it to `ControlDefaults.Layout`. `ControlLayoutServiceInstallTests` pins where each default tile lands, so update that test in the same change.

The registry lives in Core, which may not import app namespaces, so a tile reaches an app through the navigator by id and through Core services, never through an app class.

### Example: a tile that opens Counter

```csharp
Add(new ToggleModule("counter", FontAwesomeIcon.Plus, L.Apps.Counter, () => false, () =>
{
    navigation.Open("counter");
    dismiss();
}));
```

The state reads false, so the tile never lights up; the press opens the app and closes the Control Center. `navigation.Open` does nothing when Counter is not installed. A real switch passes a configuration read and a toggle-and-save action instead, like the existing `lock` tile.

## Gotchas

- **Widgets and tiles draw while the app is closed.** They are built separately from the app, so share state through Core services or `Configuration`, never through the app object.
- **Control ids must be unique within a widget.** The hit key combines the tile and the control id, so two controls with one id fire together.
- **Controls do nothing in the gallery.** Previews are not interactive, so test a control on a placed tile.
- **`ToggleModule` state is read every frame.** Pass a cheap field read, never a lookup.

## Related docs

- [Creating an app](creating-an-app.md): the Counter app these examples extend
- [App framework](app-framework.md): the app contract, launchers and tab routes widgets use
- [UI toolkit](ui-toolkit.md): typography, spacing and the shared components
- [State and persistence](state-and-persistence.md): where the home and Control Center layouts are saved
- [Localization](localization.md): adding the strings
