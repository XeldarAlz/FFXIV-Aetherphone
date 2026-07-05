using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class Material
{
    private const float BorderAlpha = 0.09f;
    private const float HighlightAlpha = 0.11f;
    private static readonly Vector4 FrostedFill = new(0.12f, 0.12f, 0.15f, 0.86f);

    public static void TopGlow(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 accent,
        float coverage, float strength)
    {
        if (strength <= 0f)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var tint = ImGui.GetColorU32(accent with { W = strength });
        var clear = ImGui.GetColorU32(accent with { W = 0f });
        var capBottom = min.Y + rounding + scale;
        drawList.AddRectFilled(min, new Vector2(max.X, capBottom), tint, rounding, ImDrawFlags.RoundCornersTop);
        var fadeBottom = min.Y + (max.Y - min.Y) * coverage;
        if (fadeBottom > capBottom)
        {
            drawList.AddRectFilledMultiColor(new Vector2(min.X, capBottom), new Vector2(max.X, fadeBottom), tint, tint,
                clear, clear);
        }
    }

    public static void Veil(ImDrawListPtr drawList, Vector2 min, Vector2 max, float dim, float rounding = 0f)
    {
        if (dim <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)), rounding);
    }

    public static void Frosted(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(FrostedFill with { W = FrostedFill.W * opacity }));
        EdgeSquircle(drawList, min, max, radius, scale, opacity);
    }

    public static void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 fill, float scale,
        float opacity = 1f)
    {
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill with { W = fill.W * opacity }), rounding);
        Edge(drawList, min, max, rounding, scale, opacity);
    }

    public static void Edge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)), rounding,
            ImDrawFlags.RoundCornersAll, 1f * scale);
        Highlight(drawList, min, max, rounding, scale, opacity);
    }

    public static void EdgeSquircle(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)),
            1f * scale);
        Highlight(drawList, min, max, radius, scale, opacity);
    }

    private static void Highlight(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale,
        float opacity)
    {
        var highlight = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, HighlightAlpha * opacity));
        var inset = MathF.Max(rounding, 1f);
        drawList.AddLine(new Vector2(min.X + inset, min.Y + 1f * scale), new Vector2(max.X - inset, min.Y + 1f * scale),
            highlight, 1f * scale);
    }
}
