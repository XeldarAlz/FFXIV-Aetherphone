# UI toolkit

This doc covers the reusable widget library in src/Aetherphone/Windows/Components: typography, spacing tokens, materials, the custom input layer, popups, scrolling, and the most-used widgets. Read it before you draw any UI inside a phone app, and come back whenever you are tempted to hand-roll a button, a text block, or a scroll region: there is almost certainly a component for it already.

## Background: how Aetherphone draws UI

The whole phone is one Dalamud window (src/Aetherphone/Windows/PhoneWindow.cs) rendered with Dear ImGui, an immediate mode UI library: nothing is retained between frames, and every frame your code re-declares everything on screen. Aetherphone mostly does not use stock ImGui widgets. Instead, components paint directly onto a draw list (an ImGui command buffer you get from `ImGui.GetWindowDrawList()`) using screen-space coordinates, and resolve hover and clicks themselves through `UiInteract`. Rectangles are passed around as the `Rect` record (src/Aetherphone/Core/Rect.cs), which has `Min`, `Max`, `Center`, `Width`, `Height`, and helpers like `Inset`.

Colors and surfaces come from `AppSkin` and `AppPalette` (per-app skin) or `PhoneTheme` (system chrome); see [App framework](app-framework.md) for how an app receives them.

## How the folder is laid out

The toolkit's files are grouped by kind: `Primitives` (drawing, text, tokens, materials, motion), `Layout` (surfaces, headers, tab bars, scrolling, lists), `Fields` (inputs and buttons), `Sheets` (sheets, dialogs, overlays), `Chrome` (device body, tiles, badges), `Media` (photos, emoji, stories, card art), `Chat`, `Notify`, `Skin`, and `Social` (chrome shared by the social apps: ink tokens, screen headers, underline tabs, user rows, feed filters, the double-tap like).

**Every file in every one of those folders declares the same flat namespace, `Aetherphone.Windows.Components`**, so consuming the toolkit stays a single `using` no matter how many components a screen draws. This is the one place in the tree where a namespace deliberately does not follow its folder, and the "Verify namespace matches folder" CI guard checks that subtree against the flat namespace instead. Sub-namespacing was measured before it was rejected: it would have added 792 `using` lines across 424 files, and the busiest drawing files would have needed five to nine imports each. The folders are for finding things; the namespace is the contract.

A component used by exactly one app does not belong here. It lives in that app, and 25 of them were moved out for that reason.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Windows/Components/Primitives/Typography.cs | All text drawing: measure, draw, wrap, fit, ellipsize |
| src/Aetherphone/Windows/Components/Primitives/TextStyles.cs | The style ladder: named `TextStyle` values (scale + weight) |
| src/Aetherphone/Core/FontService.cs | Font atlas: Inter in 4 weights and 12 size buckets, lazy glyphs |
| src/Aetherphone/Windows/Components/Primitives/Metrics.cs | Spacing, radius, size, and stroke tokens |
| src/Aetherphone/Windows/Components/Primitives/Material.cs | Navigation-layer glass (`LiquidGlass`, `ThemedGlass`, `AccentGlass`), the tone picker `ToneFor`, the `Veil` dim, and the corner-following `Sheen` |
| src/Aetherphone/Windows/Components/Primitives/Surfaces.cs | Content fill ladder (`Surfaces.Fill`) and `ControlInk` |
| src/Aetherphone/Windows/Components/Primitives/UiInteract.cs | Hit-testing, click claims, input blocking |
| src/Aetherphone/Windows/Components/Sheets/DropdownMenu.cs | Anchored context menu with open/dismiss lifecycle |
| src/Aetherphone/Windows/Components/Layout/DragScrollHost.cs | Kinetic touch-style scrolling for child windows |
| src/Aetherphone/Windows/Components/Layout/AppSurface.cs | Standard scrollable app body (child window + DragScrollHost) |
| src/Aetherphone/Windows/Components/Layout/ScrollLayout.cs | `StableContentWidth()`, the scrollbar feedback-loop fix |
| src/Aetherphone/Windows/Components/Layout/FeedVirtualizer.cs | Skips offscreen rows in long feeds |
| src/Aetherphone/Windows/Components/Layout/FeedCell.cs | Edge-to-edge list cell: flat hover wash, whole-cell tap, trailing hairline |
| src/Aetherphone/Windows/Components/Layout/GroupCard.cs | Grouped inset list card (theme or AppSkin overload) with fixed rows |
| src/Aetherphone/Windows/Components/Layout/ListSection.cs | Section header over cell lists (SettingsSection and ListSection.Label delegate here) |
| src/Aetherphone/Windows/Components/Layout/TabBar.cs | Floating glass bottom tab bar with a sliding highlight, badges, and an optional action circle (geometry in TabBarLayout.cs) |
| src/Aetherphone/Windows/Components/Sheets/Sheet.cs | Bottom sheet presenter: veil, themed glass panel at the screen radius, grabber, medium and large detents, drag to dismiss |
| src/Aetherphone/Windows/Components/Sheets/ActionSheet.cs | iOS bottom action sheet with an optional header band, built on `Sheet` |
| src/Aetherphone/Windows/Components/Social/SocialInk.cs | Social ink token set derived from any `AppPalette` (accent link/deep/wash, faint ink, glass, chips, button fills) |
| src/Aetherphone/Windows/Components/Social/SocialChrome.cs | Glass back chip screen header, top bar icon buttons with knockout count badges, inline stats, section labels, bar backdrop |
| src/Aetherphone/Windows/Components/Social/UnderlineTabs.cs | Sliding underline tabs: a text pair or an icon row |
| src/Aetherphone/Windows/Components/Social/SocialUserRow.cs | Avatar + badged name + subtitle row with a trailing slot for a pill |
| src/Aetherphone/Windows/Components/Social/SocialProfilePages.cs | Social confirm/report plumbing (block, delete post/comment) plus handle validation and list titles |
| src/Aetherphone/Windows/Components/Social/FeedFilterSheet.cs | Bottom sheet of iOS toggles plus a region chip row and a Done pill for feed filters |
| src/Aetherphone/Windows/Components/Sheets/ActionReveal.cs | Open/close state for an anchored popover (progress, opened frame, outside-click dismiss) |
| src/Aetherphone/Windows/Components/Notify/ShellToast.cs | Shell-level bottom-pill toast (replaced the mouse-anchored CopyToast) |
| src/Aetherphone/Windows/Components/Fields/Toggle.cs | iOS-style switch |
| src/Aetherphone/Windows/Components/Fields/GlassField.cs | Borderless text, search, and title inputs that sit on a field surface |
| src/Aetherphone/Windows/Components/Fields/HoverTooltip.cs | Hover labels for icon-only controls, queued and drawn on top once per frame |
| src/Aetherphone/Windows/Components/Layout/ChipRail.cs | Single pannable row of filter chips |
| src/Aetherphone/Windows/Components/Layout/PanRail.cs | Horizontal kinetic pan state for a row of cards drawn by the caller (Photos month rail) |
| src/Aetherphone/Windows/Components/Primitives/SoftWrapField.cs | Multiline input with soft wrapping and mention support |
| src/Aetherphone/Windows/Components/Sheets/ConfirmOverlay.cs | Modal confirm layer driven by `ConfirmService` |
| src/Aetherphone/Windows/Components/Media/EmojiRender.cs | Draws emoji images inline with text |
| src/Aetherphone/Core/Rect.cs | The rectangle type every component takes |

