using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.ControlCenter;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal sealed class ControlCenter
{
    private const float SmoothTime = 0.19f;
    private const float OpenFraction = 0.55f;
    private const float CommitFraction = 0.30f;
    private const float FlingVelocity = 900f;
    private const float TapSlop = 6f;
    private const float TopBandHeight = 44f;
    private const float DismissBandHeight = 48f;
    private const float LongPressSeconds = 0.40f;
    private const float DragThreshold = 7f;
    private const float ReflowSmoothTime = 0.16f;
    private const float LiftSmoothTime = 0.13f;
    private static readonly Vector4 NeutralTint = new(1f, 1f, 1f, 0.14f);

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
    private readonly NotificationCenter notificationCenter;
    private readonly ControlRegistry registry;
    private readonly ControlLayoutService layout;
    private readonly ControlGallery gallery;
    private readonly DragTracker drag = new();
    private readonly Dictionary<string, SlotPose> poses = new();
    private Spring offset;
    private Spring lift;
    private float target;
    private bool open;
    private bool editing;
    private float editClock;
    private ControlSlot? draggingSlot;
    private Vector2 dragGrab;
    private ControlSlot? pressSlot;
    private Vector2 pressOrigin;
    private float pressTime;
    private ControlMetrics metrics;

    public ControlCenter(ThemeProvider themes, PlaybackHub playback, CallHub calls, INavigator navigation,
        NotificationService notifications, NotificationRouter router)
    {
        this.themes = themes;
        this.playback = playback;
        this.navigation = navigation;
        this.notifications = notifications;
        notificationCenter = new NotificationCenter(notifications, router, Dismiss);
        registry = new ControlRegistry(themes, playback, calls, navigation, Dismiss);
        layout = new ControlLayoutService(registry);
        gallery = new ControlGallery(layout);
    }

    public bool IsActive => open || offset.Value > 0.01f;
    public bool CapturesPointer => IsActive;

    public void Draw(Rect screen, PhoneTheme theme, float delta, bool gesturesEnabled, bool inputEnabled = true)
    {
        var busy = editing || draggingSlot is not null || gallery.Active || pressSlot is not null;
        HandleGesture(screen, delta, gesturesEnabled, !busy);
        editClock += delta;
        var eased = offset.Value;
        if (eased <= 0.001f)
        {
            editing = false;
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetForegroundDrawList();
        var height = screen.Height;
        var rounding = theme.ScreenRounding * scale;
        var panelTop = screen.Min.Y - (1f - eased) * height;
        dl.PushClipRect(screen.Min, screen.Max, true);
        Material.Veil(dl, screen.Min, screen.Max, 0.68f * eased, rounding);
        Material.Frosted(dl, new Vector2(screen.Min.X, panelTop), new Vector2(screen.Max.X, panelTop + height),
            rounding, scale, 1f);
        var opacity = Math.Clamp(eased * 1.7f, 0f, 1f);
        var interactive = open && !drag.Active && offset.Value > 0.96f && inputEnabled;
        DrawContents(dl, screen, theme, panelTop, scale, delta, opacity, interactive);
        dl.PopClipRect();
    }

    private void DrawContents(ImDrawListPtr dl, Rect screen, PhoneTheme theme, float panelTop, float scale, float delta,
        float opacity, bool interactive)
    {
        var pad = 20f * scale;
        var left = screen.Min.X + pad;
        var right = screen.Max.X - pad;
        var grabberHalf = 20f * scale;
        var grabberY = panelTop + 13f * scale;
        dl.AddRectFilled(new Vector2(screen.Center.X - grabberHalf, grabberY - 2.5f * scale),
            new Vector2(screen.Center.X + grabberHalf, grabberY + 2.5f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.32f * opacity)), 2.5f * scale);

        var titleY = panelTop + 30f * scale;
        Typography.Draw(dl, new Vector2(left, titleY), Loc.T(L.ControlCenter.Title),
            Palette.WithAlpha(theme.TextStrong, opacity), 1.15f, FontWeight.Bold);
        DrawHeaderButtons(dl, theme, right, titleY + 8f * scale, scale, delta, opacity, interactive);

        var gridTop = panelTop + 62f * scale;
        metrics = ControlMetrics.Compute(new Rect(new Vector2(left, gridTop), new Vector2(right, gridTop + 1f)), 4,
            scale);
        var slots = layout.Slots;
        var placements = layout.Placements;
        StepPoses(slots, placements, delta);
        if (interactive)
        {
            UpdateEditInput(delta);
        }

        if (editing)
        {
            DrawEmptyCells(dl, slots, placements, scale, opacity);
        }

        DrawSlots(dl, theme, slots, scale, opacity, interactive);

        var gridBottom = gridTop + metrics.HeightForRows(layout.RowsUsed);
        if (editing)
        {
            Typography.DrawCentered(dl, new Vector2(screen.Center.X, gridBottom + 16f * scale),
                Loc.T(L.ControlCenter.EditHint), Palette.WithAlpha(theme.TextMuted, opacity * 0.9f), 0.72f);
        }
        else
        {
            DrawNotificationSection(dl, theme, left, right, gridBottom, screen, scale, opacity, interactive);
        }

        var galleryRegion = new Rect(new Vector2(screen.Min.X, screen.Min.Y),
            new Vector2(screen.Max.X, screen.Max.Y - DismissBandHeight * scale));
        gallery.Draw(galleryRegion, theme, delta, scale, opacity);
    }

    private void DrawHeaderButtons(ImDrawListPtr dl, PhoneTheme theme, float right, float centerY, float scale,
        float delta, float opacity, bool interactive)
    {
        var radius = 15f * scale;
        if (!editing)
        {
            var center = new Vector2(right - radius, centerY);
            if (HoverButton.Circle(dl, "cc.customize", center, radius, FontAwesomeIcon.SlidersH, NeutralTint,
                    theme.TextStrong, delta, opacity, interactive, Loc.T(L.ControlCenter.Customize)))
            {
                EnterEdit();
            }

            return;
        }

        var donePad = 14f * scale;
        var doneText = Loc.T(L.ControlCenter.Done);
        var doneWidth = Typography.Measure(doneText, 0.82f).X + 2f * donePad;
        var doneRect = new Rect(new Vector2(right - doneWidth, centerY - radius),
            new Vector2(right, centerY + radius));
        if (TextPill(dl, doneRect, doneText, theme.Accent, opacity, interactive))
        {
            ExitEdit();
        }

        var addCenter = new Vector2(doneRect.Min.X - 12f * scale - radius, centerY);
        if (HoverButton.Circle(dl, "cc.add", addCenter, radius, FontAwesomeIcon.Plus, theme.Accent,
                new Vector4(1f, 1f, 1f, 1f), delta, opacity, interactive, Loc.T(L.ControlCenter.AddControls)))
        {
            gallery.Open();
        }
    }

    private void StepPoses(IReadOnlyList<ControlSlot> slots, IReadOnlyList<GridCell> placements, float delta)
    {
        lift.Step(draggingSlot is not null ? 1f : 0f, LiftSmoothTime, delta);
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
                pose.W.Step(targetRect.Width, ReflowSmoothTime, delta);
                pose.H.Step(targetRect.Height, ReflowSmoothTime, delta);
            }
            else
            {
                pose.X.Step(targetMin.X, ReflowSmoothTime, delta);
                pose.Y.Step(targetMin.Y, ReflowSmoothTime, delta);
                pose.W.Step(targetRect.Width, ReflowSmoothTime, delta);
                pose.H.Step(targetRect.Height, ReflowSmoothTime, delta);
            }

            var posedMin = gridOrigin + new Vector2(pose.X.Value, pose.Y.Value);
            pose.Current = new Rect(posedMin, posedMin + new Vector2(pose.W.Value, pose.H.Value));
        }
    }

    private void DrawEmptyCells(ImDrawListPtr dl, IReadOnlyList<ControlSlot> slots,
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
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (occupied[row * columns + column])
                {
                    continue;
                }

                var cellRect = metrics.SlotRect(new GridCell(column, row), 1, 1);
                var radius = MathF.Min(cellRect.Width, cellRect.Height) * 0.30f;
                dl.AddRectFilled(cellRect.Min, cellRect.Max,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.045f * opacity)), radius);
                dl.AddRect(cellRect.Min, cellRect.Max,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.11f * opacity)), radius, ImDrawFlags.RoundCornersAll,
                    1f * scale);
            }
        }
    }

    private void DrawSlots(ImDrawListPtr dl, PhoneTheme theme, IReadOnlyList<ControlSlot> slots, float scale,
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
                var phase = editClock * 11f + index * 0.9f;
                rect = new Rect(rect.Min + new Vector2(MathF.Sin(phase), MathF.Cos(phase * 1.13f)) * 0.9f * scale,
                    rect.Max + new Vector2(MathF.Sin(phase), MathF.Cos(phase * 1.13f)) * 0.9f * scale);
            }

            if (dragged)
            {
                var grow = 1f + 0.06f * lift.Value;
                rect = Grow(rect, grow);
                Elevation.Floating(dl, rect.Min, rect.Max, MathF.Min(rect.Width, rect.Height) * 0.30f, scale,
                    lift.Value);
            }

            var moduleInteractive = interactive && !editing && !gallery.Active;
            var context = new ControlModuleContext(dl, rect, theme, slot.Span, scale, opacity, moduleInteractive);
            slot.Module.Draw(context);
            if (editing)
            {
                DrawEditDecorations(dl, slot, rect, theme, scale, opacity);
            }
        }
    }

    private void DrawEditDecorations(ImDrawListPtr dl, ControlSlot slot, Rect rect, PhoneTheme theme, float scale,
        float opacity)
    {
        var badge = BadgeCenter(rect, scale);
        var badgeRadius = 10f * scale;
        dl.AddCircleFilled(badge, badgeRadius, ImGui.GetColorU32(new Vector4(0.16f, 0.16f, 0.18f, 0.95f * opacity)), 20);
        dl.AddCircle(badge, badgeRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f * opacity)), 20, 1f * scale);
        dl.AddLine(badge - new Vector2(4f * scale, 0f), badge + new Vector2(4f * scale, 0f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, opacity)), 2f * scale);
        if (slot.Module.Sizes.Count <= 1)
        {
            return;
        }

        var handle = HandleCenter(rect, scale);
        var handleRadius = 10f * scale;
        dl.AddCircleFilled(handle, handleRadius, ImGui.GetColorU32(new Vector4(0.16f, 0.16f, 0.18f, 0.95f * opacity)),
            20);
        ProgressRing.CenterIcon(dl, handle, FontAwesomeIcon.ExpandAlt, new Vector4(1f, 1f, 1f, opacity), 9f * scale);
    }

    private void UpdateEditInput(float delta)
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
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
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

            BeginPress(mouse, slots, true);
            return;
        }

        BeginPress(mouse, slots, false);
        if (pressSlot is not null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressTime += delta;
            if ((mouse - pressOrigin).Length() < TapSlop * metrics.Scale && pressTime >= LongPressSeconds)
            {
                EnterEdit();
                StartDrag(pressSlot, mouse);
                ControlTile.CancelPress();
            }
        }
    }

    private void BeginPress(Vector2 mouse, IReadOnlyList<ControlSlot> slots, bool armDrag)
    {
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
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

            var center = Pose(slots[slotIndex].Id).Current.Center;
            var rect = Pose(slots[slotIndex].Id).Current;
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

    private void EnterEdit()
    {
        editing = true;
        editClock = 0f;
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

    private static bool TextPill(ImDrawListPtr dl, Rect rect, string text, Vector4 accent, float opacity,
        bool interactive)
    {
        var hovered = interactive && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        Squircle.Fill(dl, rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(accent, (hovered ? 1f : 0.9f) * opacity)));
        Typography.DrawCentered(dl, rect.Center, text, new Vector4(1f, 1f, 1f, opacity), 0.82f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawNotificationSection(ImDrawListPtr dl, PhoneTheme theme, float left, float right,
        float contentBottom, Rect screen, float scale, float opacity, bool interactive)
    {
        var titleTop = contentBottom + 26f * scale;
        var panelTop = titleTop + 32f * scale;
        var panelBottom = screen.Max.Y - DismissBandHeight * scale - 8f * scale;
        if (panelBottom - panelTop < 80f * scale)
        {
            return;
        }

        Typography.Draw(dl, new Vector2(left, titleTop), Loc.T(L.ControlCenter.Notifications),
            Palette.WithAlpha(theme.TextStrong, opacity), 1.0f, FontWeight.Bold);
        var padding = 12f * scale;
        var rounding = 22f * scale;
        var panel = new Rect(new Vector2(left, panelTop), new Vector2(right, panelBottom));
        Squircle.Fill(dl, panel.Min, panel.Max, rounding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f * opacity)));
        Material.EdgeSquircle(dl, panel.Min, panel.Max, rounding, scale, opacity);
        var inner = new Rect(panel.Min + new Vector2(padding, padding), panel.Max - new Vector2(padding, padding));
        notificationCenter.DrawOverlay(dl, inner, theme, opacity, interactive && !editing);
    }

    public void Open()
    {
        open = true;
        target = 1f;
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
        gallery.Close();
    }

    private void HandleGesture(Rect screen, float delta, bool gesturesEnabled, bool allowDismiss)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = screen.Height;
        var openDistance = height * OpenFraction;
        var fling = FlingVelocity * scale;
        drag.Track(delta);
        if (!open)
        {
            if (gesturesEnabled)
            {
                var topBand = new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + TopBandHeight * scale));
                if (!drag.Active && topBand.Contains(ImGui.GetMousePos()))
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                drag.Begin(topBand);
            }

            if (drag.Released(out var totalDelta, out _) && MathF.Abs(totalDelta.X) < TapSlop * scale &&
                MathF.Abs(totalDelta.Y) < TapSlop * scale)
            {
                Open();
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
            offset.Step(target, SmoothTime, delta);
            if (offset.IsResting(target, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
            {
                offset.SnapTo(target);
            }
        }
    }
}
