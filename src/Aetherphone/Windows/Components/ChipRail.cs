using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class ChipRail
{
    private const float Height = 34f;
    private const float Gap = 8f;
    private const float SidePad = 2f;
    private const float DragSlop = 5f;

    private float offset;
    private float maxOffset;
    private bool dragging;
    private float dragTravel;
    private float lastMouseX;

    public int Draw(AppSkin ui, ReadOnlySpan<string> labels, ReadOnlySpan<bool> active)
    {
        if (labels.Length == 0)
        {
            return -1;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var row = ReserveRow(scale);
        var gap = Gap * scale;
        var content = SidePad * 2f * scale;
        for (var index = 0; index < labels.Length; index++)
        {
            content += ChipWidth(labels[index], scale) + (index > 0 ? gap : 0f);
        }

        maxOffset = MathF.Max(0f, content - row.Width);
        HandleDrag(row);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(row.Min, row.Max, true);
        var cursorX = row.Min.X + SidePad * scale - offset;
        var tapped = -1;
        for (var index = 0; index < labels.Length; index++)
        {
            var width = ChipWidth(labels[index], scale);
            if (cursorX + width >= row.Min.X && cursorX <= row.Max.X
                && DrawChip(drawList, ui, labels[index], active[index],
                    new Vector2(cursorX, row.Center.Y), width, scale))
            {
                tapped = index;
            }

            cursorX += width + gap;
        }

        drawList.PopClipRect();
        return tapped;
    }

    private static float ChipWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + 26f * scale;

    private bool DrawChip(ImDrawListPtr drawList, AppSkin ui, string label, bool active, Vector2 leftCenter,
        float width, float scale)
    {
        var height = Height * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        var radius = height * 0.5f;
        var hovered = UiInteract.Hover(min, max);
        var highlighted = hovered && !dragging;
        var fill = active
            ? Palette.WithAlpha(ui.Accent, highlighted ? 1f : 0.92f)
            : highlighted ? ui.HoverTint : ui.FieldSurface;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        var ink = active ? new Vector4(0.11f, 0.08f, 0.02f, 1f) : ui.BodyInk;
        Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), label, ink,
            TextStyles.SubheadlineEmphasized);
        if (highlighted)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return dragTravel <= DragSlop * scale && UiInteract.Click(min, max, hovered);
    }

    private static Rect ReserveRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Height * scale;
        ImGui.Dummy(new Vector2(width, height));
        return new Rect(new Vector2(ImGui.GetWindowPos().X, origin.Y),
            new Vector2(origin.X + width, origin.Y + height));
    }

    private void HandleDrag(Rect row)
    {
        if (ImGui.IsMouseHoveringRect(row.Min, row.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
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
            if (dragTravel > DragSlop * ImGuiHelpers.GlobalScale)
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
