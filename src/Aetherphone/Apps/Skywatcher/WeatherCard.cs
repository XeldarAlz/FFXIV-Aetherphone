using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher;

internal static class WeatherCard
{
    private const int ShadowLayers = 3;

    public static void Panel(ImDrawListPtr drawList, Rect card, in SkyPalette palette, float scale, float radius = -1f)
    {
        var corner = radius < 0f ? Metrics.Radius.Lg * scale : radius;
        Shadow(drawList, card, corner, palette.Shadow, scale);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, corner,
            ImGui.GetColorU32(palette.SurfaceTop), ImGui.GetColorU32(palette.SurfaceBottom));
        Squircle.Stroke(drawList, card.Min, card.Max, corner, ImGui.GetColorU32(palette.CardStroke), 1f * scale);
        Sheen(drawList, card, corner, palette.Sheen, scale);
    }

    public static void Shadow(ImDrawListPtr drawList, Rect card, float radius, Vector4 color, float scale)
    {
        if (color.W <= 0f)
        {
            return;
        }

        for (var layer = 0; layer < ShadowLayers; layer++)
        {
            var grow = (2f + layer * 3f) * scale;
            var drop = (3f + layer * 2f) * scale;
            var min = new Vector2(card.Min.X - grow, card.Min.Y - grow + drop);
            var max = new Vector2(card.Max.X + grow, card.Max.Y + grow + drop);
            var alpha = color.W * (1f - layer * 0.3f) / ShadowLayers;
            Squircle.Fill(drawList, min, max, radius + grow, ImGui.GetColorU32(color with { W = alpha }));
        }
    }

    public static void Sheen(ImDrawListPtr drawList, Rect card, float radius, Vector4 color, float scale)
    {
        if (color.W <= 0f)
        {
            return;
        }

        var inset = MathF.Max(radius, 1f);
        var y = card.Min.Y + 1.2f * scale;
        drawList.AddLine(new Vector2(card.Min.X + inset, y), new Vector2(card.Max.X - inset, y),
            ImGui.GetColorU32(color), 1.2f * scale);
    }

    public static void Chip(ImDrawListPtr drawList, Rect chip, WeatherKind kind, bool isDay, float scale)
    {
        var palette = WeatherSky.Resolve(kind, isDay);
        var radius = Metrics.Radius.Md * scale;
        Squircle.FillVerticalGradient(drawList, chip.Min, chip.Max, radius,
            ImGui.GetColorU32(palette.Top), ImGui.GetColorU32(palette.Bottom));
        var glyphRadius = MathF.Min(chip.Width, chip.Height) * 0.36f;
        WeatherGlyph.Draw(kind, chip.Center, glyphRadius, palette, isDay, palette.Bottom);
        Squircle.Stroke(drawList, chip.Min, chip.Max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), 1f * scale);
    }
}
