# UI toolkit

This doc covers the reusable widget library in src/Aetherphone/Windows/Components: typography, spacing tokens, the custom input layer, popups, scrolling, and the most-used widgets. Read it before you draw any UI inside a phone app, and come back whenever you are tempted to hand-roll a button, a text block, or a scroll region: there is almost certainly a component for it already.

## Background: how Aetherphone draws UI

The whole phone is one Dalamud window (src/Aetherphone/Windows/PhoneWindow.cs) rendered with Dear ImGui, an immediate mode UI library: nothing is retained between frames, and every frame your code re-declares everything on screen. Aetherphone mostly does not use stock ImGui widgets. Instead, components paint directly onto a draw list (an ImGui command buffer you get from `ImGui.GetWindowDrawList()`) using screen-space coordinates, and resolve hover and clicks themselves through `UiInteract`. Rectangles are passed around as the `Rect` record (src/Aetherphone/Core/Rect.cs), which has `Min`, `Max`, `Center`, `Width`, `Height`, and helpers like `Inset`.

Colors and surfaces come from `AppSkin` and `AppPalette` (per-app skin) or `PhoneTheme` (system chrome); see [App framework](app-framework.md) for how an app receives them.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Windows/Components/Typography.cs | All text drawing: measure, draw, wrap, fit, ellipsize |
| src/Aetherphone/Windows/Components/TextStyles.cs | The style ladder: named `TextStyle` values (scale + weight) |
| src/Aetherphone/Core/FontService.cs | Font atlas: Inter in 4 weights and 12 size buckets, lazy glyphs |
| src/Aetherphone/Windows/Components/Metrics.cs | Spacing, radius, size, and stroke tokens |
| src/Aetherphone/Windows/Components/UiInteract.cs | Hit-testing, click claims, input blocking |
| src/Aetherphone/Windows/Components/DropdownMenu.cs | Anchored context menu with open/dismiss lifecycle |
| src/Aetherphone/Windows/Components/DragScrollHost.cs | Kinetic touch-style scrolling for child windows |
| src/Aetherphone/Windows/Components/AppSurface.cs | Standard scrollable app body (child window + DragScrollHost) |
| src/Aetherphone/Windows/Components/ScrollLayout.cs | `StableContentWidth()`, the scrollbar feedback-loop fix |
| src/Aetherphone/Windows/Components/FeedVirtualizer.cs | Skips offscreen rows in long feeds |
| src/Aetherphone/Windows/Components/Toggle.cs | iOS-style switch |
| src/Aetherphone/Windows/Components/ChipRail.cs | Single pannable row of filter chips |
| src/Aetherphone/Windows/Components/SoftWrapField.cs | Multiline input with soft wrapping and mention support |
| src/Aetherphone/Windows/Components/ConfirmOverlay.cs | Modal confirm layer driven by `ConfirmService` |
| src/Aetherphone/Windows/Components/EmojiRender.cs | Draws emoji images inline with text |
| src/Aetherphone/Core/Rect.cs | The rectangle type every component takes |

## Typography

`TextStyle` (src/Aetherphone/Windows/Components/TextStyles.cs) is a record of a font scale and a `FontWeight` (Regular, Medium, SemiBold, Bold, defined in src/Aetherphone/Core/FontService.cs). `TextStyles` is the ladder of named styles: `LargeTitle`, `Title1`, `Title2`, `Title3`, `Headline`, `Body`, `BodyEmphasized`, `Callout`, `Subheadline`, `SubheadlineEmphasized`, `Footnote`, `FootnoteEmphasized`, `Caption1`, `Caption2`, `IconLabel`.

The rule: all text goes through `Typography` with a `TextStyles` entry. Never invent a magic scale like `0.83f`. The ladder scales exist as size buckets in `FontService` (the `SizeMultipliers` array), so an off-ladder scale lands in the nearest bucket anyway and only makes the call site misleading.

```csharp
var drawList = ImGui.GetWindowDrawList();
Typography.Draw(drawList, titlePosition, title, ui.TitleInk, TextStyles.Title2);
var bodyHeight = Typography.DrawWrappedLeft(bodyPosition, body, ui.BodyInk, TextStyles.Body, maxWidth);
```

