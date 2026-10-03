using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Skywatcher;

internal static class WeatherCard
{
    public const float PaddingUnits = 14f;
    public const float HeaderUnits = 34f;
    private const float CalmFillAlpha = 0.32f;
    private const float DenseFillAlpha = 0.70f;
    private const float LightFillAlpha = 0.34f;
    private const float StrokeAlpha = 0.12f;
    private const float DividerAlpha = 0.16f;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static float DensityFor(WeatherKind kind) => kind switch
    {
        WeatherKind.Rain or WeatherKind.Thunder or WeatherKind.Snow or WeatherKind.Sand => 1f,
        WeatherKind.Wind => 0.6f,
        WeatherKind.Clear => 0.25f,
        _ => 0f,
    };

    public static Vector4 Fill(in SkyPalette palette, float density)
    {
        if (palette.LightSky)
        {
            return White with { W = LightFillAlpha };
        }

        var tint = Vector4.Lerp(palette.Top, Black, 0.50f);
        return tint with { W = CalmFillAlpha + (DenseFillAlpha - CalmFillAlpha) * density };
    }

    public static Vector4 GlassBase(in SkyPalette palette) =>
        palette.LightSky ? palette.Horizon with { W = 1f } : Vector4.Lerp(palette.Top, Black, 0.50f) with { W = 1f };

    public static Vector4 Behind(in SkyPalette palette, float density)
    {
        var fill = Fill(palette, density);
        return Vector4.Lerp(palette.Horizon, fill with { W = 1f }, fill.W) with { W = 1f };
    }

    public static void Panel(ImDrawListPtr drawList, Rect card, in SkyPalette palette, float density, float scale,
        float radius = -1f)
    {
        var corner = radius < 0f ? Metrics.Radius.Grouped * scale : radius;
        Squircle.Fill(drawList, card.Min, card.Max, corner, ImGui.GetColorU32(Fill(palette, density)));
        Squircle.Stroke(drawList, card.Min, card.Max, corner,
            ImGui.GetColorU32((palette.LightSky ? palette.Ink : White) with { W = StrokeAlpha }), 1f * scale);
    }

    public static float Header(ImDrawListPtr drawList, Rect card, FontAwesomeIcon icon, string title,
        in SkyPalette palette, float scale)
    {
        var padding = PaddingUnits * scale;
        var height = HeaderUnits * scale;
        var centerY = card.Min.Y + height * 0.5f + 2f * scale;
        var iconCenter = new Vector2(card.Min.X + padding + 6f * scale, centerY);
        ProgressRing.CenterIcon(drawList, iconCenter, icon, palette.InkSoft, 11f * scale);
        var label = Loc.Upper(title);
        var style = TextStyles.FootnoteEmphasized;
        var labelHeight = Typography.LineHeight(style);
        var labelX = iconCenter.X + 12f * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, card.Max.X - padding - labelX), style);
        Typography.Draw(drawList, new Vector2(labelX, centerY - labelHeight * 0.5f), fitted, palette.InkSoft, style);
        Divider(drawList, card, card.Min.Y + height, palette, scale);
        return height;
    }

    public static void Divider(ImDrawListPtr drawList, Rect card, float y, in SkyPalette palette, float scale)
    {
        var padding = PaddingUnits * scale;
        drawList.AddLine(new Vector2(card.Min.X + padding, y), new Vector2(card.Max.X - padding, y),
            ImGui.GetColorU32(palette.Ink with { W = DividerAlpha }), 1f);
    }

    public static void Chip(ImDrawListPtr drawList, Rect chip, WeatherKind kind, bool isDay, float scale)
    {
        var palette = WeatherSky.Resolve(kind, isDay);
        var radius = Metrics.Radius.Md * scale;
        Squircle.FillVerticalGradient(drawList, chip.Min, chip.Max, radius,
            ImGui.GetColorU32(palette.Top), ImGui.GetColorU32(palette.Bottom));
        WeatherAmbience.Draw(drawList, chip, radius, kind, isDay, palette, scale * 0.6f, 0.85f);
        var glyphRadius = MathF.Min(chip.Width, chip.Height) * 0.30f;
        WeatherGlyph.Draw(drawList, kind, chip.Center, glyphRadius, palette, isDay, palette.Top);
        Squircle.Stroke(drawList, chip.Min, chip.Max, radius,
            ImGui.GetColorU32(White with { W = 0.14f }), 1f * scale);
    }

    public static void Glyph(ImDrawListPtr drawList, WeatherKind kind, Vector2 center, float radius, bool isDay,
        Vector4 behind)
    {
        var palette = WeatherSky.Resolve(kind, isDay);
        WeatherGlyph.Draw(drawList, kind, center, radius, palette, isDay, behind);
    }
}
