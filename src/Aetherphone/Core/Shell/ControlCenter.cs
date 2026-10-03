using Aetherphone.Core.Animation;
using Aetherphone.Core.ControlCenter;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell;

internal sealed class ControlCenter
{
    private const float OpenFraction = 0.55f;
    private const float CommitFraction = 0.30f;
    private const float FlingVelocity = 900f;
    private const float TapSlop = 6f;
    private const float TopBandHeight = 44f;
    private const float DismissBandHeight = 48f;
    private const float LongPressSeconds = 0.40f;
    private const float DragThreshold = 7f;
    private const float PanelVeil = 0.55f;
    private const float DetailVeil = 0.35f;
    private const float HeaderButtonRadius = 18f;
    private const float DragGrow = 0.06f;
    private const float DetailSmallFraction = 0.50f;
    private const float DetailLargeFraction = 0.78f;
    private const float DetailTallWidthFraction = 0.30f;
    private const float DetailTallHeightFraction = 0.95f;
    private const float DetailWideHeightFraction = 0.50f;
    private const float DetailLiveThreshold = 0.9f;
    private const float MinimumNotificationHeight = 80f;
    private const string DoneId = "cc.done";
    private const string CustomizeId = "cc.customize";
    private const string AddId = "cc.add";

    private sealed class SlotPose
    {
        public Spring X;
        public Spring Y;
        public Spring W;
        public Spring H;
        public bool Initialized;
        public Rect Current;
    }

    private readonly ThemeProvider themes;
    private readonly PlaybackHub playback;
    private readonly INavigator navigation;
    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly NotificationCenter notificationCenter;
    private readonly ControlRegistry registry;
    private readonly ControlLayoutService layout;
    private readonly ControlGallery gallery;
    private readonly DragTracker drag = new();
    private readonly Dictionary<string, SlotPose> poses = new();
    private Spring offset;
    private Spring lift;
    private Spring expand;
    private float target;
    private bool open;
    private bool editing;
    private float editClock;
    private ControlSlot? draggingSlot;
    private Vector2 dragGrab;
    private ControlSlot? pressSlot;
    private Vector2 pressOrigin;
    private float pressTime;
    private ControlSlot? expandedSlot;
    private bool collapsing;
    private int expandedFrame;
    private ControlMetrics metrics;

    public ControlCenter(Configuration configuration, ThemeProvider themes, PlaybackHub playback, CallHub calls,
        INavigator navigation, NotificationService notifications, NotificationRouter router,
        Coins.CoinStore coins, Aethernet.AethernetSession session, SystemMedia.PcMediaSource pcMedia)
    {
        this.themes = themes;
        this.playback = playback;
        this.navigation = navigation;
        this.notifications = notifications;
        this.router = router;
        notificationCenter = new NotificationCenter(notifications, router, Dismiss);
        registry = new ControlRegistry(configuration, themes, playback, calls, navigation, Dismiss, coins, session,
            pcMedia);
        layout = new ControlLayoutService(registry, configuration);
        gallery = new ControlGallery(layout);
    }

    public bool IsActive => open || offset.Value > 0.01f;
    public bool CapturesPointer => IsActive;
    public Rect NotificationArea { get; private set; }

    public void Draw(Rect screen, PhoneTheme theme, float delta, bool gesturesEnabled, bool inputEnabled = true)
    {
        var busy = editing || draggingSlot is not null || gallery.Active || pressSlot is not null ||
                   expandedSlot is not null;
        HandleGesture(screen, delta, gesturesEnabled, !busy);
        editClock += delta;
        var eased = offset.Value;
        if (eased <= 0.001f)
        {
            editing = false;
            Collapse(true);
            return;
        }

        if (UiInteract.HoverWindowOnly(screen.Min, screen.Max, false))
        {
            UiInteract.ReportGestureSurface();
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        var height = screen.Height;
        var rounding = theme.ScreenRounding * scale;
        var panelTop = screen.Min.Y - (1f - eased) * height;
        var panel = new Rect(new Vector2(screen.Min.X, panelTop), new Vector2(screen.Max.X, panelTop + height));
        drawList.PushClipRect(screen.Min, screen.Max, true);
        Material.Veil(drawList, screen.Min, screen.Max, PanelVeil * eased, rounding);
        Material.FrostedGlass(drawList, panel.Min, panel.Max, rounding, scale, 1f);
        var opacity = Math.Clamp(eased * 1.7f, 0f, 1f);
        var interactive = open && !drag.Active && offset.Value > 0.96f && inputEnabled;
        DrawContents(drawList, screen, panel, theme, scale, delta, opacity, interactive);
        drawList.PopClipRect();
    }

    private void DrawContents(ImDrawListPtr drawList, Rect screen, Rect panel, PhoneTheme theme, float scale,
        float delta, float opacity, bool interactive)
    {
        var padding = Metrics.Space.Lg * scale;
        var left = screen.Min.X + padding;
        var right = screen.Max.X - padding;
        DrawGrabber(drawList, panel, scale, opacity);
        var headerCenterY = panel.Min.Y +
                            (Metrics.Space.Sm + Metrics.Size.GrabberHeight + Metrics.Space.Glass + HeaderButtonRadius) *
                            scale;
        var titleMaxWidth = MathF.Max(1f, HeaderButtonsLeft(right, scale) - Metrics.Space.Glass * scale - left);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, headerCenterY - titleHeight * 0.5f),
            Typography.FitText(Loc.T(L.ControlCenter.Title), titleMaxWidth, TextStyles.Title3),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.Title3);
        DrawHeaderButtons(drawList, theme, right, headerCenterY, scale, opacity, interactive);