The main entry points in src/Aetherphone/Windows/Components/Typography.cs:

| Method | Use |
| --- | --- |
| `Measure(text, style)` | Text size for layout math |
| `MeasureWrappedBlock(text, style, maxWidth)` | Size of a wrapped block before drawing it |
| `LineHeight(style)` | Line height including spacing |
| `Draw(drawList, position, text, color, style)` | Paint a single line at a screen position |
| `DrawCentered(drawList, center, text, color, style)` | Paint centered on a point |
| `DrawWrappedLeft(topLeft, text, color, style, maxWidth)` | Wrapped block, left aligned, returns height |
| `DrawWrappedCentered(topCenter, text, color, style, maxWidth)` | Wrapped block, centered, returns height |
| `WrapText(text, style, maxWidth)` | Get the wrapped lines to lay out yourself |
| `FitText(text, maxWidth, style)` | Ellipsize a single line that must not wrap |
| `FitScale(text, maxWidth, maxScale, minScale, weight)` | Shrink a label until it fits |

Never-overflow is the default posture, not an opt-in. `DrawCentered` auto-wraps when the text is wider than the window content region (see `AutoWrapWidth` in Typography.cs). For single-line labels that cannot wrap (buttons, pills), use `FitText` to ellipsize or `Marquee.DrawCenteredAuto` (src/Aetherphone/Windows/Components/Marquee.cs) to scroll the label, as `ConfirmDialog.DrawPillButton` does. Wrapping is word-based with CJK-aware per-character breaking, and results are cached per font generation, so calling the wrapped helpers every frame is fine.

Every `Typography` method that takes text calls `Plugin.Fonts.NoticeText(text)` internally (`WrapCurrent` is the one exception). That registers the characters with the `FontService` glyph ledger so missing glyphs (CJK and rare symbols are loaded lazily) get added on the next atlas rebuild. If you ever draw text without `Typography`, you must call `NoticeText` yourself or the glyphs may render as placeholders.

Watch for the cursor landmine: `Typography.Draw` and `Typography.DrawCentered` have overloads without an `ImDrawListPtr` parameter. Those call `ImGui.SetCursorScreenPos` and `ImGui.TextUnformatted`, which moves the ImGui layout cursor and can silently shift everything you draw afterwards. Inside custom-painted layouts, always pass the draw list explicitly; the drawList overloads use `drawList.AddText` and leave the cursor alone.

## Layout and spacing tokens

`Metrics` (src/Aetherphone/Windows/Components/Metrics.cs) holds the shared design tokens:

- `Metrics.Space`: `Xxs` 4, `Xs` 6, `Sm` 8, `Md` 12, `Lg` 16, `Xl` 22, `Xxl` 32
- `Metrics.Radius`: `Field` 9, `Sm` 8, `Md` 12, `Card` 16, `Lg` 18, `TileFactor` 0.28
- `Metrics.Size`: `Header` 42, `Row` 46, `FieldHeight` 34, `FieldMultiline` 88, `ToggleWidth` 46, `ToggleHeight` 28, `IconTile` 28, `HeroRing` 56, `HomeIndicatorInset` 34
- `Metrics.Stroke`: `Hairline` 1, `Thin` 1.4, `Ring` 2

