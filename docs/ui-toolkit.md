# UI toolkit

This doc is the guide to the shared component library in src/Aetherphone/Windows/Components and the design rules every phone screen follows. Read it before you draw UI in an app, and come back whenever you are about to hand-roll a button, a text block or a scroll region: a component for it almost certainly exists. The [task table](#need-x-use-y) is the fastest way in.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Windows/Components/Primitives/Typography.cs | All text drawing: measure, draw, wrap, fit |
| src/Aetherphone/Windows/Components/Primitives/TextStyles.cs | The named text styles |
| src/Aetherphone/Windows/Components/Primitives/Metrics.cs | Spacing, radius, size and stroke tokens |
| src/Aetherphone/UiScale.cs | The one scale every size is multiplied by |
| src/Aetherphone/Windows/Components/Primitives/UiInteract.cs | Hit testing, tap claims, input blocking |
| src/Aetherphone/Windows/Components/Primitives/Material.cs | Glass for the navigation layer |
| src/Aetherphone/Windows/Components/Primitives/Surfaces.cs | The content fill ladder |
| src/Aetherphone/Windows/Components/Layout/AppSurface.cs | The standard scrolling app body |
| src/Aetherphone/Windows/Components/Layout/AppHeader.cs | The collapsing large title |
| src/Aetherphone/Windows/Components/Fields/Button.cs | The one labelled button |

## How the phone draws

The phone is one Dalamud window drawn with Dear ImGui, an immediate mode library: every frame, your code declares everything on screen again. Components rarely use stock ImGui widgets; they paint onto a draw list (the command buffer from `ImGui.GetWindowDrawList()`) in screen coordinates and run their own hit tests. Rectangles travel as the `Rect` record.

The folders group files by kind (Primitives, Layout, Fields, Sheets and so on), but every file declares the one flat namespace `Aetherphone.Windows.Components`, so a screen needs a single using; a CI guard checks it. A component only one app uses lives in that app.

## The rules every screen follows

- **Text goes through `Typography` with a `TextStyles` style**, and you pass the draw list ([details](#typography-and-the-cursor-trap)).
- **Every size is a token or a named constant times `UiScale.Current`** ([details](#spacing-tokens-and-uiscale)).
- **Text wraps; it never overflows.** Clipped or overlapping text is always a bug.
- **Glass belongs to the navigation layer; content is a fill** ([details](#design-rules)).
- **Hit tests go through `UiInteract`.** A tap fires on release, and the last claim of the press frame wins ([details](#input-and-ime)).
- **`Toggle`, `SegmentStrip` and `SettingsRow.Bool` return the new value**, not "was clicked". Assign it back every frame.
- **A popup ignores the frame it opened on** ([details](#popups-and-dismissal)).
- **Motion runs through `Spring`**, critically damped with no overshoot. Draw the raw spring value with no easing curve on top, and take every smooth time and press scale from `Motion` rather than declaring your own. Bouncy easing is for the mini-games and casino cabinets only.
- **Clock text goes through `TimeText.Clock`**, which honors the 12 or 24 hour setting.
- **Nothing allocates per frame.** Cache formatted strings, ids and delegates in fields.

## Need X, use Y

Each row names the component, when to pick something else, and one real file to copy from. Paths are under src/Aetherphone.

### Text

| Need | Use | Not when | Copy from |
| --- | --- | --- | --- |
| A line of text | `Typography.Draw` with a draw list and a style | It can be a sentence: wrap it | Apps/AppStore/AppStoreApp.Today.cs |
| A paragraph | `Typography.DrawWrappedLeft` or `DrawWrappedCentered` | A label in a fixed slot | Apps/Announcements/AnnouncementsStatePanel.cs |
| A label that must stay on one line | `Typography.FitText`, or `Marquee` to scroll it | Body copy | Windows/Components/Sheets/ConfirmDialog.cs |
| A time of day | `TimeText.Clock` | Never a hand-made format | Apps/Timers/TimerText.cs |
| Text a user typed, with links, mentions or emoji | `RichText`, or `LinkText` for links behind the open-link confirm | Your own UI copy | Apps/Music/Radio/Live/RadioChatRow.cs |

### Screen structure

| Need | Use | Not when | Copy from |
| --- | --- | --- | --- |
| A scrolling body | `AppSurface.Begin` | Feeds use `BeginEdgeToEdge` | Apps/Settings/Pages/GeneralPage.cs |
| A screen title | `AppHeader.BeginLargeTitle` and `EndLargeTitle` | `AppHeader.Draw` is only for compact sub-screens outside a router | Apps/Dailies/DailiesApp.cs |
| Several screens | `ViewRouter` ([App framework](app-framework.md)) | Peer sections: tabs | Apps/Notes/NotesApp.cs |
| Bottom tabs | `TabBar`, bodies inside `TabBar.ReserveContent` | Views of the same data | Apps/Clock/ClockApp.cs |
| A few views of the same data | `SegmentStrip` | Many options | Apps/Settings/Pages/DisplayPage.cs |
| A custom bottom sheet | `Sheet` | A list of actions | Apps/Calculator/CalculatorApp.History.cs |
| Nothing to show yet | `EmptyState`, or `Skeleton` while loading | Errors needing an action | Apps/Music/MusicApp.Artists.cs |
| Move, scale or fade a whole screen | A `ScreenLayer` stage with a transform | Never repaint it at a shifted rect | Core/Animation/SceneCompositor.cs |

### Lists and rows

| Need | Use | Not when | Copy from |
| --- | --- | --- | --- |
| Settings or detail rows | `GroupCard` with `SettingsRow` | Feed content | Apps/Settings/Pages/GeneralPage.cs |
| Posts, conversations, results | `FeedCell` in an edge-to-edge surface | Settings rows | Apps/Music/MusicApp.Artists.cs |
| A long feed | `FeedVirtualizer`, `InfiniteScroll` to page | Short lists | Apps/Aethergram/AethergramApp.cs |
| A section title | `CardSectionHeader` over cards, `ListSection.Header` over cells | One row | Apps/Music/Components/SectionHeader.cs |

### Buttons and input

| Need | Use | Not when | Copy from |
| --- | --- | --- | --- |
| A labelled button | `Button.Draw`, or the `AppSkin` pill helpers | Never `ImGui.Button` or a new painter | Apps/Announcements/AnnouncementsStatePanel.cs |
| A round icon button | `RoundButton.Icon` | Over media or in bars: `GlassCircle` | Apps/Fishing/FishingApp.cs |
| A custom tappable shape | `UiInteract.HoverClick`, with `PressFx.Scale` for the press | A standard control fits | Apps/Music/MusicApp.MiniPlayer.cs |
| A hover label for an icon | `HoverTooltip.Show` | `RoundButton.Icon` shows one | Apps/Velvet/Kit/VIcon.cs |
| An on and off setting | `SettingsRow.Bool` in a card, `Toggle` elsewhere | An action | Apps/Clock/ClockApp.Alarms.cs |
| Filter chips | `ChipRail`, one pannable row | Never a wrapping chip wall | Apps/Fishing/FishingApp.Fish.cs |
| A text field or search box | `SearchBar.Surface` under `GlassField.Text`, or `SearchBar.Draw` | Never a bare field | Apps/Settings/SettingsForm.cs |
| Multiline text | `SoftWrapField.Multiline` | Raw multiline input does not wrap | Apps/Aethergram/AethergramApp.EditProfile.cs |

### Feedback and overlays

| Need | Use | Not when | Copy from |
| --- | --- | --- | --- |
| Confirm a destructive step | `ConfirmService.Ask` with `Sheet = true` | Money, consent, the irreversible | Apps/Casino/Tables/TableLeaveConfirm.cs |
| Confirm money, consent or the irreversible | `ConfirmService.Ask` with `Sheet` unset, which shows a centered alert | Never draw the dialog yourself | Apps/Coin/CoinApp.Shop.cs |
| Tell the user something with one button | `ConfirmService.Alert` | A choice: `Ask` | Apps/Settings/Pages/AvatarPhotoPage.cs |
| Overflow actions | `ActionSheet` | A picker | Apps/Notes/NotesApp.Trash.cs |
| A picker or context menu | `DropdownMenu` | An editor: `PopoverSurface` | Apps/Dailies/DailiesApp.cs |
| Report a result | `ShellToast.Show` | Anything needing action | Apps/Shortcuts/ShortcutsApp.Preview.cs |
| Open a link another user posted | `UrlActions.AskThenOpen` | First-party links | Apps/Venues/VenuesApp.Djs.cs |

### Social screens

| Need | Use | Not when | Copy from |
| --- | --- | --- | --- |
| A social header or top bar | `SocialChrome`, inks from `SocialInk` | Other apps: the large title | Apps/Aethergram/AethergramApp.EditProfile.cs |
| Two feeds or an icon tab row | `UnderlineTabs` | App navigation: `TabBar` | Apps/Chirper/ChirperApp.cs |
| People with a follow pill | `SocialUserRow` | Settings rows | Apps/Chirper/ChirperApp.Screens.cs |
| A person's picture | `AvatarView` | Never a hand-drawn circle | Apps/Velvet/Kit/VAvatar.cs |

## Design rules

These hold across every app, and reviewers check them.

### Materials and controls

- **Glass is for the navigation layer only**: nav bar buttons, tab bars, floating toolbars, sheets, menus, popovers, toasts, the dock and the Control Center. Cards, rows, buttons, fields, chips and heroes scroll with the content and are fills. Glass never sits on glass.
- **Glass in an app refracts the app's own backdrop**, which `AppSkin.Backdrop` records, so a tab bar lifts the color behind it instead of darkening into a black block.
- **Content fills come from one ladder.** `Surfaces.Fill` derives four levels from the title ink, right in dark and light. Never hand-pick a white-alpha fill.
- **A top highlight follows the corner.** Use `Material.Sheen`; a straight line inset by the radius stops short of the squircle edge.
- **Every labelled button is a `Button`**: Prominent, Tinted, Gray or Plain, in three capsule sizes, with a destructive role. In a tinted app pass the app's ink (`ui.Ink`), or the button falls back to the system accent.
- **Fields are filled capsules.** `GlassField` draws no background, so a field in content sits on `SearchBar.Surface` (navigation chrome uses the glass `GlassField.Surface`). `SearchBar.Surface` paints a capsule at most 36 units tall, centered in the rectangle it gets, so a field laid out at `GlassField.HeightUnits` (44) keeps a 44 unit tap target.
- **Chips sit in one pannable `ChipRail`.** They never wrap into a second line.
- **Free input over preset chips.** Let the user enter any value, as `TimeOfDayField` steps through the whole day.

### Lists

- **Feed content gets edge-to-edge cells**: `FeedCell` in an edge-to-edge surface, full-width hairlines, content inset by `FeedCell.PadX`, a whole-cell tap. A cell whose interior is all controls passes `interactive: false`. Where the image is the post, the media goes full bleed with no rounding, while the header, actions and caption keep the inset.
- **Settings and detail lists get grouped inset cards**: `GroupCard` and `SettingsRow` painters with fixed row heights.
- **Floating cards are for card-shaped content only**, such as venue listings, health metrics or the photo grid. No new row lists of floating cards. A card inside an edge-to-edge surface is inset by `FeedCell.PadX` so it lines up with the cells around it.
- **A row shows a value only when it deviates**: the chosen language, an unread count, a feature that is off. It never restates its own label. Otherwise pass `string.Empty`.
- **One card per group of switches**, and a section header only where a page turns a corner, never for one row.
- **A footer has to earn its place.** The `hint` icon on a row or section header explains a control; `SettingsSection.Hint` is for destructive warnings, empty and loading states, and sign-in prompts.

### Sheets, dialogs, menus and toasts

- **A destructive or choice flow is a bottom sheet**: `ActionSheet` for overflow actions, `ConfirmService.Ask` with `Sheet = true` for one destructive confirm.
- **An irreversible, monetary or consent confirm stays a centered alert**, including the external-link and adult-content gates.
- **Pickers stay menus** (`DropdownMenu`), and **a popover is not a menu**: `PopoverSurface` hosts editors a label list cannot express, such as the Jobs color editor.
- **Transient feedback is a toast.** `ShellToast.Show` appears in whichever window the pointer is in. An app that needs its own toast style keeps a `ScreenToast`, as Chirper does.
- **A titled panel of content is a `SheetSurface`**, the bottom sheet with a title and a grabber, as the AetherStream track list uses. A list of actions stays an `ActionSheet`.
- **The share sheet sends to apps, not people.** Sending to people needs the app's own recipient list, as Aethergram's Send to screen does.

## Typography and the cursor trap

`TextStyles` is a ladder of named styles, `Hero` and `LargeTitle` down to `Caption2`, plus purpose styles. Never invent a scale: the font service snaps every scale to a fixed set of sizes, so an off-ladder number only misleads. `TextStyles.Footnote` is the practical floor for standalone text.

`Typography.Draw` and `DrawCentered` have overloads without a draw list. Those create a real ImGui item and move the layout cursor, which shifts whatever draws next. When one card lands on top of another, look for the last cursor-moving draw. Pass the draw list in all bespoke drawing.

`DrawCentered` wraps when the text is wider than the room around its anchor, and the wrap helpers break CJK text per character and cache their results, so calling them every frame is fine. A centered card is a measured stack: measure each line, add `Metrics.Space` gaps, and size the card from the total. Stack lines with `LineHeight` for a single line and the height a wrap helper returns for a paragraph:

```csharp
var drawList = ImGui.GetWindowDrawList();
var origin = ImGui.GetCursorScreenPos();
var width = ScrollLayout.StableContentWidth();
Typography.Draw(drawList, origin, title, ui.TitleInk, TextStyles.Headline);
var top = origin.Y + Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Xs * scale;
top += Typography.DrawWrappedLeft(new Vector2(origin.X, top), body, ui.BodyInk, TextStyles.Body, width);
ImGui.Dummy(new Vector2(width, top - origin.Y));
```

## Spacing tokens and UiScale

`Metrics` holds the shared rhythm (`Space`, `Radius`, `Size`, `Stroke`) in unscaled design units, authored against a 360 unit wide phone. A size that belongs to one component is a named constant at the top of its file; never write a bare number inline. Multiply everything by `UiScale.Current`, which combines Dalamud's UI scale with the user's phone size; CI fails any other read of Dalamud's global scale.

The Text Size setting scales the fonts, not `UiScale.Current`, so a box that holds text is sized from `Typography`, never from a height constant alone:

```csharp
private const float TileUnits = 64f;

var scale = UiScale.Current;
var padding = Metrics.Space.Md * scale;
var text = Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Footnote);
var height = MathF.Max(TileUnits * scale, padding * 2f + text);
```

A tile that fits at a small text size and clips at a large one has this bug.

## Input and IME

ImGui does not know painted components exist, so `UiInteract` does the hit testing. `Hover` is the normal gate: the pointer is over the rectangle, the phone window is hovered, input is not blocked this frame, and no overlay reserved the pointer. `HoverWindowOnly` skips the blocking checks, for the rows of the overlay that is doing the blocking. `ClickedOutside` is the only correct tap-outside test.

`Click` fires on release. On the press frame every hovered `Click` overwrites one pending claim, so the last caller in draw order wins, and a row that calls `Click` after its child buttons steals their taps. Gate the parent on not hovering the child, as src/Aetherphone/Apps/Settings/Pages/AccountPage.cs does:

```csharp
var removable = !active;
var overRemove = removable && UiInteract.Hover(removeCenter - removeExtent, removeCenter + removeExtent);
var hovered = UiInteract.Hover(row.Min, row.Max);
if (removable && UiInteract.Click(removeCenter - removeExtent, removeCenter + removeExtent, overRemove))
{
    return AccountRowAction.Remove;
}

if (!active && UiInteract.Click(row.Min, row.Max, hovered && !overRemove))
{
    return AccountRowAction.Switch;
}
```

Text input is real ImGui input. Dalamud's IME writes composed text (Chinese, Japanese, Korean) into ImGui's text state between frames, so an input callback sees it under `CallbackAlways`, never `CallbackEdit`, and the composition stays selected until committed. A plain `InputText` on a `ref string` is fine; a callback-driven editor must reconcile every frame and never rewrite the buffer while a selection is held. `SoftWrapField` already does both.

## Popups and dismissal

The click that opens a popup lies outside it, so a popup that closes on an outside tap must ignore its opening frame. `DropdownMenu` records that frame for you; a hand-made popup compares `ImGui.GetFrameCount()` with the frame it stored. A popup that blocks the UI beneath calls `Gate()` at the top of the frame, and controls inside a gated sheet pass `overlay: true` (buttons, round buttons, toggles and segment strips take it). Test the outside tap with `UiInteract.ClickedOutside`, never with a raw mouse check and a negated hover: that pair reads a click in another plugin's window as outside and closes the popup. The menu lifecycle from src/Aetherphone/Apps/Dailies/DailiesApp.cs, condensed and adapted to Counter:

```csharp
menu.Gate();
if (UiInteract.HoverClick(menuRect.Min, menuRect.Max))
{
    menu.Toggle("counter.menu", menuRect);
}

if (!menu.Open)
{
    return;
}

menuItems[0] = new DropdownMenu.Item(Loc.T(L.Counter.Reset), IconGlyph.Of(FontAwesomeIcon.Undo));
if (menu.Draw(screen, theme, menuItems) == 0)
{
    ResetCount();
}
```

For confirms, call `ConfirmService.Ask` or `Alert`; the shell shows the dialog over the window that raised it.

## Scrolling

`AppSurface.Begin` wraps the standard body in a child window with `DragScrollHost`, so a press and drag anywhere scrolls with a fling and cancels pending taps. Drag scrolling is on only while the phone's position is locked; unlocked, a drag moves the window and the native scrollbar returns. Content sized to the full width then makes that scrollbar flicker on and off, so size rows with `ScrollLayout.StableContentWidth()`, not `ImGui.GetContentRegionAvail().X`.

`FeedVirtualizer` caches each row's height and skips rows outside the view. Change the revision passed to `Skip` and `Record` whenever a row's height can change:

```csharp
feed.BeginFrame();
for (var index = 0; index < ids.Length; index++)
{
    if (feed.Skip(ids[index]))
    {
        continue;
    }

    DrawRow(ids[index]);
    feed.Record(ids[index]);
}
```

The social feeds pass the configuration to `BeginFrame` for two settings: arrow keys step the feed one post at a time, and auto-advance steps it on a timer while the phone sees no input, stopping after a long idle so an absent player does not page through the feed.

## Name effects

Badged account names animate through the `Typography.Draw` overload that takes a `TextEffect` from `NameEffects.For(badge, light)`. The painter (Typography.Effects.cs) is stateless and allocation free, so any number of names can carry one. A new effect also needs its key in the lists `NameEffectSyncTests` pins.

## Gotchas

- **`DrawWrappedCentered` returns two different things.** The `(drawList, text, style, ...)` overload returns the bottom edge it reached; the `(topCenter, text, ...)` overload returns the height. Adding the first to your own top doubles the offset.
- **Measure and draw with a matching pair.** `MeasureWrappedBlock` matches `DrawWrappedLeft`, the `(topCenter, ...)` and the center-anchored `(drawList, center, ...)` overloads; the `(drawList, text, style, ...)` overload spaces lines wider.
- **`Hero` and `WidgetDisplay` come out smaller than their scale.** They sit above the largest size the font service builds, so the regular calls snap them down to it. Measure and draw with `MeasureExact` and `DrawCenteredExact` when the true size matters.
- **`GroupCard` is a struct, not a scope.** `End()` moves the cursor past the card, and `NextRow` takes design units.
- **A fitted label must not go through `DrawCentered`.** Its wrap measures to the window edge, so a label near the edge wraps anyway; draw `FitText` output with `Typography.Draw`.
- **`HoverTooltip.Show` only queues a label.** A new top-level window must call `HoverTooltip.Flush()` like the phone and the pop-outs do.
- **Text drawn without `Typography` skips the glyph ledger**, so CJK and rare symbols can render as boxes.
- **A real ImGui item ignores the `UiInteract` gates.** Combine `ImGui.IsItemHovered()` with `UiInteract.Hover`, and restore the cursor around the item.

## Related docs

- [App framework](app-framework.md): the app contract, navigation and theming scope
- [Creating an app](creating-an-app.md): a tutorial that uses these components
- [Design accents](design-accents.md): the accent ring and the palettes `AppSkin` paints with
- [Messaging and chat](messaging-and-chat.md): the chat components built on this toolkit
- [Assets and media](assets-and-media.md): fonts, emoji and icons
- [Conventions](conventions.md): code style and performance rules
