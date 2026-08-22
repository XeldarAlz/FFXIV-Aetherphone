using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class ChipRail
{
    public const float RowHeight = 34f;

    public const float DefaultLabelPadding = 26f;
    public const float CompactLabelPadding = 14f;

    private const float Gap = 8f;
    private const float SidePad = 2f;
    private const float DragSlop = 5f;

    private float offset;
    private float maxOffset;
    private bool dragging;
    private float dragTravel;
    private float lastMouseX;

    public int Draw(AppSkin ui, ReadOnlySpan<string> labels, ReadOnlySpan<bool> active, string? anchorKey = null,
        float labelPadding = DefaultLabelPadding) =>
        Draw(ReserveRow(this, UiScale.Current), ui, labels, active, false, anchorKey, labelPadding);

    public int Draw(Rect row, AppSkin ui, ReadOnlySpan<string> labels, ReadOnlySpan<bool> active, bool overlay = false,
        string? anchorKey = null, float labelPadding = DefaultLabelPadding)
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
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(row.Min, row.Max, true);
        var cursorX = row.Min.X + SidePad * scale - offset;
        var tapped = -1;
        for (var index = 0; index < labels.Length; index++)
        {
            var width = ChipWidth(labels[index], scale, labelPadding);
            if (cursorX + width >= row.Min.X && cursorX <= row.Max.X
                && DrawChip(drawList, ui, labels[index], active[index],
                    new Vector2(cursorX, row.Center.Y), width, scale, overlay))
            {
                tapped = index;
            }

            cursorX += width + gap;
        }

        drawList.PopClipRect();
        return tapped;
    }

    private static float ChipWidth(string label, float scale, float labelPadding) =>
        Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + labelPadding * scale;

    private bool DrawChip(ImDrawListPtr drawList, AppSkin ui, string label, bool active, Vector2 leftCenter,
        float width, float scale, bool overlay)
    {
        var height = RowHeight * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        var radius = height * 0.5f;
        var hovered = Hovered(min, max, overlay);
        var highlighted = hovered && !dragging;
        var fill = active
            ? Palette.WithAlpha(ui.Accent, highlighted ? 1f : 0.92f)
            : highlighted ? ui.HoverTint : ui.FieldSurface;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        var ink = active ? new Vector4(0.11f, 0.08f, 0.02f, 1f) : ui.BodyInk;
        var labelSize = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var labelOrigin = new Vector2((min.X + max.X - labelSize.X) * 0.5f, (min.Y + max.Y - labelSize.Y) * 0.5f);
        Typography.Draw(drawList, labelOrigin, label, ink, TextStyles.SubheadlineEmphasized);
        if (highlighted)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return dragTravel <= DragSlop * scale && UiInteract.Click(min, max, hovered);
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
    }
}
