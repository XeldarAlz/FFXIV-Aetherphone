using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Material
{
    private const float BorderAlpha = 0.09f;
    private const float HighlightAlpha = 0.11f;

    public static void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 fill, float scale, float opacity = 1f)
    {
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill with { W = fill.W * opacity }), rounding);
        Edge(drawList, min, max, rounding, scale, opacity);
    }

    public static void Edge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale, float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)), rounding, ImDrawFlags.RoundCornersAll, 1f * scale);
        Highlight(drawList, min, max, rounding, scale, opacity);
    }

    public static void EdgeSquircle(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale, float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)), 1f * scale);
        Highlight(drawList, min, max, radius, scale, opacity);
    }

    private static void Highlight(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale, float opacity)
    {
        var highlight = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, HighlightAlpha * opacity));
        var inset = MathF.Max(rounding, 1f);
        drawList.AddLine(new Vector2(min.X + inset, min.Y + 1f * scale), new Vector2(max.X - inset, min.Y + 1f * scale), highlight, 1f * scale);
    }
}