        var gridTop = headerCenterY + (HeaderButtonRadius + Metrics.Space.Md) * scale;
        metrics = ControlMetrics.Compute(new Rect(new Vector2(left, gridTop), new Vector2(right, gridTop + 1f)),
            ControlLayoutService.Columns, scale);
        var slots = layout.Slots;
        var placements = layout.Placements;
        StepPoses(slots, placements, delta);
        StepExpansion(delta);
        var detailOpen = expandedSlot is not null;
        if (interactive && !detailOpen)
        {
            UpdateEditInput(screen, delta);
        }

        if (editing)
        {
            DrawEmptyCells(drawList, slots, placements, scale, opacity);
        }

        DrawSlots(drawList, theme, slots, scale, opacity, interactive && !detailOpen);

        var gridBottom = gridTop + metrics.HeightForRows(layout.RowsUsed);
        var contentBottom = screen.Max.Y - DismissBandHeight * scale;
        if (editing)
        {
            Typography.DrawWrappedCentered(drawList, Loc.T(L.ControlCenter.EditHint), TextStyles.Footnote,
                Palette.WithAlpha(theme.TextMuted, opacity * 0.9f),
                new Vector2(screen.Center.X, gridBottom + Metrics.Space.Lg * scale), right - left);
        }
        else
        {
            DrawNotificationSection(drawList, theme, left, right, gridBottom, contentBottom, scale, opacity,
                interactive && !detailOpen);
        }

