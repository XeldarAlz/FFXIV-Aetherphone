using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class ChipRail
{
    public const float RowHeight = 34f;
    public const float ChipHeight = 30f;
    public const float ChipPadX = 13f;
    public static readonly TextStyle LabelStyle = TextStyles.SubheadlineEmphasized;

    public const float DefaultLabelPadding = 26f;
    public const float CompactLabelPadding = 14f;

    private const float Gap = 8f;
    private const float SidePad = 2f;
    private const float DragSlop = 5f;
    private const float ArrowRadius = RoundButton.SmallRadius;
    private const float ArrowInset = RoundButton.SmallRadius + SidePad;
    private const float ArrowBaseRing = 2f;
    private const int ArrowBaseSegments = 32;
    private const float ArrowGlyphFraction = 0.95f;
    private const float PageFraction = 0.75f;
    private const float OverflowEpsilon = 0.5f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private const float ActiveHoverLift = 0.10f;

    private float offset;
    private float maxOffset;
    private bool dragging;
    private float dragTravel;
    private float lastMouseX;
    private Spring pageSpring;
    private float pageTarget;
    private bool paging;

    public int Draw(AppSkin ui, ReadOnlySpan<string> labels, ReadOnlySpan<bool> active, string? anchorKey = null,
        float labelPadding = DefaultLabelPadding, bool interactive = true) =>
        Draw(ReserveRow(this, UiScale.Current), ui, labels, active, false, anchorKey, labelPadding,
            interactive: interactive);

    public int Draw(Rect row, AppSkin ui, ReadOnlySpan<string> labels, ReadOnlySpan<bool> active, bool overlay = false,
        string? anchorKey = null, float labelPadding = DefaultLabelPadding, bool centered = false,
        bool interactive = true, float chipHeight = ChipHeight)
    {
        if (labels.Length == 0)
        {
            return -1;
        }

        var scale = UiScale.Current;
        if (anchorKey is not null)
        {
            UiAnchors.Report(anchorKey, row);
        }

        var gap = Gap * scale;
        var content = SidePad * 2f * scale;
        for (var index = 0; index < labels.Length; index++)
        {
            content += ChipWidth(labels[index], scale, labelPadding) + (index > 0 ? gap : 0f);
        }

        maxOffset = MathF.Max(0f, content - row.Width);
        HandleDrag(row, overlay);
        StepPaging();
        var canPageBack = offset > OverflowEpsilon;
        var canPageForward = offset < maxOffset - OverflowEpsilon;
        var backArrow = ArrowRect(row, scale, false);
        var forwardArrow = ArrowRect(row, scale, true);
        var arrowHovered = (canPageBack && Hovered(backArrow.Min, backArrow.Max, overlay))
            || (canPageForward && Hovered(forwardArrow.Min, forwardArrow.Max, overlay));
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(row.Min, row.Max, true);
        var slack = centered ? MathF.Max(0f, row.Width - content) * 0.5f : 0f;
        var cursorX = row.Min.X + SidePad * scale - offset + slack;
        var tapped = -1;
        for (var index = 0; index < labels.Length; index++)
        {
            var width = ChipWidth(labels[index], scale, labelPadding);
            if (cursorX + width >= row.Min.X && cursorX <= row.Max.X
                && DrawChip(drawList, ui, labels[index], active[index],
                    new Vector2(cursorX, row.Center.Y), width, chipHeight * scale, scale, overlay, arrowHovered,
                    interactive))
            {
                tapped = index;
            }

            cursorX += width + gap;
        }

        if (canPageBack && DrawArrow(drawList, ui, backArrow, false, scale, overlay))
        {
            PageTo(offset - row.Width * PageFraction);
        }

        if (canPageForward && DrawArrow(drawList, ui, forwardArrow, true, scale, overlay))
        {
            PageTo(offset + row.Width * PageFraction);
        }

        drawList.PopClipRect();
        return tapped;
    }

    private static Rect ArrowRect(Rect row, float scale, bool forward)
    {
        var radius = ArrowRadius * scale;
        var centerX = forward ? row.Max.X - ArrowInset * scale : row.Min.X + ArrowInset * scale;
        var center = new Vector2(centerX, row.Center.Y);
        return new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
    }

    private bool DrawArrow(ImDrawListPtr drawList, AppSkin ui, Rect rect, bool forward, float scale, bool overlay)
    {
        var center = rect.Center;
        var radius = rect.Width * 0.5f;
        var baseRadius = radius + ArrowBaseRing * scale;
        drawList.AddCircleFilled(center, baseRadius, ImGui.GetColorU32(ui.Palette.BackdropTop with { W = 1f }),
            ArrowBaseSegments);
        var key = (uint)HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this), forward);
        var clicked = RoundButton.Draw(drawList, key, center, radius, ui.Ink, ButtonStyle.Gray, true, overlay,
            out var face);
        PhoneIcon.Draw(drawList, center, forward ? PhoneIcons.ChevronRight : PhoneIcons.ChevronLeft, face.LabelInk,
            radius * ArrowGlyphFraction * (face.Face.Width / MathF.Max(rect.Width, 0.0001f)));
        return clicked && dragTravel <= DragSlop * scale;
    }

    private void PageTo(float target)
    {
        pageSpring = new Spring(offset);
        pageTarget = Math.Clamp(target, 0f, maxOffset);
        paging = true;
    }

    private void StepPaging()
    {
        if (!paging)
        {
            return;
        }

        pageTarget = Math.Clamp(pageTarget, 0f, maxOffset);
        offset = pageSpring.Step(pageTarget, Motion.PageSettle, ImGui.GetIO().DeltaTime);
        if (pageSpring.IsResting(pageTarget, 0.5f, 1f))
        {
            offset = pageTarget;
            paging = false;
        }
    }

    private static float ChipWidth(string label, float scale, float labelPadding) =>
        WidthFor(Typography.Measure(label, LabelStyle).X, labelPadding, scale);

    public static float WidthFor(float labelWidth, float labelPadding, float scale) => labelWidth + labelPadding * scale;

    public static float LabelRoom(Rect chip) => chip.Width - chip.Height * 0.5f;

    private bool DrawChip(ImDrawListPtr drawList, AppSkin ui, string label, bool active, Vector2 leftCenter,
        float width, float height, float scale, bool overlay, bool shadowed, bool interactive = true)
    {
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        var hovered = interactive && !shadowed && Hovered(min, max, overlay);
        PaintChip(drawList, new Rect(min, max), label, active, hovered && !dragging, ui.Ink);
        return interactive && dragTravel <= DragSlop * scale && UiInteract.Click(min, max, hovered);
    }

    public static void PaintChip(ImDrawListPtr drawList, Rect rect, string label, bool active, bool highlighted,
        in ControlInk ink)
    {
        var radius = rect.Height * 0.5f;
        var fill = active
            ? Palette.Mix(ink.Accent, White, highlighted ? ActiveHoverLift : 0f)
            : Surfaces.Fill(ink, highlighted ? FillLevel.Secondary : FillLevel.Tertiary);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var labelInk = active ? White : ink.Ink;
        var fitted = Typography.FitText(label, MathF.Max(1f, LabelRoom(rect)), LabelStyle);
        Typography.DrawCentered(drawList, rect.Center, fitted, labelInk, LabelStyle);
        if (highlighted)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private static Rect ReserveRow(ChipRail rail, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = RowHeight * scale;
        ImGui.PushID(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rail));
        ImGui.InvisibleButton("##chipRailRow", new Vector2(width, height));
        ImGui.PopID();
        return new Rect(new Vector2(ImGui.GetWindowPos().X, origin.Y),
            new Vector2(origin.X + width, origin.Y + height));
    }

    private static bool Hovered(Vector2 min, Vector2 max, bool overlay) =>
        overlay ? UiInteract.HoverWindowOnly(min, max) : UiInteract.Hover(min, max);

    private void HandleDrag(Rect row, bool overlay)
    {
        if (Hovered(row.Min, row.Max, overlay) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragTravel = 0f;
            lastMouseX = ImGui.GetIO().MousePos.X;
        }

        if (dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var mouseX = ImGui.GetIO().MousePos.X;
            var travel = mouseX - lastMouseX;
            lastMouseX = mouseX;
            dragTravel += MathF.Abs(travel);
            if (dragTravel > DragSlop * UiScale.Current)
            {
                paging = false;
                offset -= travel;
            }
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            dragging = false;
        }

        offset = Math.Clamp(offset, 0f, maxOffset);
    }

    public void Reset()
    {
        offset = 0f;
        dragging = false;
        dragTravel = 0f;
        paging = false;
    }
}
