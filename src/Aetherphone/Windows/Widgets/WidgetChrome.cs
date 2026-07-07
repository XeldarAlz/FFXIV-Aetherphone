using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetChrome
{
    public const float RadiusUnits = 22f;
    private const float EyebrowTracking = 1.6f;
    private const float EyebrowFontScale = 0.66f;

    public static float Radius(float scale) => RadiusUnits * scale;

    public static void Card(ImDrawListPtr drawList, Rect bounds, float scale, float opacity)
    {
        Material.Frosted(drawList, bounds.Min, bounds.Max, Radius(scale), scale, opacity);
    }

    public static void Tinted(ImDrawListPtr drawList, Rect bounds, Vector4 top, Vector4 bottom, float scale,
        float opacity)
    {
        var radius = Radius(scale);
        Squircle.FillVerticalGradient(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(top with { W = top.W * opacity }),
            ImGui.GetColorU32(bottom with { W = bottom.W * opacity }));
        Material.Veil(drawList, bounds.Min, bounds.Max, 0.08f * opacity, radius);
        Material.EdgeSquircle(drawList, bounds.Min, bounds.Max, radius, scale, opacity);
    }

    public static void Eyebrow(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale,
        float opacity)
    {
        Tracked(drawList, position, text.ToUpperInvariant(), color with { W = color.W * opacity }, EyebrowFontScale,
            FontWeight.SemiBold, EyebrowTracking * scale);
    }

    public static float EyebrowWidth(string text, float scale)
    {
        var upper = text.ToUpperInvariant();
        var width = Typography.Measure(upper, EyebrowFontScale, FontWeight.SemiBold).X;
        return width + EyebrowTracking * scale * Math.Max(0, upper.Length - 1);
    }

    public static void Tracked(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float fontScale,
        FontWeight weight, float tracking)
    {
        using (Plugin.Fonts.Push(fontScale, weight))
        {
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(color);
            var cursor = position;
            Span<char> buffer = stackalloc char[1];
            for (var index = 0; index < text.Length; index++)
            {
                buffer[0] = text[index];
                var glyph = new string(buffer);
                drawList.AddText(font, fontSize, cursor, packed, glyph);
                cursor.X += ImGui.CalcTextSize(glyph).X + tracking;
            }
        }
    }
}
