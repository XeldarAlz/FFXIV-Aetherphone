using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal static class MapGlyphs
{
    private const float HighlightBleed = 8f;

    public static void Highlight(Rect row, Vector4 color, float verticalInset, float scale)
    {
        var min = new Vector2(row.Min.X - HighlightBleed * scale, row.Min.Y + verticalInset * scale);
        var max = new Vector2(row.Max.X + HighlightBleed * scale, row.Max.Y - verticalInset * scale);
        Squircle.Fill(ImGui.GetWindowDrawList(), min, max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(color));
    }

    public static void Pin(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        var headCenter = new Vector2(center.X, center.Y - radius * 0.35f);
        drawList.AddCircleFilled(headCenter, radius * 0.7f, packed, 24);
        var tip = new Vector2(center.X, center.Y + radius);
        drawList.AddTriangleFilled(new Vector2(headCenter.X - radius * 0.55f, headCenter.Y + radius * 0.2f),
            new Vector2(headCenter.X + radius * 0.55f, headCenter.Y + radius * 0.2f), tip, packed);
        drawList.AddCircleFilled(headCenter, radius * 0.28f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)), 16);
    }

    public static void Disclosure(ImDrawListPtr drawList, Vector2 center, float size, float thickness, bool expanded,
        Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        if (expanded)
        {
            var tip = new Vector2(center.X, center.Y + size * 0.7f);
            drawList.AddLine(new Vector2(center.X - size, center.Y - size * 0.45f), tip, packed, thickness);
            drawList.AddLine(tip, new Vector2(center.X + size, center.Y - size * 0.45f), packed, thickness);
        }
        else
        {
            var tip = new Vector2(center.X + size * 0.7f, center.Y);
            drawList.AddLine(new Vector2(center.X - size * 0.45f, center.Y - size), tip, packed, thickness);
            drawList.AddLine(tip, new Vector2(center.X - size * 0.45f, center.Y + size), packed, thickness);
        }
    }

    public static void ChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }
}