## Typography

`TextStyle` (src/Aetherphone/Windows/Components/Primitives/TextStyles.cs) is a record of a font scale and a `FontWeight` (Regular, Medium, SemiBold, Bold, defined in src/Aetherphone/Core/FontService.cs). `TextStyles` is the ladder of named styles: `Hero`, `LargeTitle`, `Title1`, `Title2`, `Title3`, `Headline`, `Body`, `BodyEmphasized`, `Callout`, `Subheadline`, `SubheadlineEmphasized`, `Footnote`, `FootnoteEmphasized`, `Caption1`, `Caption2`, `StatusDigits`, `IconLabel`, `WidgetDisplay`, `WidgetDisplayCompact`.

The rule: all text goes through `Typography` with a `TextStyles` entry. Never invent a magic scale like `0.83f`. `FontService` snaps every scale to the nearest of its size buckets (the `SizeMultipliers` array), so an off-ladder scale lands in a bucket anyway and only makes the call site misleading. Four named styles sit off the buckets: `IconLabel` (0.85) rides the 0.88 bucket, and `Hero` (2.30), `WidgetDisplay` (2.3) and `WidgetDisplayCompact` (1.8) all ride 1.90, the largest bucket, through every regular `Typography` call. Only `Typography.MeasureExact` and `Typography.DrawCenteredExact`, which take a raw scale and weight, render the true size by scaling the nearest bucket's glyphs.

```csharp
var drawList = ImGui.GetWindowDrawList();
Typography.Draw(drawList, titlePosition, title, ui.TitleInk, TextStyles.Title2);
var bodyHeight = Typography.DrawWrappedLeft(bodyPosition, body, ui.BodyInk, TextStyles.Body, maxWidth);
```

The main entry points in src/Aetherphone/Windows/Components/Primitives/Typography.cs:

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

Never-overflow is the default posture, not an opt-in. `DrawCentered` auto-wraps when the text is wider than the window content region (see `AutoWrapWidth` in Typography.cs). For single-line labels that cannot wrap (buttons, pills), use `FitText` to ellipsize or `Marquee.DrawCenteredAuto` (src/Aetherphone/Windows/Components/Primitives/Marquee.cs) to scroll the label, as `ConfirmDialog.DrawPillButton` does. Wrapping is word-based with CJK-aware per-character breaking, and results are cached per font generation, so calling the wrapped helpers every frame is fine.

### Name effects

Badged account names animate through `Typography.Draw(drawList, position, text, color, style, effect)`, where the `TextEffect` comes from `NameEffects.For(badge, light)` (community catalog badges) or `NameEffects.For(role, light)` (legacy role flags). The painter lives in src/Aetherphone/Windows/Components/Primitives/Typography.Effects.cs and works in four families, all stateless and allocation free so any number of names can carry one per frame:

- **Tints** (`Gradient`, `Wave`, `Spectrum`, `Candy`, `Horizon`, `Chrome`, `Blaze`, ...) draw the text once and recolor the vertices ImGui appended: `across` is the vertex x over the text width, `down` its y over the line height, and the glyph index is `vertexIndex >> 2` because every visible glyph is one quad of four vertices.
- **Motion** (`Bounce`, `Shiver`, `Wobble`, `Pop`, `Flipboard`, `Typewriter`) walk those same quads and move, scale, rotate or fade each glyph about its own center, so the measured width and the layout never change.
- **Decorations** (`Glow`, `Outline`, `Shadow`, `Longshadow`, `Emboss`, `Chromatic`, `Neon`, `Sweep`, `Comet`, `Scan`, `Stripes`, ...) are extra `AddText` copies, offset or under a clip band. Clip bands are fine here because nothing rotates afterwards (see the vertex rotation note in docs/conventions.md).
- **Motes** (`Sakura`, `Snowfall`, `Fireflies`, `Hearts`, `Glitter`, `Bubbles`, `Confetti`, `Storm`, the embers under `Blaze`) are procedural particles: each slot derives its cycle, spawn point, drift and size from `Hash01(slot, cycle, salt)` and `Pulse.Seconds`, so there is no particle state and no spawner.

Colors come pre-themed: `NameEffects` runs every badge color through `RoleInk.For`/`RoleInk.Highlight` and packs them as the `Crest` (second color, or the first lifted) and an optional `WaveRamp` (cyclic `Sample` for travelling kinds, non-wrapping `SampleAcross` for `Horizon`/`Blaze`/static `Gradient`). `Seed` is a hash of the badge id, so two badges with the same effect never fire in lockstep. Adding an effect touches five places in lockstep: `NameEffectKind` in TextStyles.cs, `BadgeStyle.ParseEffect`, `NameEffects` (period, ramp and seed rules), the `Paint` switch in Typography.Effects.cs, and the key lists in src/Aetherphone.Tests/NameEffectSyncTests.cs; the backend `NameEffectKinds.cs` and the mod console painter `name-effects.js` carry the same key, and the console previews are a 1:1 port of this file, so a change here is a change there.