        DrawDetail(drawList, theme, new Rect(new Vector2(left, gridTop), new Vector2(right, contentBottom)), screen,
            scale, opacity, interactive);
        var galleryRegion = new Rect(screen.Min, new Vector2(screen.Max.X, contentBottom));
        gallery.Draw(galleryRegion, theme, delta, scale, opacity);
    }

    private static void DrawGrabber(ImDrawListPtr drawList, Rect panel, float scale, float opacity)
    {
        var half = Metrics.Size.GrabberWidth * 0.5f * scale;
        var height = Metrics.Size.GrabberHeight * scale;
        var top = panel.Min.Y + Metrics.Space.Sm * scale;
        drawList.AddRectFilled(new Vector2(panel.Center.X - half, top),
            new Vector2(panel.Center.X + half, top + height),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.32f * opacity)), height * 0.5f);
    }

    private float HeaderButtonsLeft(float right, float scale)
    {
        var diameter = 2f * HeaderButtonRadius * scale;
        if (!editing)
        {
            return right - diameter;
        }

        return right - DoneWidth(scale) - Metrics.Space.Md * scale - diameter;
    }

    private static float DoneWidth(float scale) =>
        Typography.Measure(Loc.T(L.ControlCenter.Done), TextStyles.SubheadlineEmphasized).X +
        2f * Metrics.Space.Lg * scale;

    private void DrawHeaderButtons(ImDrawListPtr drawList, PhoneTheme theme, float right, float centerY, float scale,
        float opacity, bool interactive)
    {
        var radius = HeaderButtonRadius * scale;
        if (!editing)
        {
            if (ControlTile.Circle(drawList, CustomizeId, new Vector2(right - radius, centerY), radius,
                    FontAwesomeIcon.SlidersH, false, theme.Accent, theme, opacity,
                    interactive && expandedSlot is null, Loc.T(L.ControlCenter.Customize)))
            {
                EnterEdit();
            }

            return;
        }

        var doneWidth = DoneWidth(scale);
        var doneRect = new Rect(new Vector2(right - doneWidth, centerY - radius), new Vector2(right, centerY + radius));
        if (GlassPill(drawList, doneRect, Loc.T(L.ControlCenter.Done), theme, scale, opacity, interactive))
        {
            ExitEdit();
        }

        var addCenter = new Vector2(doneRect.Min.X - Metrics.Space.Md * scale - radius, centerY);
        if (ControlTile.Circle(drawList, AddId, addCenter, radius, FontAwesomeIcon.Plus, false, theme.Accent, theme,
                opacity, interactive, Loc.T(L.ControlCenter.AddControls)))
        {
            gallery.Open();
        }
    }

    private static bool GlassPill(ImDrawListPtr drawList, Rect rect, string text, PhoneTheme theme, float scale,
        float opacity, bool interactive)
    {
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Press(DoneId, pressed, PressFx.IconPressedScale);
        var half = rect.Size * (0.5f * press);
        Material.LiquidGlass(drawList, rect.Center - half, rect.Center + half, half.Y, scale, GlassTone.Light,
            WallpaperLegibility.Strength(theme), opacity);
        Typography.DrawCentered(drawList, rect.Center, text, ControlTile.Glyph(true, opacity),
            TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void StepPoses(IReadOnlyList<ControlSlot> slots, IReadOnlyList<GridCell> placements, float delta)
    {
        lift.Step(draggingSlot is not null ? 1f : 0f, Motion.HoverLift, delta);
        var gridOrigin = metrics.Grid.Min;
        var mouse = ImGui.GetMousePos();
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var pose = Pose(slot.Id);
            var cell = index < placements.Count ? placements[index] : default;
            var rest = metrics.SlotRect(cell, slot.ColumnSpan, slot.RowSpan);
            var targetRect = rest;
            if (ReferenceEquals(slot, draggingSlot))
            {
                var min = mouse - dragGrab;
                targetRect = new Rect(min, min + rest.Size);
            }

            var targetMin = targetRect.Min - gridOrigin;
            if (!pose.Initialized)
            {
                pose.X.SnapTo(targetMin.X);
                pose.Y.SnapTo(targetMin.Y);
                pose.W.SnapTo(targetRect.Width);
                pose.H.SnapTo(targetRect.Height);
                pose.Initialized = true;
            }

            if (ReferenceEquals(slot, draggingSlot))
            {
                pose.X.SnapTo(targetMin.X);
                pose.Y.SnapTo(targetMin.Y);
            }
            else
            {
                pose.X.Step(targetMin.X, Motion.Release, delta);
                pose.Y.Step(targetMin.Y, Motion.Release, delta);
            }

            pose.W.Step(targetRect.Width, Motion.Release, delta);
            pose.H.Step(targetRect.Height, Motion.Release, delta);
            var posedMin = gridOrigin + new Vector2(pose.X.Value, pose.Y.Value);
            pose.Current = new Rect(posedMin, posedMin + new Vector2(pose.W.Value, pose.H.Value));
        }
    }

    private void StepExpansion(float delta)
    {
        if (expandedSlot is null)
        {
            return;
        }

        expand.Step(collapsing ? 0f : 1f, Motion.Island, delta);
        if (collapsing && expand.Value < 0.01f)
        {
            expandedSlot = null;
            collapsing = false;
            expand.SnapTo(0f);
        }
    }

    private void DrawEmptyCells(ImDrawListPtr drawList, IReadOnlyList<ControlSlot> slots,
        IReadOnlyList<GridCell> placements, float scale, float opacity)
    {
        Span<bool> occupied = stackalloc bool[HomeGridSolver.MaxCells];
        occupied.Clear();
        var columns = ControlLayoutService.Columns;
        var maxRows = HomeGridSolver.MaxCells / columns;
        for (var index = 0; index < placements.Count && index < slots.Count; index++)
        {
            for (var rowOffset = 0; rowOffset < slots[index].RowSpan; rowOffset++)
            {
                for (var columnOffset = 0; columnOffset < slots[index].ColumnSpan; columnOffset++)
                {
                    var cellIndex = (placements[index].Row + rowOffset) * columns +
                                    placements[index].Column + columnOffset;
                    if (cellIndex >= 0 && cellIndex < occupied.Length)
                    {
                        occupied[cellIndex] = true;
                    }
                }
            }
        }

        var rows = Math.Min(layout.RowsUsed, maxRows);
        var radius = ControlTile.Radius(scale);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (occupied[row * columns + column])
                {
                    continue;
                }

                var cellRect = metrics.SlotRect(new GridCell(column, row), 1, 1);
                Squircle.Fill(drawList, cellRect.Min, cellRect.Max, radius,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.045f * opacity)));
                Squircle.Stroke(drawList, cellRect.Min, cellRect.Max, radius,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.11f * opacity)), Metrics.Stroke.Hairline * scale);
            }
        }
    }

    private void DrawSlots(ImDrawListPtr drawList, PhoneTheme theme, IReadOnlyList<ControlSlot> slots, float scale,
        float opacity, bool interactive)
    {
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var pose = Pose(slot.Id);
            var rect = pose.Current;
            var dragged = ReferenceEquals(slot, draggingSlot);
            if (editing && !dragged)
            {
                rect = rect.Translate(HomeInteractionController.JiggleOffset(slot.Id.GetHashCode(), editClock, scale));
            }

            if (dragged)
            {
                rect = Grow(rect, 1f + DragGrow * lift.Value);
                Elevation.Floating(drawList, rect.Min, rect.Max, ControlTile.Radius(scale), scale, lift.Value);
            }

            var moduleInteractive = interactive && !editing && !gallery.Active;
            var context = new ControlModuleContext(drawList, rect, theme, slot.Span, scale, opacity, moduleInteractive);
            slot.Module.Draw(context);
            if (editing)
            {
                DrawEditDecorations(drawList, slot, rect, scale, opacity);
            }
        }
    }

    private static void DrawEditDecorations(ImDrawListPtr drawList, ControlSlot slot, Rect rect, float scale,
        float opacity)
    {
        var badge = BadgeCenter(rect, scale);
        var badgeRadius = 10f * scale;
        EditBadge(drawList, badge, badgeRadius, opacity);
        drawList.AddCircle(badge, badgeRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f * opacity)), 20,
            1f * scale);
        drawList.AddLine(badge - new Vector2(4f * scale, 0f), badge + new Vector2(4f * scale, 0f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, opacity)), 2f * scale);
        if (slot.Module.Sizes.Count <= 1)
        {
            return;
        }

        var handle = HandleCenter(rect, scale);
        EditBadge(drawList, handle, badgeRadius, opacity);
        ProgressRing.CenterIcon(drawList, handle, FontAwesomeIcon.ExpandAlt, new Vector4(1f, 1f, 1f, opacity),
            9f * scale);
    }

    private static void EditBadge(ImDrawListPtr drawList, Vector2 center, float radius, float opacity) =>
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.16f, 0.16f, 0.18f, 0.95f * opacity)),
            20);

    private void DrawDetail(ImDrawListPtr drawList, PhoneTheme theme, Rect content, Rect screen, float scale,
        float opacity, bool interactive)
    {
        if (expandedSlot is null)
        {
            return;
        }

        var slot = expandedSlot;
        var progress = Math.Clamp(expand.Value, 0f, 1f);
        Material.Veil(drawList, screen.Min, screen.Max, DetailVeil * progress * opacity, theme.ScreenRounding * scale);
        var origin = Pose(slot.Id).Current;
        var destination = DetailRect(slot, content, scale);
        var card = new Rect(Vector2.Lerp(origin.Min, destination.Min, progress),
            Vector2.Lerp(origin.Max, destination.Max, progress));
        Elevation.Floating(drawList, card.Min, card.Max, ControlTile.Radius(scale), scale, progress * opacity);
        var live = interactive && !collapsing && progress > DetailLiveThreshold;
        var context = new ControlModuleContext(drawList, card, theme, slot.Span, scale, opacity, live, progress);
        slot.Module.Draw(context);
        if (live && ImGui.GetFrameCount() != expandedFrame && UiInteract.ClickedOutside(card.Min, card.Max, false))
        {
            Collapse(false);
        }
    }

    private static Rect DetailRect(ControlSlot slot, Rect content, float scale)
    {
        var width = content.Width;
        Vector2 size;
        switch (slot.Span)
        {
            case ControlSpan.Tall:
                size = new Vector2(width * DetailTallWidthFraction, width * DetailTallHeightFraction);
                break;
            case ControlSpan.Large:
                size = new Vector2(width * DetailLargeFraction, width * DetailLargeFraction);
                break;
            case ControlSpan.Wide:
            case ControlSpan.Bar:
                size = new Vector2(width, width * DetailWideHeightFraction);
                break;
            default:
                size = new Vector2(width * DetailSmallFraction, width * DetailSmallFraction);
                break;
        }

        var maxHeight = MathF.Max(1f, content.Height - 2f * Metrics.Space.Lg * scale);
        if (size.Y > maxHeight)
        {
            size *= maxHeight / size.Y;
        }

        var half = size * 0.5f;
        return new Rect(content.Center - half, content.Center + half);
    }

    private void UpdateEditInput(Rect screen, float delta)
    {
        var mouse = ImGui.GetMousePos();
        if (draggingSlot is not null)
        {
            UpdateDrag(mouse);
            return;
        }

        if (gallery.Active)
        {
            return;
        }

        var slots = layout.Slots;
        if (editing)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(screen.Min, screen.Max))
            {
                for (var index = 0; index < slots.Count; index++)
                {
                    var rect = Pose(slots[index].Id).Current;
                    if ((BadgeCenter(rect, metrics.Scale) - mouse).Length() <= 12f * metrics.Scale)
                    {
                        layout.Remove(slots[index]);
                        ControlTile.CancelPress();
                        return;
                    }

                    if (slots[index].Module.Sizes.Count > 1 &&
                        (HandleCenter(rect, metrics.Scale) - mouse).Length() <= 12f * metrics.Scale)
                    {
                        layout.Resize(slots[index]);
                        ControlTile.CancelPress();
                        return;
                    }
                }
            }

            BeginPress(mouse, slots, true, screen);
            return;
        }

        BeginPress(mouse, slots, false, screen);
        if (pressSlot is not null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressTime += delta;
            if ((mouse - pressOrigin).Length() < TapSlop * metrics.Scale && pressTime >= LongPressSeconds)
            {
                Expand(pressSlot);
            }
        }
    }

    private void BeginPress(Vector2 mouse, IReadOnlyList<ControlSlot> slots, bool armDrag, Rect screen)
    {
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(screen.Min, screen.Max))
        {
            pressSlot = SlotAt(mouse, slots);
            pressOrigin = mouse;
            pressTime = 0f;
            return;
        }

        if (pressSlot is null || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                pressSlot = null;
            }

            return;
        }

        if (armDrag && (mouse - pressOrigin).Length() > DragThreshold * metrics.Scale)
        {
            StartDrag(pressSlot, mouse);
        }
    }

    private void UpdateDrag(Vector2 mouse)
    {
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            layout.Persist();
            draggingSlot = null;
            pressSlot = null;
            return;
        }

        var dropIndex = ComputeDropIndex(mouse);
        layout.Reorder(draggingSlot!, dropIndex);
    }

    private int ComputeDropIndex(Vector2 mouse)
    {
        var slots = layout.Slots;
        var index = 0;
        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            if (ReferenceEquals(slots[slotIndex], draggingSlot))
            {
                continue;
            }

            var rect = Pose(slots[slotIndex].Id).Current;
            var center = rect.Center;
            var before = mouse.Y < center.Y - rect.Height * 0.3f ||
                         (MathF.Abs(mouse.Y - center.Y) <= rect.Height * 0.7f && mouse.X < center.X);
            if (before)
            {
                break;
            }

            index++;
        }

        return index;
    }

    private void StartDrag(ControlSlot slot, Vector2 mouse)
    {
        draggingSlot = slot;
        dragGrab = mouse - Pose(slot.Id).Current.Min;
        pressSlot = null;
    }

    private ControlSlot? SlotAt(Vector2 mouse, IReadOnlyList<ControlSlot> slots)
    {
        for (var index = 0; index < slots.Count; index++)
        {
            if (Pose(slots[index].Id).Current.Contains(mouse))
            {
                return slots[index];
            }
        }

        return null;
    }

    private void Expand(ControlSlot slot)
    {
        expandedSlot = slot;
        collapsing = false;
        expand.SnapTo(0f);
        expandedFrame = ImGui.GetFrameCount();
        pressSlot = null;
        ControlTile.CancelPress();
        UiInteract.CancelPendingTap();
    }

    private void Collapse(bool immediate)
    {
        if (expandedSlot is null)
        {
            return;
        }

        if (immediate)
        {
            expandedSlot = null;
            collapsing = false;
            expand.SnapTo(0f);
            return;
        }

        collapsing = true;
    }

    private void EnterEdit()
    {
        editing = true;
        editClock = 0f;
        Collapse(true);
    }

    private void ExitEdit()
    {
        editing = false;
        draggingSlot = null;
        pressSlot = null;
        gallery.Close();
        layout.Persist();
    }

    private SlotPose Pose(string id)
    {
        if (!poses.TryGetValue(id, out var pose))
        {
            pose = new SlotPose();
            poses[id] = pose;
        }

        return pose;
    }

    private static Vector2 BadgeCenter(Rect rect, float scale) => rect.Min + new Vector2(3f, 3f) * scale;

    private static Vector2 HandleCenter(Rect rect, float scale) => rect.Max - new Vector2(3f, 3f) * scale;

    private static Rect Grow(Rect rect, float factor)
    {
        var center = rect.Center;
        var half = rect.Size * 0.5f * factor;
        return new Rect(center - half, center + half);
    }

    private void DrawNotificationSection(ImDrawListPtr drawList, PhoneTheme theme, float left, float right,
        float contentBottom, float panelBottomLimit, float scale, float opacity, bool interactive)
    {
        var titleTop = contentBottom + Metrics.Space.Xl * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var panelTop = titleTop + titleHeight + Metrics.Space.Glass * scale;
        var panelBottom = panelBottomLimit - Metrics.Space.Sm * scale;
        if (panelBottom - panelTop < MinimumNotificationHeight * scale)
        {
            NotificationArea = default;
            return;
        }

        Typography.Draw(drawList, new Vector2(left, titleTop),
            Typography.FitText(Loc.T(L.ControlCenter.Notifications), right - left, TextStyles.Title3),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.Title3);
        var panel = new Rect(new Vector2(left, panelTop), new Vector2(right, panelBottom));
        ControlTile.Surface(drawList, panel, theme, opacity);
        var inner = panel.Inset(Metrics.Space.Md * scale);
        NotificationArea = inner;
        notificationCenter.DrawOverlay(drawList, inner, theme, opacity, interactive && !editing);
    }

    public void Open()
    {
        open = true;
        target = 1f;
        router.AcknowledgeAll();
        notifications.MarkAllRead();
        notificationCenter.Reset();
    }

    public void Dismiss()
    {
        open = false;
        target = 0f;
        editing = false;
        draggingSlot = null;
        pressSlot = null;
        Collapse(true);
        gallery.Close();
    }

    private void HandleGesture(Rect screen, float delta, bool gesturesEnabled, bool allowDismiss)
    {
        var scale = UiScale.Current;
        var height = screen.Height;
        var openDistance = height * OpenFraction;
        var fling = FlingVelocity * scale;
        drag.Track(delta);
        if (!open)
        {
            if (gesturesEnabled)
            {
                var topBand = new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + TopBandHeight * scale));
                if (!drag.Active && UiInteract.Hover(topBand.Min, topBand.Max))
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                drag.Begin(topBand);
                if (drag.Active)
                {
                    var fraction = Math.Clamp(drag.Delta.Y / openDistance, 0f, 1f);
                    offset.SnapTo(fraction);
                    target = fraction;
                }
            }

            if (drag.Released(out var totalDelta, out var velocity))
            {
                var tapped = MathF.Abs(totalDelta.X) < TapSlop * scale && MathF.Abs(totalDelta.Y) < TapSlop * scale;
                if (tapped || totalDelta.Y / openDistance > CommitFraction || velocity > fling)
                {
                    Open();
                }
                else
                {
                    target = 0f;
                }
            }
        }
        else if (allowDismiss)
        {
            var bottomZone = new Rect(new Vector2(screen.Min.X, screen.Max.Y - DismissBandHeight * scale), screen.Max);
            drag.Begin(bottomZone);
            if (drag.Active)
            {
                var fraction = Math.Clamp(1f + drag.Delta.Y / openDistance, 0f, 1f);
                offset.SnapTo(fraction);
                target = fraction;
            }

            if (drag.Released(out var totalDelta, out var velocity))
            {
                var tapped = MathF.Abs(totalDelta.Y) < TapSlop * scale;
                var dismiss = tapped || -totalDelta.Y / openDistance > CommitFraction || velocity < -fling;
                open = !dismiss;
                target = dismiss ? 0f : 1f;
                if (dismiss)
                {
                    editing = false;
                }
            }
        }

        if (!drag.Active)
        {
            offset.Step(target, Motion.SwitcherReveal, delta);
            if (offset.IsResting(target, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
            {
                offset.SnapTo(target);
            }
        }
    }
}