The values are unscaled design units, authored against a 360 wide phone. Multiply by `UiScale.Current` (Dalamud's UI scale times the phone zoom) at the call site:

```csharp
var scale = UiScale.Current;
ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
```

Do not hard-code pixel values for gaps, radii, or row heights. If the token you need is missing, add it to `Metrics` rather than inlining a number; that keeps rhythm consistent across every app. Rounded rectangles are drawn with `Squircle.Fill` and `Squircle.Stroke` (src/Aetherphone/Windows/Components/Squircle.cs), the one corner family used across the phone.

## Input: UiInteract

Because widgets are painted onto draw lists instead of created as ImGui items, ImGui does not know they exist. `UiInteract` (src/Aetherphone/Windows/Components/UiInteract.cs) is the hit-testing layer that replaces `ImGui.Button` and friends.

### Hover gates

- `UiInteract.Hover(min, max)`: the normal gate. True only when the mouse is over the rect, the phone window is hovered (`PhoneWindow.Draw` feeds `SetWindowHovered` every frame), input is not blocked this frame, and no overlay has reserved the pointer via `HoverOverlay`.
- `UiInteract.HoverWindowOnly(min, max)`: skips the blocked and overlay checks. Overlays and menus use this for their own rows, because they are the thing doing the blocking.
- `UiInteract.ClickedOutside(min, max)`: true when the left button was clicked while the pointer is outside the rect. Used to dismiss popups.
- `UiInteract.BlockThisFrame()`: call once at the top of a frame when a modal surface (menu, picker, overlay) is open; every `Hover` and every scroll press underneath returns false for that frame.

### Clicks and claims

`UiInteract.Click(min, max, hovered)` implements press-then-release taps:

1. On the frame the left button goes down over a hovered rect, `Click` records that rect (in scroll-compensated content space) as the pending tap claim. Every `Click` call that frame overwrites the claim, so the last claimant in draw order wins the press. Widgets draw back to front, so the topmost widget is the last to call `Click` and correctly wins.
2. On the frame the button is released, `Click` returns true only for the call whose rect matches the stored claim (within half a pixel) and is still hovered. Dragging off a widget before releasing cancels the tap; scrolling calls `UiInteract.CancelPendingTap()`.

The corollary: a parent row drawn after its child buttons would steal the claim. Gate the parent's `hovered` argument on not hovering the child. Real example from src/Aetherphone/Apps/Settings/Pages/AccountPage.cs:

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

Conveniences: `UiInteract.HoverClick(min, max)` combines hover, hand cursor, and click. `HoverClickCircle(center, radius)` does the same for round buttons. `HoverHighlight(drawList, min, max, rounding)` paints the standard press/hover tint.

## Popups and dropdowns

`DropdownMenu` (src/Aetherphone/Windows/Components/DropdownMenu.cs) is the anchored context menu. Lifecycle, as used in src/Aetherphone/Apps/Jobs/JobsApp.cs:

```csharp
private readonly DropdownMenu menu = new();

public void Draw(in PhoneContext context)
{
    menu.Gate();

    if (UiInteract.HoverClick(buttonRect.Min, buttonRect.Max))
    {
        menu.Toggle("jobs.color", buttonRect);
    }

    var picked = menu.Draw(context.Content, context.Theme, items);
    if (picked >= 0)
    {
        Apply(items[picked]);
    }
}
```

- `Gate()` runs first in the frame: while the menu is open it calls `UiInteract.BlockThisFrame()` so the UI underneath is inert.
- `Toggle(id, anchorRect)` opens the menu anchored to a rect, or closes it if the same id is already open. It records `openedFrame = ImGui.GetFrameCount()`.
- `Draw(screen, theme, items)` paints on the foreground draw list (above everything in the window), returns the tapped item index or -1, and handles dismissal: any left click outside the menu closes it, except on `openedFrame` itself.

The `openedFrame` guard is mandatory in any popup you build: the click that opened the popup is, by definition, outside the popup rect, so without the guard the open click immediately dismisses it on the same frame. `ConfirmOverlay` applies the same pattern before honoring `UiInteract.ClickedOutside` on its card. Menu rows use `HoverWindowOnly` because `Gate()` has blocked normal hover.

For confirms, do not draw `ConfirmDialog` yourself. Apps receive a `ConfirmService` (src/Aetherphone/Core/Confirm/ConfirmService.cs) and call `confirm.Ask(new ConfirmRequest { ... })` or `confirm.Alert(...)`; the shell-level `ConfirmOverlay` dims the screen, animates the card in, and routes the buttons back to your `Confirm`/`Cancel` callbacks. `ConfirmDialog` (src/Aetherphone/Windows/Components/ConfirmDialog.cs) is the presentational card it renders, and its `DrawPillButton` is reusable for pill-shaped buttons.

## Scrolling

### DragScrollHost

`DragScrollHost` (src/Aetherphone/Windows/Components/DragScrollHost.cs) gives child windows phone-style kinetic scrolling: press and drag anywhere to scroll, release to coast, mouse wheel still works. Call `DragScrollHost.Begin(key)` at the top of a scrollable child region (key from `ImGui.GetID`), and pass your window flags through `DragScrollHost.ScrollFlags` so the native scrollbar is hidden while drag scrolling is enabled. While a drag is in progress it calls `UiInteract.BlockThisFrame()` and cancels pending taps, so rows do not fire when the user was scrolling.

Most apps never call it directly. `AppSurface.Begin(area)` (src/Aetherphone/Windows/Components/AppSurface.cs) wraps the standard app body: a padded child window with `DragScrollHost` attached, returning a scope with `Pull` (overscroll distance for `PullToRefresh`), `Dragging`, and `JumpToTop()`.

### StableContentWidth

Drag scrolling is only active while the phone window position is locked (`PhoneWindow.PreDraw` sets `DragScrollHost.Enabled` from `Configuration.LockPosition`); with the window unlocked, dragging would move the window, so the native ImGui scrollbar comes back. With a native scrollbar a feedback loop becomes possible: content sized to the full available width forces a scrollbar, the scrollbar shrinks the available width, the content no longer overflows, the scrollbar disappears, and the layout shakes every frame. `ScrollLayout.StableContentWidth()` (src/Aetherphone/Windows/Components/ScrollLayout.cs) breaks the loop by reserving the scrollbar width whenever the scrollbar is not yet showing. Use it instead of `ImGui.GetContentRegionAvail().X` whenever you size rows or cards inside a scrollable region.

### FeedVirtualizer

`FeedVirtualizer` (src/Aetherphone/Windows/Components/FeedVirtualizer.cs) keeps long feeds cheap by caching each row's measured height and skipping the draw when the row is offscreen. Pattern from src/Aetherphone/Apps/Aethergram/AethergramApp.cs:

```csharp
private readonly FeedVirtualizer feedVirtualizer = new(400f);

feedVirtualizer.BeginFrame(store.FeedSource(scope));
for (var index = 0; index < snapshot.Length; index++)
{
    var post = snapshot[index];
    var revision = post.CommentCount > 0 ? 1 : 0;
    if (feedVirtualizer.Skip(post.Id, revision))
    {
        continue;
    }

    DrawGramCard(post);
    feedVirtualizer.Record(post.Id, revision);
}
```

`Skip` advances the cursor by the cached height when the row is out of view (beyond the cull margin); `Record` captures the height after drawing. Bump the `revision` argument whenever something changes the row's height, or the stale cached height will be used. `BeginFrame` invalidates the cache on width or font changes and trims the backing store to `rowCap` rows while the list is parked at the top.

For paged loading, `InfiniteScroll.ReachedBottom()` (src/Aetherphone/Windows/Components/InfiniteScroll.cs) reports when the scroll position is near the end so you can fetch the next page, and `InfiniteScroll.DrawLoadingRow` draws the three-dot loading indicator; see [Messaging and chat](messaging-and-chat.md) for the chat-side pagination contract.

## Common widgets

### Toggle

`Toggle.Draw(id, bounds, value, theme)` (src/Aetherphone/Windows/Components/Toggle.cs) draws the animated switch and returns the new value, not a "was clicked" flag. Assign the result; comparing it to the old value tells you whether it changed:

```csharp
var toggleRect = new Rect(min, min + new Vector2(Metrics.Size.ToggleWidth * scale, Metrics.Size.ToggleHeight * scale));
var enabled = Toggle.Draw($"alarm.{alarm.Id}", toggleRect, alarm.Enabled, theme);
if (enabled != alarm.Enabled)
{
    alarm.Enabled = enabled;
    configuration.Save();
}
```

### ChipRail

`ChipRail` (src/Aetherphone/Windows/Components/ChipRail.cs) is the one way to show a row of filter chips: a single horizontal row that clips and pans by dragging. Chips never wrap into a second line; if they overflow, the user drags the rail sideways. It is stateful (pan offset), so keep one instance per rail:

```csharp
private readonly ChipRail filterRail = new();

var tapped = filterRail.Draw(ui, labels, active);
if (tapped >= 0)
{
    selectedFilter = tapped;
}
```

A tap only registers if the pointer traveled less than the drag slop, so panning does not select chips.

### Other frequently used widgets

| Widget | One-liner |
| --- | --- |
| `EmptyState.Draw(body, ui, icon, title, hint)` | Centered icon, title, and wrapped hint for empty lists (src/Aetherphone/Windows/Components/EmptyState.cs) |
| `AvatarView.Draw` / `AvatarView.DrawRemote` | Circular avatar with monogram fallback, loading pulse, and fade-in (src/Aetherphone/Windows/Components/AvatarView.cs) |
| `SoftWrapField.Multiline(id, ref value, maxLength, size, wrapWidth)` | Multiline composer input; wraps visually without inserting real newlines, supports `MentionAutocomplete` (src/Aetherphone/Windows/Components/SoftWrapField.cs) |
| `SearchField.Draw` / `SearchField.DrawSubmit` | Pill search input with search icon; `Draw` adds a clear button, `DrawSubmit` returns true on Enter (src/Aetherphone/Windows/Components/SearchField.cs) |
| `Elevation.Card` / `Elevation.Floating` | Layered soft drop shadows behind cards and floating surfaces (src/Aetherphone/Windows/Components/Elevation.cs) |
| `AppHeader.Draw(context, title, onBack)` | Standard app title bar with optional back button (src/Aetherphone/Windows/Components/AppHeader.cs) |
| `HoverButton.Circle` | Round icon button with hover ring (src/Aetherphone/Windows/Components/HoverButton.cs) |
| `PopoverSurface.Draw` | The floating card background menus sit on (src/Aetherphone/Windows/Components/PopoverSurface.cs) |
| `PullToRefresh` | Overscroll spinner fed by `AppSurface` `Pull` (src/Aetherphone/Windows/Components/PullToRefresh.cs) |
| `Marquee.DrawCenteredAuto` | Auto-scrolls a label that is too wide for its slot (src/Aetherphone/Windows/Components/Marquee.cs) |

`SoftWrapField.Multiline` is the composer field for anything the user types more than one line into (posts, notes, feedback). It maintains a display string with soft line breaks and a logical string without them, via `SoftWrap` (src/Aetherphone/Windows/Components/SoftWrap.cs), so stored text never contains layout-only newlines.

## Emoji in text

Emoji are not font glyphs. Messages carry shortcodes like `:sparkles:`, and rendering resolves them to PNG images:

- `EmojiCatalog` (src/Aetherphone/Core/Emoji/EmojiCatalog.cs) loads catalog.json from the plugin's Emoji asset folder and maps shortcodes to codepoint-named image files.
- `EmojiScanner.Collect` (src/Aetherphone/Core/Emoji/EmojiScanner.cs) finds `:shortcode:` spans in a string.
- `EmojiRender.Draw` (src/Aetherphone/Windows/Components/EmojiRender.cs) draws one emoji image at text position, with `Advance` and `LineHeight` for layout; `EmojiImages` (src/Aetherphone/Core/Emoji/EmojiImages.cs) loads the textures.

You rarely call these directly. `RichText.Build` (src/Aetherphone/Windows/Components/RichText.cs) lays out a paragraph into plain, link, mention, and emoji runs, and `RichText.Draw` paints them; chat and social surfaces go through it. The images themselves are Twemoji assets fetched and cataloged by tools/emoji-generator; see [Assets and media](assets-and-media.md).

## Motion

Animated components (the `Toggle` knob, `ConfirmOverlay` reveal) use `Spring` (src/Aetherphone/Core/Animation/Spring.cs), a critically damped smoother that clamps on target crossing, so motion settles without bouncing. Follow that: no overshoot or bounce in phone UI. `Easing` (src/Aetherphone/Core/Animation) provides curves like `EaseOutQuint` for reveals.

## Which widget do I reach for

| I need to... | Reach for |
| --- | --- |
| Draw any text | `Typography` + a `TextStyles` entry |
| Show a paragraph that must not overflow | `Typography.DrawWrappedLeft` / `DrawWrappedCentered` |
| Keep a one-line label inside a slot | `Typography.FitText` or `Marquee.DrawCenteredAuto` |
| Build a scrollable app body | `AppSurface.Begin(area)` |
| Size rows inside a scroll region | `ScrollLayout.StableContentWidth()` |
| Render a long feed | `FeedVirtualizer` + `InfiniteScroll.ReachedBottom` |
| Make a rect clickable | `UiInteract.HoverClick` (or `Hover` + `Click`) |
| Flip a boolean setting | `Toggle.Draw` |
| Offer filters in a row | `ChipRail` |
| Show "nothing here yet" | `EmptyState.Draw` |
| Confirm a destructive action | `ConfirmService.Ask` (never draw `ConfirmDialog` yourself) |
| Show a context menu | `DropdownMenu` |
| Draw a person's picture | `AvatarView` |
| Take multiline text input | `SoftWrapField.Multiline` |
| Add a search box | `SearchField` |
| Let someone pick a color | `ColorField.Draw` (shade square plus hue rail) |
| Add depth behind a card | `Elevation.Card` + `Squircle.Fill` |
| Space or round anything | `Metrics` tokens times `UiScale.Current` |

## Gotchas

- `Typography.Draw` and `Typography.DrawCentered` overloads without an `ImDrawListPtr` first parameter move the ImGui cursor (`SetCursorScreenPos` + `TextUnformatted`). In custom-painted layouts this shifts everything drawn after them. Pass the draw list explicitly.
- `Toggle.Draw` returns the new value, not a clicked flag. Writing `if (Toggle.Draw(...)) { ... }` treats "switch is on" as "switch was clicked" and fires every frame while on.
- A popup without the `openedFrame` guard closes on the same click that opened it, because the opening click lands outside the popup rect. Compare `ImGui.GetFrameCount()` to the frame the popup opened before honoring outside-click dismissal, as `DropdownMenu.Draw` and `ConfirmOverlay.Draw` do.
- `UiInteract.Click` claims: the last `Click` call of the press frame wins. A parent row hit-tested after its child button steals the child's tap unless you gate the parent with `hovered && !overChildRect` (see AccountPage.cs above).
- Using `ImGui.GetContentRegionAvail().X` to size content in a native-scrollbar region causes the scrollbar show/hide feedback loop (layout shakes every frame). Use `ScrollLayout.StableContentWidth()`.
- `Metrics` values are unscaled design units. Forgetting `UiScale.Current` makes layouts wrong at any UI scale other than 100 percent and at any phone size other than 360 wide.
- `FeedVirtualizer.Skip`/`Record` cache row heights per id and revision. If a row can change height (comments appear, text expands), change its revision or the feed will draw with stale heights.
- Text drawn without `Typography` skips `Plugin.Fonts.NoticeText`, so characters outside the base glyph ranges (CJK in particular) may render as placeholder boxes until something else notices them.
- `DropdownMenu` rows and other blocked-frame overlays must hit-test with `UiInteract.HoverWindowOnly`, because `Gate()` makes plain `Hover` return false while they are open.

## Related docs

- [Getting started](getting-started.md): build, dev loop, Dalamud and ImGui primer
- [Architecture](architecture.md): plugin boot, frame loop, shell, window
- [App framework](app-framework.md): the IPhoneApp contract, skins, navigation
- [Creating an app](creating-an-app.md): tutorial that builds a new phone app from zero using these components
- [Messaging and chat](messaging-and-chat.md): the shared chat layer built on these components
- [Assets and media](assets-and-media.md): fonts, emoji, icons, and the generator tools
- [Localization](localization.md): where user-facing strings come from
- [Conventions](conventions.md): the rulebook for code, copy, and commits; its UI section is the checklist form of this doc