Every `Typography` method that takes text calls `Plugin.Fonts.NoticeText(text)` internally (`WrapCurrent` is the one exception). That registers the characters with the `FontService` glyph ledger so missing glyphs (CJK and rare symbols are loaded lazily) get added on the next atlas rebuild. If you ever draw text without `Typography`, you must call `NoticeText` yourself or the glyphs may render as placeholders.

Watch for the cursor landmine: `Typography.Draw` and `Typography.DrawCentered` have overloads without an `ImDrawListPtr` parameter. Those call `ImGui.SetCursorScreenPos` and `ImGui.TextUnformatted`, which moves the ImGui layout cursor and can silently shift everything you draw afterwards. Inside custom-painted layouts, always pass the draw list explicitly; the drawList overloads use `drawList.AddText` and leave the cursor alone.

## Layout and spacing tokens

`Metrics` (src/Aetherphone/Windows/Components/Primitives/Metrics.cs) holds the shared design tokens:

- `Metrics.Space`: `Xxs` 4, `Xs` 6, `Sm` 8, `Glass` 10, `Md` 12, `Lg` 16, `Xl` 22, `Xxl` 32, `GlassInset` 10
- `Metrics.Radius`: `Field` 9, `Sm` 8, `Md` 12, `Card` 16, `Lg` 18, `Widget` 22, `Grouped` 22, `TileFactor` 0.28, `HomeTileFactor` 0.26
- `Metrics.Size`: `Header` 42, `Row` 46, `FieldHeight` 34, `FieldMultiline` 88, `ToggleWidth` 46, `ToggleHeight` 28, `HintIconHeight` 22, `HintIconGap` 16, `IconTile` 28, `HeroRing` 56, `HomeIndicatorInset` 34, `GrabberWidth` 36, `GrabberHeight` 5, `Pill` 44, `TapTarget` 44, `GlassButton` 36
- `Metrics.Stroke`: `Hairline` 1, `Thin` 1.4, `Ring` 2

