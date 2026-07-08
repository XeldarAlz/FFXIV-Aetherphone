using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher;

internal static class WeatherGlyph
{
    public static void Draw(WeatherKind kind, Vector2 center, float radius, in SkyPalette palette, bool isDay,
        Vector4 sky)
    {
        var drawList = ImGui.GetWindowDrawList();
        switch (kind)
        {
            case WeatherKind.Clear:
                if (isDay)
                {
                    DrawSun(drawList, center, radius, palette.Glow);
                }
                else
                {
                    DrawMoon(drawList, center, radius, palette.Glow, sky);
                }

                break;
            case WeatherKind.Clouds:
                DrawLuminary(drawList, center - new Vector2(radius * 0.30f, radius * 0.34f), radius * 0.62f,
                    palette.Glow, sky, isDay);
                DrawCloud(drawList, center + new Vector2(0f, radius * 0.10f), radius, CloudFill(isDay), true);
                break;
            case WeatherKind.Fog:
                DrawLuminary(drawList, center - new Vector2(radius * 0.20f, radius * 0.40f), radius * 0.50f,
                    palette.Glow with { W = 0.5f }, sky, isDay);
                DrawFog(drawList, center, radius, CloudFill(isDay));
                break;
            case WeatherKind.Rain:
                DrawCloud(drawList, center - new Vector2(0f, radius * 0.24f), radius * 0.86f, CloudFill(isDay), true);
                DrawRain(drawList, center, radius, new Vector4(0.62f, 0.80f, 0.98f, 1f));
                break;
            case WeatherKind.Thunder:
                DrawCloud(drawList, center - new Vector2(0f, radius * 0.24f), radius * 0.86f, CloudFill(isDay), true);
                DrawBolt(drawList, center, radius, palette.Glow);
                break;
            case WeatherKind.Wind:
                DrawWind(drawList, center, radius, Palette.Lighten(palette.Glow, 0.1f));
                break;
            case WeatherKind.Sand:
                DrawLuminary(drawList, center - new Vector2(radius * 0.18f, radius * 0.40f), radius * 0.46f,
                    palette.Glow with { W = 0.7f }, sky, isDay);
                DrawWind(drawList, center, radius, palette.Glow);
                DrawDust(drawList, center, radius, palette.Glow);
                break;
            case WeatherKind.Heat:
                DrawSun(drawList, center - new Vector2(0f, radius * 0.18f), radius * 0.86f, palette.Glow);
                DrawHeatWaves(drawList, center, radius, palette.Glow);
                break;
            case WeatherKind.Snow:
                DrawCloud(drawList, center - new Vector2(0f, radius * 0.24f), radius * 0.86f, CloudFill(isDay), true);
                DrawSnow(drawList, center, radius, new Vector4(0.97f, 0.99f, 1.00f, 1f));
                break;
            default:
                DrawCloud(drawList, center - new Vector2(0f, radius * 0.06f), radius, Palette.Darken(CloudFill(isDay), 0.45f),
                    true);
                DrawDust(drawList, center, radius, palette.Glow);
                break;
        }
    }

