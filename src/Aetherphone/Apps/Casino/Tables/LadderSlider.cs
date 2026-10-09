using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class LadderSlider
{
    public const float RowHeight = 44f;
    public const float ThumbRadius = 12f;
    public const float TrackThickness = 6f;

    private const float TickRadius = 2f;
    private const float ThumbGrowth = 1.15f;
    private const float ShadowAlpha = 0.30f;
    private const float TickAlpha = 0.35f;
    private const int Segments = 28;

    private static readonly Vector4 Thumb = new(1f, 1f, 1f, 1f);

    private static string draggingId = string.Empty;
    private static bool draggingHigh;

    public static float TrackLeft(in Rect row, float scale) => row.Min.X + ThumbRadius * scale;

    public static float TrackWidth(in Rect row, float scale) => MathF.Max(1f, row.Width - ThumbRadius * 2f * scale);

    public static float XOf(int index, LadderSpan span, float left, float width) =>
        span.Count <= 1 ? left : left + width * (index - span.First) / (span.Count - 1);

    public static int IndexAt(float x, LadderSpan span, float left, float width)
    {
        if (span.Count <= 1)
        {
            return span.First;
        }

        var fraction = Math.Clamp((x - left) / MathF.Max(1f, width), 0f, 1f);
        return span.First + (int)MathF.Round(fraction * (span.Count - 1));
    }

    public static bool Single(ImDrawListPtr drawList, string id, in Rect row, LadderSpan span, ref int index,
        Vector4 accent, Vector4 rail, float scale)
    {
        var high = span.Clamp(index);
        var low = span.First;
        var changed = Track(drawList, id, row, span, ref low, ref high, false, accent, rail, scale);
        index = high;
        return changed;
    }

    public static bool Range(ImDrawListPtr drawList, string id, in Rect row, LadderSpan span, ref int low,
        ref int high, Vector4 accent, Vector4 rail, float scale)
    {
        low = span.Clamp(low);
        high = Math.Max(low, span.Clamp(high));
        return Track(drawList, id, row, span, ref low, ref high, true, accent, rail, scale);
    }

    private static bool Track(ImDrawListPtr drawList, string id, in Rect row, LadderSpan span, ref int low,
        ref int high, bool range, Vector4 accent, Vector4 rail, float scale)
    {
        var left = TrackLeft(row, scale);
        var width = TrackWidth(row, scale);
        var interaction = Interact(id, row, out var activated, out var active, out var pointerX);
        var changed = false;
        if (activated && span.Count > 1)
        {
            draggingId = id;
            var lowX = XOf(low, span, left, width);
            var highX = XOf(high, span, left, width);
            draggingHigh = !range || MathF.Abs(pointerX - highX) <= MathF.Abs(pointerX - lowX)
                || (low == high && pointerX > highX);
        }

        if (active && string.Equals(draggingId, id, StringComparison.Ordinal))
        {
            var picked = IndexAt(pointerX, span, left, width);
            if (draggingHigh)
            {
                var next = range ? Math.Max(low, picked) : picked;
                changed = next != high;
                high = next;
            }
            else
            {
                var next = Math.Min(high, picked);
                changed = next != low;
                low = next;
            }
        }
        else if (!active && string.Equals(draggingId, id, StringComparison.Ordinal))
        {
            draggingId = string.Empty;
        }

        Paint(drawList, row, span, low, high, range, interaction || active, accent, rail, scale);
        return changed;
    }

    private static bool Interact(string id, in Rect row, out bool activated, out bool active, out float pointerX)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(row.Min);
        ImGui.InvisibleButton(id, row.Size);
        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                      && UiInteract.Hover(row.Min, row.Max);
        activated = hovered && ImGui.IsItemActivated();
        active = ImGui.IsItemActive();
        ImGui.SetCursorScreenPos(cursor);
        pointerX = ImGui.GetMousePos().X;
        if (hovered || active)
        {
            UiInteract.ReportDragSurface();
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered;
    }

    private static void Paint(ImDrawListPtr drawList, in Rect row, LadderSpan span, int low, int high, bool range,
        bool engaged, Vector4 accent, Vector4 rail, float scale)
    {
        var left = TrackLeft(row, scale);
        var width = TrackWidth(row, scale);
        var centerY = row.Center.Y;
        var half = TrackThickness * 0.5f * scale;
        drawList.AddRectFilled(new Vector2(left, centerY - half), new Vector2(left + width, centerY + half),
            ImGui.GetColorU32(rail), half);
        var tick = TickRadius * scale;
        var tickInk = ImGui.GetColorU32(Palette.WithAlpha(Thumb, TickAlpha));
        for (var index = span.First; index <= span.Last; index++)
        {
            drawList.AddCircleFilled(new Vector2(XOf(index, span, left, width), centerY), tick, tickInk, 8);
        }

        var lowX = range ? XOf(low, span, left, width) : left;
        var highX = XOf(high, span, left, width);
        drawList.AddRectFilled(new Vector2(lowX, centerY - half), new Vector2(highX, centerY + half),
            ImGui.GetColorU32(accent), half);
        var radius = ThumbRadius * scale * (engaged ? ThumbGrowth : 1f);
        if (range)
        {
            DrawThumb(drawList, new Vector2(lowX, centerY), radius, accent, scale);
        }

        DrawThumb(drawList, new Vector2(highX, centerY), radius, accent, scale);
    }

    private static void DrawThumb(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float scale)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, 1.5f * scale), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ShadowAlpha)), Segments);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Thumb), Segments);
        drawList.AddCircleFilled(center, radius * 0.38f, ImGui.GetColorU32(accent), Segments);
    }
}