The values are unscaled design units, authored against a 360 wide phone. Multiply by `UiScale.Current` (Dalamud's UI scale times the phone zoom) at the call site:

```csharp
var scale = UiScale.Current;
ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
```

Do not hard-code pixel values for gaps, radii, or row heights. If the token you need is missing, add it to `Metrics` rather than inlining a number; that keeps rhythm consistent across every app. Rounded rectangles are drawn with `Squircle.Fill` and `Squircle.Stroke` (src/Aetherphone/Windows/Components/Primitives/Squircle.cs), the one corner family used across the phone.

## Materials and the two-layer rule

The phone follows the iOS split between a navigation layer that floats over the screen and a content layer that scrolls. [Conventions](conventions.md) keeps the checklist form; this section is the API behind it.

**Glass belongs to the navigation layer only.** Nav bar buttons, the bottom tab bar, floating toolbars, sheets, menus, popovers, toasts, the home dock and Control Center are glass. Anything that scrolls with the content (cards, rows, buttons, fields, chips, heroes) is a fill, never glass, and glass never sits on glass.

The glass layer lives in `Material` (src/Aetherphone/Windows/Components/Primitives/Material.cs):

- `Material.LiquidGlass(drawList, min, max, radius, scale, tone, brightness, opacity)` is the glass body. It samples what `WallpaperBackdrop` recorded behind the rect, tints it for the `GlassTone` (`Light` or `Dark`), and adds the rim, the refraction edge, and a pointer light that follows the mouse. With nothing recorded behind it, it falls back to a denser flat tint.
- `Material.ThemedGlass(drawList, min, max, radius, scale, theme)`, or the overload that takes a background color, is `LiquidGlass` with the tone picked for you. `Material.ToneFor(theme)` returns `GlassTone.Light` when the background's luminance is at least 0.5 and `GlassTone.Dark` otherwise; call it yourself when the ink on top depends on the tone, as `Sheet` does for its grabber.
- `Material.AccentGlass` is glass filled with an accent; `GlassCircle` uses it for an active button.
- `GlassCircle.Draw` and `GlassCircle.Icon` (src/Aetherphone/Windows/Components/Chrome/GlassCircle.cs) are the round glass buttons, with the shared press sink and an optional hover label, for controls that float over media (Camera, the Photos viewer). Nav bar buttons come from the `NavBarButton`s you pass to `AppHeader.EndLargeTitle`.
- `Material.Veil(drawList, min, max, dim, rounding)` is the black dim under a sheet or overlay.
- `Material.Sheen` draws a top highlight that follows the squircle corner. Use it for the inner sheen of a card, pill, or tile; a straight `AddLine` inset by the radius stops short of the real edge.

In-app glass refracts the app's own backdrop. `AppSkin.Backdrop` records its gradient through `WallpaperBackdrop.RecordAppGround`, and the shell records the theme background for every other app, so a tab bar or sheet inside an app lifts the color behind it instead of darkening the bottom of the gradient into a black block.

**Content is a fill.** `Surfaces.Fill(ink, FillLevel)` (src/Aetherphone/Windows/Components/Primitives/Surfaces.cs) derives the `Primary`, `Secondary`, `Tertiary` and `Quaternary` fills from the screen's title ink, so one call is right in dark and light themes. `Button`, `RoundButton`, `SegmentStrip` and `SearchBar.Surface` all paint from it; never hand-pick a white-alpha fill for a control.

**Fields are a surface plus an input.** `GlassField` (src/Aetherphone/Windows/Components/Fields/GlassField.cs) is the input layer, despite its name: `GlassField.Text`, `GlassField.Search` (search glyph plus a clear button) and `GlassField.Title` draw a borderless ImGui input with a transparent frame and no background of their own. In content, put `SearchBar.Surface` (the filled 36 tall capsule on `Fill.Tertiary`) under it. `GlassField.Surface` paints a glass capsule and belongs to navigation-layer chrome only, such as the home screen's folder name field. Real example from src/Aetherphone/Apps/Settings/SettingsForm.cs:

```csharp
var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
SearchBar.Surface(ImGui.GetWindowDrawList(), field, ControlInk.From(theme));
var changed = GlassField.Text(field, imguiId, hint, ref text, theme, scale, maxLength, false, flags);
```

## Input: UiInteract

Because widgets are painted onto draw lists instead of created as ImGui items, ImGui does not know they exist. `UiInteract` (src/Aetherphone/Windows/Components/Primitives/UiInteract.cs) is the hit-testing layer that replaces `ImGui.Button` and friends.

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

`DropdownMenu` (src/Aetherphone/Windows/Components/Sheets/DropdownMenu.cs) is the anchored context menu. Lifecycle, as used in src/Aetherphone/Apps/Jobs/JobsApp.cs:

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

For confirms, do not draw `ConfirmDialog` yourself. Apps receive a `ConfirmService` (src/Aetherphone/Core/Confirm/ConfirmService.cs) and call `confirm.Ask(new ConfirmRequest { ... })` or `confirm.Alert(...)`; the shell-level `ConfirmOverlay` dims the screen, animates the card in, and routes the buttons back to your `Confirm`/`Cancel` callbacks. A request is stamped with the host that raised it: `ConfirmHosts.Current` is the phone unless a window wraps its own draw in `ConfirmHosts.Enter(host)`, and each `ConfirmOverlay` only shows requests for its own host, so a dialog raised inside a Linkpearl pop-out appears over that window instead of vanishing with a minimized phone. A window that hosts confirms owns them for their lifetime: call `confirm.CancelHost(host)` when it collapses, unbinds, or is suppressed, or the dialog is stranded where nobody can answer it. Set `Sheet = true` on the request for a destructive confirm and the overlay presents it as an iOS bottom action sheet (title and message in a muted header band, the confirm label as the action row) instead of the centered alert; leave it unset for money, consent, and irreversible-account flows. The sheet path only applies to a request with no `Sections` and no `Acknowledge`; either of those still renders as the centered alert (the open-link confirmation carries `Sections`, for example). `ConfirmDialog` (src/Aetherphone/Windows/Components/Sheets/ConfirmDialog.cs) is the presentational layer for both. Its `DrawPillButton` wraps `Button.Surface` with a `cardScale` parameter for buttons on scaled overlay cards; anywhere else, use `Button.Draw`.

`ActionSheet` (src/Aetherphone/Windows/Components/Sheets/ActionSheet.cs) is the multi-action bottom sheet for post and row overflow menus. It and `ShareSheet` are built on the shared `Sheet` presenter (src/Aetherphone/Windows/Components/Sheets/Sheet.cs), which owns the veil (`SheetMetrics.AppVeil` inside apps, `HomeVeil` over home), the themed glass panel at the screen radius, the 36 by 5 grabber, the medium (50 percent) and large (92 percent) detents with drag between them, the drag past the bottom to dismiss, and the tap-outside dismissal with its `openedFrame` guard. Hosts call `sheet.Begin(drawList, screen, theme, detents, veil)`, draw into `frame.Content` (clipped to the panel), then `sheet.End(in frame)`; `SheetDetents.Fitted(height)` makes a non-resizable sheet. `ActionSheet` usage: per-app instance, `Gate()` early in the frame, `Open()`/`Close()`, `Draw(screen, style, items, cancelLabel, keepOpen, title)` returns the picked index. Build the style with `ActionSheetStyle.From(theme)` or `From(ui)` rather than hand-picking colors; Chirper remains the reference host (sheet picks the action, `ConfirmService` still owns anything irreversible, a toast reports the result). For transient feedback anywhere, `ShellToast.Show(text)` raises the shell-level bottom pill; it renders in whichever host (phone screen, Linkpearl popout, minimized phone) the pointer was in when it was raised. Per-app `ScreenToast` instances stay for app-styled toasts.

Web links that other users supplied (post bodies, chat bubbles, venue and ad buttons, chat-log URLs) go through `UrlActions.AskThenOpen` (src/Aetherphone/Windows/UrlActions.cs), which shows the open-link confirmation with a host-first destination chip before calling `OpenInBrowser`. Both entry points normalize a scheme-less link first (a bare `www.` host becomes `https://`), because link scanners match `www.` too and `Process.Start` only accepts an absolute http or https URL. `LinkText` already does this for clickable URLs inside text, so anything drawn through `LinkText` or `ChatTranscript` gets the gate for free. `PhoneServices.Build` binds the single `ConfirmService` with `UrlActions.Configure`. Reserve a direct `OpenInBrowser` for first-party destinations (Patreon, Discord, Lodestone, sign-in verification pages).

## Scrolling

### DragScrollHost

`DragScrollHost` (src/Aetherphone/Windows/Components/Layout/DragScrollHost.cs) gives child windows phone-style kinetic scrolling: press and drag anywhere to scroll, release to coast, mouse wheel still works. Call `DragScrollHost.Begin(key)` at the top of a scrollable child region (key from `ImGui.GetID`), and pass your window flags through `DragScrollHost.ScrollFlags` so the native scrollbar is hidden while drag scrolling is enabled. While a drag is in progress it calls `UiInteract.BlockThisFrame()` and cancels pending taps, so rows do not fire when the user was scrolling.

Most apps never call it directly. `AppSurface.Begin(area)` (src/Aetherphone/Windows/Components/Layout/AppSurface.cs) wraps the standard app body: a padded child window with `DragScrollHost` attached, returning a scope with `Pull` (overscroll distance for `PullToRefresh`), `Dragging`, and `JumpToTop()`.

### StableContentWidth

Drag scrolling is only active while the phone window position is locked (`PhoneWindow.PreDraw` sets `DragScrollHost.Enabled` from `Configuration.LockPosition`); with the window unlocked, dragging would move the window, so the native ImGui scrollbar comes back. With a native scrollbar a feedback loop becomes possible: content sized to the full available width forces a scrollbar, the scrollbar shrinks the available width, the content no longer overflows, the scrollbar disappears, and the layout shakes every frame. `ScrollLayout.StableContentWidth()` (src/Aetherphone/Windows/Components/Layout/ScrollLayout.cs) breaks the loop by reserving the scrollbar width whenever the scrollbar is not yet showing. Use it instead of `ImGui.GetContentRegionAvail().X` whenever you size rows or cards inside a scrollable region.

### FeedVirtualizer

`FeedVirtualizer` (src/Aetherphone/Windows/Components/Layout/FeedVirtualizer.cs) keeps long feeds cheap by caching each row's measured height and skipping the draw when the row is offscreen. Pattern from src/Aetherphone/Apps/Aethergram/AethergramApp.cs:

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

For paged loading, `InfiniteScroll.ReachedBottom()` (src/Aetherphone/Windows/Components/Layout/InfiniteScroll.cs) reports when the scroll position is near the end so you can fetch the next page, and `InfiniteScroll.DrawLoadingRow` draws the three-dot loading indicator; see [Messaging and chat](messaging-and-chat.md) for the chat-side pagination contract.

## Common widgets

### Toggle

`Toggle.Draw(id, bounds, value, theme)` (src/Aetherphone/Windows/Components/Fields/Toggle.cs) draws the animated switch and returns the new value, not a "was clicked" flag. Assign the result; comparing it to the old value tells you whether it changed. Pattern from src/Aetherphone/Apps/Clock/ClockApp.Alarms.cs:

```csharp
var toggleSize = new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale;
var toggleMin = new Vector2(right - toggleSize.X, row.Center.Y - toggleSize.Y * 0.5f);
var toggleRect = new Rect(toggleMin, toggleMin + toggleSize);
var next = Toggle.Draw(alarmToggleIds[orderIndex], toggleRect, alarm.Enabled, theme);
if (next != alarm.Enabled)
{
    SetAlarmEnabled(alarm, next);
}
```

The ids in `alarmToggleIds` are built once, when the alarm list changes (`string.Concat("clock.alarm.", alarm.Id.ToString("N"))`). An interpolated id such as `$"alarm.{alarm.Id}"` would allocate a new string every frame.

### TabBar

`TabBar` (src/Aetherphone/Windows/Components/Layout/TabBar.cs) is the floating glass bottom tab bar: a themed glass capsule, a sliding accent highlight behind the active tab, optional badges, and an optional trailing action circle (`TabBarAction`). It is stateful (hover, press, and highlight springs), so keep one instance per bar, and keep the `TabItem` array as a field. Wrap the tab bodies in `TabBar.ReserveContent(scale)`, which makes every `AppSurface` opened inside reserve the bar's height at the bottom so content scrolls clear of it, then draw the bar after the body. Pattern from src/Aetherphone/Apps/Clock/ClockApp.cs:

```csharp
private readonly TabBar tabBar = new();
private readonly TabItem[] tabItems = new TabItem[TabCount];

using (TabBar.ReserveContent(scale))
{
    DrawActiveTab(context);
}

tabItems[(int)ClockTab.Alarms] = new TabItem(Loc.T(L.Clock.TabAlarms), PhoneIcons.Bell, PhoneIcons.BellFilled);
var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
if (result.Tapped >= 0 && result.Tapped != (int)activeTab)
{
    SelectTab((ClockTab)result.Tapped);
}
```

Fill every slot of `tabItems` the same way before calling `Draw` (the sample shows one). `Draw` returns a `TabBarResult`: `Tapped` is the tapped tab index or -1, and `ActionTapped` reports a tap on the action circle.

### ChipRail

`ChipRail` (src/Aetherphone/Windows/Components/Layout/ChipRail.cs) is the one way to show a row of filter chips: a single horizontal row that clips and pans by dragging. Chips never wrap into a second line; if they overflow, the rail shows round paging arrows at the clipped edge (a tap springs the rail one page along) and the user can also drag it sideways. It is stateful (pan offset), so keep one instance per rail:

```csharp
private readonly ChipRail filterRail = new();

var tapped = filterRail.Draw(ui, labels, active);
if (tapped >= 0)
{
    selectedFilter = tapped;
}
```

A tap only registers if the pointer traveled less than the drag slop, so panning does not select chips. Pass `centered: true` to the `Rect` overload to center the chips inside the row when they all fit; an overflowing rail still starts at the left edge and pans. Chips are 30 units tall; pass `chipHeight` (for example `Metrics.Size.Pill`) for a touch-sized rail of action presets, with a `labelPadding` of at least half that height so the round ends never clip a label (`ChipRail.LabelRoom`).

### Other frequently used widgets

| Widget | One-liner |
| --- | --- |
| `Button.Draw(rect, label, ink, style, role)` | The one labelled button: Prominent, Tinted, Gray or Plain capsule; sizes `ButtonSize.Small` 28, `Regular` 34 and `Large` 44 (`Button.Height`, `Button.WidthFor`); `ButtonRole.Destructive` swaps in the danger colour; hover lift and press sink come from `MotionButton`; `ui.Ink` or `ControlInk.From(theme)` supplies the colours (src/Aetherphone/Windows/Components/Fields/Button.cs) |
| `RoundButton.Icon(drawList, center, radius, glyph, ink, style)` | Content-layer circular icon button in the same styles, with an optional hover label (src/Aetherphone/Windows/Components/Fields/RoundButton.cs) |
| `SearchBar.Surface` / `SearchBar.Draw` | Filled 36 tall capsule on `Fill.Tertiary`; `Draw` paints it and runs `GlassField.Search` on top, and `Surface` goes under `GlassField.Text` or `GlassField.Search` when you lay the field out yourself (src/Aetherphone/Windows/Components/Fields/SearchBar.cs) |
| `Surfaces.Fill(ink, FillLevel)` | Content fill ladder derived from the title ink, right in dark and light (src/Aetherphone/Windows/Components/Primitives/Surfaces.cs) |
| `SegmentStrip.Draw(id, row, options, selected, theme)` | iOS segmented control on `Fill.Tertiary` with a sliding thumb; returns the selected index, so assign it back like a `Toggle`; overloads take an `AppPalette` or explicit colours (src/Aetherphone/Windows/Components/Layout/SegmentStrip.cs) |
| `CardSectionHeader.Draw(drawList, origin, width, title, ink)` / `Flow(title, ink)` | 40 tall `Title3` heading over a block of cards; `Draw` returns its height, `Flow` lays it out at the ImGui cursor (src/Aetherphone/Windows/Components/Layout/CardSectionHeader.cs) |
| `HoverTooltip.Show(rect, label, side)` | Hover label for an icon-only control; queued and drawn on the foreground list once per frame by the host's `HoverTooltip.Flush()`; `RoundButton.Icon`, `HoverButton.Circle` and `GlassCircle` call it for you (src/Aetherphone/Windows/Components/Fields/HoverTooltip.cs) |
| `Skeleton.Rows` / `Skeleton.Feed` | Pulsing placeholder shapes in the layout's place while content loads: avatar-and-two-lines rows, or post cards (src/Aetherphone/Windows/Components/Primitives/Skeleton.cs); `LoadingPulse` (Primitives/LoadingPulse.cs) holds the spinner, caption and dots |
| `EmptyState.Draw(body, ui, glyph, title, hint)` | Centered icon, title, and wrapped hint for empty lists; takes a `PhoneIcons` glyph or a `FontAwesomeIcon` (src/Aetherphone/Windows/Components/Fields/EmptyState.cs) |
| `AvatarView.Draw` / `AvatarView.DrawRemote` | Circular avatar with monogram fallback, loading pulse, and fade-in (src/Aetherphone/Windows/Components/Media/AvatarView.cs) |
| `SoftWrapField.Multiline(id, ref value, maxLength, size, wrapWidth)` | Multiline composer input; wraps visually without inserting real newlines, supports `MentionAutocomplete` (src/Aetherphone/Windows/Components/Primitives/SoftWrapField.cs) |
| `SearchField.Draw` / `SearchField.DrawSubmit` | Pill search input with search icon; `Draw` adds a clear button, `DrawSubmit` returns true on Enter (src/Aetherphone/Windows/Components/Fields/SearchField.cs) |
| `Elevation.Card` / `Elevation.Floating` | Layered soft drop shadows behind cards and floating surfaces (src/Aetherphone/Windows/Components/Primitives/Elevation.cs) |
| `AppHeader.Draw(context, title, onBack)` | Standard inline app title bar with optional back button (src/Aetherphone/Windows/Components/Layout/AppHeader.cs) |
| `AppHeader.BeginLargeTitle` / `EndLargeTitle` | Collapsing large-title navigation bar: `Begin` returns a `NavBarFrame` whose `Body` the screen passes to `AppSurface.Begin`; the surface scrolls under the bar and `End` (called after the body, with a style from `NavBarStyle.From(theme)` or `From(ui)`) draws the 82 unit expanded bar collapsing over 60 units of scroll into the 44 unit glass inline bar, the back control with the previous screen's title, and up to two glass circle buttons (`NavBarButton`), returning the pressed button index (src/Aetherphone/Windows/Components/Layout/AppHeader.cs, metrics in Layout/NavBar.cs) |
| `GroupCard.Begin(theme, rowCount)` + `NextRow()` + `End()` | The inset card that every list of rows sits in, hairlines drawn for you; `End()` moves the ImGui cursor past the card (src/Aetherphone/Windows/Components/Layout/GroupCard.cs) |
| `SettingsRow.Link` / `AppLink` / `Disclosure` / `Bool` / `Switch` / `Info` / `Selectable` / `Action` | The row vocabulary for a GroupCard; `Switch` is the icon-tile row with an inline toggle, `Bool` the plain one, both taking an optional `hint` that becomes a question-mark icon (src/Aetherphone/Windows/Components/Fields/SettingsRow.cs) |
| `SettingsSection.Header(title, theme, hint)` | Uppercase section label, with an optional hint icon for a whole section (src/Aetherphone/Windows/Components/Fields/SettingsSection.cs) |
| `HoverButton.Circle` | Round FontAwesome icon button on the fill ladder with `MotionButton` lift, a rim light, and an optional hover label; new content buttons use `RoundButton.Icon` (src/Aetherphone/Windows/Components/Fields/HoverButton.cs) |
| `PopoverSurface.Draw` | The floating card background menus sit on (src/Aetherphone/Windows/Components/Sheets/PopoverSurface.cs) |
| `ActionSheet` | Fitted glass `Sheet` of labeled rows, danger rows in red, with a Cancel row under a hairline in the same panel; `Gate()` each frame, `Draw` returns the picked index (src/Aetherphone/Windows/Components/Sheets/ActionSheet.cs) |
| `ScreenToast` | Bottom-centered glass pill that pops in and dismisses itself after 1.7 seconds; `Show(text)` then `Draw(screen, style)` every frame (src/Aetherphone/Windows/Components/Notify/ScreenToast.cs) |
| `PullToRefresh` | Overscroll spinner fed by `AppSurface` `Pull` (src/Aetherphone/Windows/Components/Layout/PullToRefresh.cs) |
| `Marquee.DrawCenteredAuto` | Auto-scrolls a label that is too wide for its slot (src/Aetherphone/Windows/Components/Primitives/Marquee.cs) |

`SoftWrapField.Multiline` is the composer field for anything the user types more than one line into (posts, notes, feedback). It keeps one `SoftWrapEditor` per id, and the editor's `SoftWrapBuffer` holds a display string with soft line breaks next to the logical string without them (both in src/Aetherphone/Windows/Components/Primitives/), so stored text never contains layout-only newlines. `SoftWrap` holds the shared wrap and byte-index helpers.

### Social components

The `Social` folder (src/Aetherphone/Windows/Components/Social/) is the chrome shared by Chirper, Aethergram, Velvet and the other social surfaces. `SocialInk` derives the whole social ink set (accent link, deep and wash, faint ink, glass, chips, button fills) from any `AppPalette`, and the rest of the folder draws with it: `SocialChrome` (screen headers, top bar icon buttons with count badges, inline stats, section labels), `UnderlineTabs`, `SocialUserRow`, `FeedFilterSheet`, `DoubleTapLike` (the double-tap heart on a photo), `CaughtUpDivider`, and `GeoScopeScreen` (the world, data center, and region scope picker). The "Which widget do I reach for" table below routes to the ones you will call most.

## Emoji in text

Emoji are not font glyphs. Messages carry shortcodes like `:sparkles:`, and rendering resolves them to PNG images:

- `EmojiCatalog` (src/Aetherphone/Core/Emoji/EmojiCatalog.cs) loads catalog.json from the plugin's Emoji asset folder and maps shortcodes to codepoint-named image files.
- `EmojiScanner.Collect` (src/Aetherphone/Core/Emoji/EmojiScanner.cs) finds `:shortcode:` spans in a string.
- `EmojiRender.Draw` (src/Aetherphone/Windows/Components/Media/EmojiRender.cs) draws one emoji image at text position, with `Advance` and `LineHeight` for layout; `EmojiImages` (src/Aetherphone/Core/Emoji/EmojiImages.cs) loads the textures.

You rarely call these directly. `RichText.Build` (src/Aetherphone/Windows/Components/Primitives/RichText.cs) lays out a paragraph into plain, link, mention, and emoji runs, and `RichText.Draw` paints them; chat and social surfaces go through it. The images themselves are Twemoji assets fetched and cataloged by tools/emoji-generator; see [Assets and media](assets-and-media.md).

## Motion

Animated components (the `Toggle` knob, `ConfirmOverlay` reveal) use `Spring` (src/Aetherphone/Core/Animation/Spring.cs), a critically damped smoother that clamps on target crossing, so motion settles without bouncing. Follow that: no overshoot or bounce in phone UI. Smooth times and press scales come from `Motion` (src/Aetherphone/Core/Animation/Motion.cs): `PressIn`, `Release`, `HoverLift`, `PageSettle`, `Sheet`, `Island`, `SwitcherReveal`, `TabBar`, `Appear`, plus `PressScaleControl`, `PressScaleCard`, `HoverLiftIcon` and `HoverLiftCard`. Draw with the raw spring value; do not layer an `Easing` curve over it. `Easing.Lerp`, `Clamp01` and `Segment` are linear helpers and stay; the curves belong to games and the boot sequence.

A spring started from rest spends its first frames barely moving, which reads as input lag. For anything the user just triggered (a screen push, an app launch), start it with `spring.Launch(value, TransitionTiming.LaunchVelocity(smoothTime) * distance)`, where `distance` is how far the spring travels (1 for a 0 to 1 spring; the `Sheet` presenter multiplies by the detent height). The kick is half the spring's natural frequency, so the motion begins immediately and decelerates into place without ever overshooting (see `SpringLaunchTests`). Step transitions with a delta clamped to `TransitionTiming.MotionFrameSeconds`, not `MaxFrameSeconds`, so a dropped frame slows the motion instead of skipping a third of it. `NavigationStack` and `ViewRouter` follow this; the shared `Sheet` presenter and `ConfirmOverlay` still clamp to `MaxFrameSeconds`, so do not copy their step into a new transition.

Tappable surfaces never hand-roll a press scale. `PressFx.Scale(key, pressed)` (src/Aetherphone/Windows/Components/Primitives/PressFx.cs) gives a custom surface the shared press sink, and `MotionButton.Animate(rect, key, hovered, pressed)` (src/Aetherphone/Windows/Components/Primitives/MotionButton.cs) returns the full button pose (the face rect after hover lift and press sink, plus the hover and press amounts) that `Button`, `RoundButton` and `HoverButton` draw from. Both step their springs with the `Motion` smooth times.

To move, scale or fade a whole screen, do not paint it at a shifted rect or a smaller size: paint it at rest inside a `ScreenLayer` stage and call `Transform` with a `LayerTransform` once the stage has ended (src/Aetherphone/Core/Animation/ScreenLayer.cs). The transform rewrites the vertices and clip rects of the stage window and every child it begun this frame, so the screen keeps its ImGui window identity (scroll, focus, state storage), its layout stays correct, and text scales as a bitmap instead of snapping between font sizes. Anything that must draw above a stage's own children (a dim, a veil, chrome) goes in a nested `ScreenLayer.BeginPassive` child, because a window's own draw list renders before its children. `SceneCompositor.DrawLayer` wraps this for the common translate case.

## Which widget do I reach for

| I need to... | Reach for |
| --- | --- |
| Draw any text | `Typography` + a `TextStyles` entry |
| Show a paragraph that must not overflow | `Typography.DrawWrappedLeft` / `DrawWrappedCentered` |
| Keep a one-line label inside a slot | `Typography.FitText` or `Marquee.DrawCenteredAuto` |
| Build a scrollable app body | `AppSurface.Begin(area)` |
| Give a screen a large title that collapses on scroll | `AppHeader.BeginLargeTitle` + `AppSurface.Begin(frame.Body)` + `AppHeader.EndLargeTitle` |
| Give an app bottom tabs | `TabBar.Draw(area, ui, items, active)`, with the tab bodies inside `TabBar.ReserveContent` |
| Switch between a few views in place | `SegmentStrip.Draw` (returns the selected index; assign it back) |
| Build a feed or conversation list | `AppSurface.BeginEdgeToEdge` + `FeedCell.Begin`/`End` |
| Build a settings or detail row list | `GroupCard.Begin` + `NextRow` + a row painter + `End` |
| Title a section above cells | `ListSection.Header` (or `ListSection.Label` from an AppSkin) |
| Title a block of cards | `CardSectionHeader.Draw` (or `CardSectionHeader.Flow` at the cursor) |
| Show content that is still loading | `Skeleton.Rows` or `Skeleton.Feed` |
| Paint navigation-layer chrome (bars, sheets, menus, toasts) | `Material.ThemedGlass` or `Material.LiquidGlass`; never on content, never on glass |
| Present a custom bottom sheet | `Sheet` (`Begin` with `SheetDetents.Standard` or `Fitted`, draw into `frame.Content`, then `End`) |
| Inset a card inside an edge-to-edge surface | shift both edges by `FeedCell.PadX * scale` |
| Size rows inside a scroll region | `ScrollLayout.StableContentWidth()` |
| Render a long feed | `FeedVirtualizer` + `InfiniteScroll.ReachedBottom` |
| Draw a labelled button | `Button.Draw` (or the AppSkin pill helpers that forward to it) |
| Put a round icon button on a card | `RoundButton.Icon` (glass `GlassCircle` only in the navigation layer) |
| Label an icon-only control on hover | `HoverTooltip.Show(rect, label, side)` (built into `RoundButton.Icon`, `HoverButton.Circle` and `GlassCircle`) |
| Make a rect clickable | `UiInteract.HoverClick` (or `Hover` + `Click`) |
| Give a custom tappable surface a press sink | `PressFx.Scale` (or `MotionButton.Animate` for the full button pose) |
| Flip a boolean setting | `Toggle.Draw` |
| Offer filters in a row | `ChipRail` |
| Show "nothing here yet" | `EmptyState.Draw` |
| Confirm a destructive action | `ConfirmService.Ask` with `Sheet = true` (never draw `ConfirmDialog` yourself) |
| Confirm money, consent, or the irreversible | `ConfirmService.Ask` (alert presentation) or `Alert` |
| Offer post or row overflow actions | `ActionSheet` (style via `ActionSheetStyle.From`) |
| Head a social sub-screen | `SocialChrome.DrawScreenHeader` (glass back chip, left or centred title) |
| Put icon buttons in a social top bar | `SocialChrome.DrawHeaderIcon` + `HeaderSlot` (count badge built in) |
| Switch between two feeds or an icon tab row | `UnderlineTabs.Draw` / `UnderlineTabs.DrawIcons` |
| Draw a Follow, Edit profile or Send button | `Button.Draw` (Prominent, Tinted or Gray) |
| List people with a follow pill | `SocialUserRow.Draw` and fill the returned `Trailing` rect |
| Anchor a popover to a row and dismiss it on tap-outside | `ActionReveal<TPanel>` + `PopoverSurface.DrawGlass` |
| Format a like or follower count | `CountText.Compact` |
| Let someone filter a social feed | `FeedFilterSheet` (`Gate()` each frame, `Draw` returns the toggled index) |
| Like a photo on double tap | `DoubleTapLike` (`Tapped` detects it, `DrawBurst` paints the heart, `SwallowedTap` tells you to drop the second tap so it does not also count as a plain tap) |
| Report a transient result | `ShellToast.Show` |
| Open a link someone else posted | `UrlActions.AskThenOpen` (confirms the destination first) |
| Show a picker or context menu | `DropdownMenu` |
| Draw a person's picture | `AvatarView` |
| Take a single line of text | `SearchBar.Surface` + `GlassField.Text` |
| Take multiline text input | `SoftWrapField.Multiline` |
| Add a search box | `SearchField`, or `SearchBar.Draw` |
| Let someone pick a color | `ColorField.Draw` (shade square plus hue rail); it is Settings-local (src/Aetherphone/Apps/Settings/ColorField.cs), so promote it into the toolkit before a second app uses it |
| Add depth behind a card | `Elevation.Card` + `Squircle.Fill` |
| Space or round anything | `Metrics` tokens times `UiScale.Current` |

## Gotchas

- `Typography.Draw` and `Typography.DrawCentered` overloads without an `ImDrawListPtr` first parameter move the ImGui cursor (`SetCursorScreenPos` + `TextUnformatted`). In custom-painted layouts this shifts everything drawn after them. Pass the draw list explicitly.
- `Toggle.Draw` returns the new value, not a clicked flag. Writing `if (Toggle.Draw(...)) { ... }` treats "switch is on" as "switch was clicked" and fires every frame while on. `SegmentStrip.Draw` follows the same contract: it returns the selected index, not the tapped one.
- `GroupCard` is a plain struct, not a scope. `End()` is what moves the ImGui cursor past the card; skip it and the next item draws on top of the card.
- `HoverTooltip.Show` only queues a label; the host draws the queue with `HoverTooltip.Flush()` at the end of its frame. The phone shell and every popout window already call it, so a new top-level window must call it too or its hover labels never appear.
- `GlassField.Text`, `GlassField.Search` and `GlassField.Title` draw no background. Put `SearchBar.Surface` under them in content (or `GlassField.Surface` in navigation-layer chrome); a field never renders bare.
- `Hero`, `WidgetDisplay` and `WidgetDisplayCompact` render at the 1.90 bucket through the regular `Typography` calls. When the true size matters, measure and draw with `MeasureExact` and `DrawCenteredExact`.
- A popup without the `openedFrame` guard closes on the same click that opened it, because the opening click lands outside the popup rect. Compare `ImGui.GetFrameCount()` to the frame the popup opened before honoring outside-click dismissal, as `DropdownMenu.Draw` and `ConfirmOverlay.Draw` do.
- `UiInteract.Click` claims: the last `Click` call of the press frame wins. A parent row hit-tested after its child button steals the child's tap unless you gate the parent with `hovered && !overChildRect` (see AccountPage.cs above).
- Using `ImGui.GetContentRegionAvail().X` to size content in a native-scrollbar region causes the scrollbar show/hide feedback loop (layout shakes every frame). Use `ScrollLayout.StableContentWidth()`.
- `Metrics` values are unscaled design units. Forgetting `UiScale.Current` makes layouts wrong at any UI scale other than 100 percent and at any phone size other than 360 wide.
- `FeedVirtualizer.Skip`/`Record` cache row heights per id and revision. If a row can change height (comments appear, text expands), change its revision or the feed will draw with stale heights.
- Text drawn without `Typography` skips `Plugin.Fonts.NoticeText`, so characters outside the base glyph ranges (CJK in particular) may render as placeholder boxes until something else notices them.
- `DropdownMenu` rows and other blocked-frame overlays must hit-test with `UiInteract.HoverWindowOnly`, because `Gate()` makes plain `Hover` return false while they are open.
- That trap extends to shared widgets you draw *inside* a gated overlay: `AppSkin.PillButton` and friends hit-test with `UiInteract.Hover`, so a button placed in a sheet whose owner called `Gate()` is dead on arrival. Pass `overlay: true` to `AppSkin.PillButton` (it switches to `HoverWindowOnly`) or hand-roll the hit test, as `CashierDrawer` does.
- Measuring wrapped text with one helper and drawing it with another silently mis-sizes the block: `Typography.MeasureWrapped` uses ImGui's own line height, while `DrawWrappedCentered(drawList, text, style, color, topCenter, maxWidth)` multiplies by a 1.25 line spacing. The exact pairs are `MeasureWrappedBlock` with the center-anchored `DrawWrappedCentered(drawList, center, ...)` overload, or `DrawWrappedCentered(topCenter, ...)`, which returns the height it drew.

## Related docs

- [Getting started](getting-started.md): build, dev loop, Dalamud and ImGui primer
- [Architecture](architecture.md): plugin boot, frame loop, shell, window
- [App framework](app-framework.md): the IPhoneApp contract, skins, navigation
- [Creating an app](creating-an-app.md): tutorial that builds a new phone app from zero using these components
- [Messaging and chat](messaging-and-chat.md): the shared chat layer built on these components
- [Assets and media](assets-and-media.md): fonts, emoji, icons, and the generator tools
- [Accent colors](design-accents.md): the accent ring, the white-glyph rule, and the palettes `AppSkin` draws with
- [Localization](localization.md): where user-facing strings come from
- [Conventions](conventions.md): the rulebook for code, copy, and commits; its UI section is the checklist form of this doc