    private static void DrawSun(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 glow)
    {
        var rotation = Pulse.Phase(26000.0) * MathF.PI * 2f;
        var rayColor = ImGui.GetColorU32(glow);
        var rayThickness = MathF.Max(radius * 0.085f, 1.25f);
        for (var ray = 0; ray < 8; ray++)
        {
            var angle = ray * (MathF.PI / 4f) + rotation;
            var inner = Polar(center, radius, 0.58f, angle);
            var outer = Polar(center, radius, 0.92f, angle);
            drawList.AddLine(inner, outer, rayColor, rayThickness);
            drawList.AddCircleFilled(outer, rayThickness * 0.5f, rayColor, 8);
        }

        drawList.AddCircleFilled(center, radius * 0.46f, rayColor, 40);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.10f, radius * 0.12f), radius * 0.30f,
            ImGui.GetColorU32(Palette.Lighten(glow, 0.4f)), 32);
    }

    private static void DrawMoon(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 glow, Vector4 sky)
    {
        drawList.AddCircleFilled(center, radius * 0.50f, ImGui.GetColorU32(glow), 40);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.34f, -radius * 0.18f), radius * 0.42f,
            ImGui.GetColorU32(sky), 40);
        if (radius < 20f)
        {
            return;
        }

        var crater = ImGui.GetColorU32(Palette.Darken(glow, 0.14f));
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.20f, radius * 0.18f), radius * 0.075f, crater, 12);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.04f, radius * 0.30f), radius * 0.05f, crater, 12);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.26f, -radius * 0.04f), radius * 0.045f, crater, 12);
    }

    private static void DrawLuminary(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 glow, Vector4 sky,
        bool isDay)
    {
        if (isDay)
        {
            DrawSun(drawList, center, radius, glow);
        }
        else
        {
            DrawMoon(drawList, center, radius, glow, sky);
        }
    }

    private static void DrawCloud(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 fill, bool withShadow)
    {
        if (withShadow)
        {
            CloudShape(drawList, center + new Vector2(0f, radius * 0.10f), radius, Palette.Darken(fill, 0.16f));
        }

        CloudShape(drawList, center, radius, fill);
        CloudShape(drawList, center - new Vector2(radius * 0.08f, radius * 0.14f), radius * 0.92f,
            Palette.Lighten(fill, 0.10f));
    }

    private static void CloudShape(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 fill)
    {
        var color = ImGui.GetColorU32(fill);
        drawList.AddCircleFilled(At(center, radius, -0.46f, 0.06f), radius * 0.34f, color, 28);
        drawList.AddCircleFilled(At(center, radius, 0.06f, -0.18f), radius * 0.50f, color, 32);
        drawList.AddCircleFilled(At(center, radius, 0.52f, 0.02f), radius * 0.36f, color, 28);
        drawList.AddRectFilled(At(center, radius, -0.78f, 0.06f), At(center, radius, 0.76f, 0.46f), color,
            radius * 0.28f);
    }

    private static void DrawRain(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 drop)
    {
        Span<float> offsets = stackalloc float[5] { -0.46f, -0.20f, 0.06f, 0.32f, 0.56f };
        for (var index = 0; index < offsets.Length; index++)
        {
            var fall = Frac(Pulse.Phase(820.0) + index * 0.21f);
            var headY = 0.30f + fall * 0.56f;
            var top = At(center, radius, offsets[index] + 0.06f, headY);
            var bottom = At(center, radius, offsets[index], headY + 0.18f);
            var alpha = MathF.Min(1f, (1f - fall) * 2.4f) * 0.9f;
            drawList.AddLine(top, bottom, ImGui.GetColorU32(drop with { W = alpha }),
                MathF.Max(radius * 0.055f, 1f));
            drawList.AddCircleFilled(bottom, MathF.Max(radius * 0.045f, 0.8f),
                ImGui.GetColorU32(drop with { W = alpha }), 8);
        }
    }

    private static void DrawSnow(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 flake)
    {
        Span<float> offsets = stackalloc float[6] { -0.50f, -0.28f, -0.04f, 0.20f, 0.42f, 0.62f };
        for (var index = 0; index < offsets.Length; index++)
        {
            var fall = Frac(Pulse.Phase(2600.0) + index * 0.17f);
            var sway = MathF.Sin((fall + index) * MathF.PI * 2f) * 0.05f;
            var position = At(center, radius, offsets[index] + sway, 0.32f + fall * 0.54f);
            var alpha = MathF.Min(1f, (1f - fall) * 2.6f);
            drawList.AddCircleFilled(position, MathF.Max(radius * (0.05f + (index % 2) * 0.02f), 0.9f),
                ImGui.GetColorU32(flake with { W = alpha }), 10);
        }
    }

    private static void DrawBolt(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 glow)
    {
        var flash = MathF.Pow(Pulse.Wave(1500.0), 3f);
        drawList.AddCircleFilled(At(center, radius, -0.02f, 0.34f), radius * 0.42f,
            ImGui.GetColorU32(glow with { W = 0.10f + 0.32f * flash }), 24);
        Span<Vector2> bolt = stackalloc Vector2[6]
        {
            At(center, radius, 0.12f, -0.04f), At(center, radius, -0.16f, 0.26f), At(center, radius, 0.02f, 0.26f),
            At(center, radius, -0.14f, 0.66f), At(center, radius, 0.20f, 0.16f), At(center, radius, 0.02f, 0.16f),
        };
        var boltColor = ImGui.GetColorU32(Palette.Lighten(glow, 0.15f * flash));
        FillConvex(drawList, boltColor, bolt[..4]);
        for (var index = 0; index < bolt.Length - 1; index++)
        {
            drawList.AddLine(bolt[index], bolt[index + 1], boltColor, radius * 0.06f);
        }
    }

    private static void DrawWind(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 stroke)
    {
        Span<float> rows = stackalloc float[3] { -0.26f, 0.06f, 0.36f };
        Span<float> lengths = stackalloc float[3] { 0.86f, 1.04f, 0.74f };
        Span<float> speeds = stackalloc float[3] { 5200.0f, 6400.0f, 4600.0f };
        for (var index = 0; index < rows.Length; index++)
        {
            var drift = MathF.Sin(Pulse.Phase(speeds[index]) * MathF.PI * 2f) * 0.10f;
            var rowY = rows[index];
            var startX = -0.62f + drift;
            var endX = startX + lengths[index];
            var alpha = 0.55f + 0.35f * Pulse.Wave(speeds[index] + 900.0);
            var color = ImGui.GetColorU32(stroke with { W = alpha });
            var thickness = MathF.Max(radius * 0.075f, 1.1f);
            var lineStart = At(center, radius, startX, rowY);
            var hookCenter = At(center, radius, endX, rowY - 0.14f);
            drawList.AddLine(lineStart, At(center, radius, endX, rowY), color, thickness);
            DrawArc(drawList, hookCenter, radius * 0.14f, MathF.PI * 0.5f, -MathF.PI * 0.7f, color, thickness);
        }
    }

    private static void DrawFog(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 fill)
    {
        Span<float> rows = stackalloc float[4] { -0.30f, -0.02f, 0.26f, 0.52f };
        Span<float> widths = stackalloc float[4] { 0.62f, 0.78f, 0.70f, 0.54f };
        Span<float> speeds = stackalloc float[4] { 4200.0f, 5600.0f, 3800.0f, 6200.0f };
        for (var index = 0; index < rows.Length; index++)
        {
            var drift = MathF.Sin(Pulse.Phase(speeds[index]) * MathF.PI * 2f) * 0.12f;
            var halfWidth = widths[index];
            var barMin = At(center, radius, -halfWidth + drift, rows[index] - 0.07f);
            var barMax = At(center, radius, halfWidth + drift, rows[index] + 0.07f);
            var alpha = 0.45f + 0.20f * Pulse.Wave(speeds[index] + 700.0);
            drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(fill with { W = alpha }), radius * 0.07f);
        }
    }

    private static void DrawDust(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 tint)
    {
        Span<float> baseX = stackalloc float[7] { -0.5f, -0.2f, 0.1f, 0.4f, 0.6f, -0.35f, 0.25f };
        Span<float> baseY = stackalloc float[7] { -0.1f, 0.2f, -0.25f, 0.1f, 0.35f, 0.4f, -0.45f };
        for (var index = 0; index < baseX.Length; index++)
        {
            var drift = Frac(Pulse.Phase(3400.0) + index * 0.13f);
            var position = At(center, radius, baseX[index] + drift * 0.4f - 0.2f, baseY[index]);
            var alpha = (0.3f + 0.4f * MathF.Sin(drift * MathF.PI)) * 0.7f;
            drawList.AddCircleFilled(position, radius * 0.045f, ImGui.GetColorU32(tint with { W = alpha }), 8);
        }
    }

    private static void DrawHeatWaves(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 tint)
    {
        Span<Vector2> wave = stackalloc Vector2[9];
        for (var row = 0; row < 3; row++)
        {
            var phase = Pulse.Phase(2600.0 + row * 400.0) * MathF.PI * 2f;
            var baseY = 0.42f + row * 0.16f;
            var halfWidth = 0.5f - row * 0.06f;
            for (var point = 0; point < wave.Length; point++)
            {
                var unitX = -halfWidth + (point / (float)(wave.Length - 1)) * halfWidth * 2f;
                var unitY = baseY + MathF.Sin(point * 0.9f + phase) * 0.05f;
                wave[point] = At(center, radius, unitX, unitY);
            }

            var alpha = 0.4f + 0.3f * Pulse.Wave(2200.0 + row * 300.0);
            var color = ImGui.GetColorU32(tint with { W = alpha });
            for (var point = 0; point < wave.Length - 1; point++)
            {
                drawList.AddLine(wave[point], wave[point + 1], color, radius * 0.05f);
            }
        }
    }

    private static void DrawArc(ImDrawListPtr drawList, Vector2 center, float radius, float fromAngle, float toAngle,
        uint color, float thickness)
    {
        const int segments = 10;
        var previous = center + new Vector2(MathF.Cos(fromAngle), MathF.Sin(fromAngle)) * radius;
        for (var step = 1; step <= segments; step++)
        {
            var angle = fromAngle + (toAngle - fromAngle) * (step / (float)segments);
            var current = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            drawList.AddLine(previous, current, color, thickness);
            previous = current;
        }
    }

    private static Vector4 CloudFill(bool isDay) =>
        isDay ? new Vector4(0.97f, 0.98f, 1.00f, 1f) : new Vector4(0.80f, 0.84f, 0.92f, 1f);

    private static float Frac(float value) => value - MathF.Floor(value);

    private static Vector2 At(Vector2 center, float radius, float unitX, float unitY) =>
        new(center.X + unitX * radius, center.Y + unitY * radius);

    private static Vector2 Polar(Vector2 center, float radius, float distance, float angle) =>
        new(center.X + MathF.Cos(angle) * distance * radius, center.Y + MathF.Sin(angle) * distance * radius);

    private static void FillConvex(ImDrawListPtr drawList, uint color, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathFillConvex(color);
    }
}
