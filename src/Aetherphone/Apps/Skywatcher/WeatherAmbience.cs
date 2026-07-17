using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher;

internal static class WeatherAmbience
{
    private const float GoldenStep = 0.618034f;
    private const float GoldenStepConjugate = 0.381966f;
    private const int StarCount = 26;
    private const int SnowflakeCount = 14;
    private const int WindStreamCount = 3;
    private const int WindMoteCount = 4;
    private const int SandMoteCount = 9;
    private const int ShimmerRowCount = 3;
    private const int EmberCount = 4;
    private const int GloomMoteCount = 7;
    private const double LightningPeriodMs = 6800.0;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 RainTint = new(0.66f, 0.80f, 1.00f, 1f);
    private static readonly Vector4 EmberTint = new(1.00f, 0.66f, 0.36f, 1f);
    private static readonly Vector4 NightCloudFill = new(0.78f, 0.83f, 0.94f, 1f);
    private static readonly Vector4 NightFogFill = new(0.64f, 0.70f, 0.80f, 1f);
    private static readonly Vector4 FlashFill = new(1f, 0.98f, 0.90f, 1f);

    public static void Draw(ImDrawListPtr drawList, in Rect bounds, float rounding, WeatherKind kind, bool isDay,
        in SkyPalette palette, float scale, float opacity, bool withGlyph)
    {
        if (opacity <= 0.02f)
        {
            return;
        }

        DrawSkyDepth(drawList, bounds, rounding, palette, opacity);
        var inset = rounding * 0.30f;
        drawList.PushClipRect(bounds.Min + new Vector2(inset, inset), bounds.Max - new Vector2(inset, inset), true);
        DrawAtmosphere(drawList, bounds, kind, isDay, palette, scale, opacity);
        if (withGlyph)
        {
            DrawGlyph(drawList, bounds, kind, isDay, palette, scale, opacity);
        }

        drawList.PopClipRect();
        DrawVignette(drawList, bounds, rounding, opacity);
        if (kind == WeatherKind.Thunder)
        {
            DrawLightningVeil(drawList, bounds, rounding, opacity);
        }
    }

    public static void Halo(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float intensity)
    {
        const int layerCount = 10;
        for (var layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            var fraction = (layerIndex + 1f) / layerCount;
            var falloff = 1f - fraction;
            var alpha = intensity * 0.045f * falloff * falloff;
            if (alpha <= 0.002f)
            {
                continue;
            }

            drawList.AddCircleFilled(center, radius * (0.45f + 1.55f * fraction), Tone(color, alpha), 48);
        }
    }

    private static void DrawSkyDepth(ImDrawListPtr drawList, in Rect bounds, float rounding, in SkyPalette palette,
        float opacity)
    {
        Squircle.FillVerticalGradient(drawList, bounds.Min,
            new Vector2(bounds.Max.X, bounds.Min.Y + bounds.Height * 0.30f), rounding,
            Tone(palette.Glow, 0.06f * opacity), Tone(palette.Glow, 0f));
    }

    private static void DrawVignette(ImDrawListPtr drawList, in Rect bounds, float rounding, float opacity)
    {
        Squircle.FillVerticalGradient(drawList,
            new Vector2(bounds.Min.X, bounds.Max.Y - bounds.Height * 0.38f), bounds.Max, rounding,
            Tone(new Vector4(0f, 0f, 0f, 1f), 0f), Tone(new Vector4(0f, 0f, 0f, 1f), 0.10f * opacity));
    }

    private static void DrawAtmosphere(ImDrawListPtr drawList, in Rect bounds, WeatherKind kind, bool isDay,
        in SkyPalette palette, float scale, float opacity)
    {
        switch (kind)
        {
            case WeatherKind.Clear:
                if (isDay)
                {
                    DrawDriftingClouds(drawList, bounds, true, 2, 0.6f, opacity);
                }
                else
                {
                    DrawStars(drawList, bounds, palette.Glow, scale, opacity);
                    DrawShootingStar(drawList, bounds, palette.Glow, scale, opacity);
                }

                break;
            case WeatherKind.Clouds:
                DrawDriftingClouds(drawList, bounds, isDay, 4, 1f, opacity);
                break;
            case WeatherKind.Fog:
                DrawHazeBands(drawList, bounds, isDay ? White : NightFogFill, 4, 0.10f, opacity);
                break;
            case WeatherKind.Rain:
                DrawRainfall(drawList, bounds, scale, 16, opacity);
                break;
            case WeatherKind.Thunder:
                DrawRainfall(drawList, bounds, scale, 12, 0.85f * opacity);
                DrawLightningGlow(drawList, bounds, palette.Glow, opacity);
                break;
            case WeatherKind.Wind:
                DrawWindStreams(drawList, bounds, palette.Glow, scale, opacity);
                break;
            case WeatherKind.Sand:
                DrawSandstream(drawList, bounds, palette.Glow, scale, opacity);
                break;
            case WeatherKind.Heat:
                DrawHeatShimmer(drawList, bounds, palette.Glow, scale, opacity);
                break;
            case WeatherKind.Snow:
                DrawSnowfall(drawList, bounds, scale, opacity);
                break;
            default:
                DrawGloomMotes(drawList, bounds, palette.Glow, scale, opacity);
                break;
        }
    }

    private static void DrawGlyph(ImDrawListPtr drawList, in Rect bounds, WeatherKind kind, bool isDay,
        in SkyPalette palette, float scale, float opacity)
    {
        var center = new Vector2(bounds.Max.X - 26f * scale, bounds.Min.Y + 22f * scale);
        var radius = 10f * scale;
        var skyFraction = Math.Clamp((center.Y - bounds.Min.Y) / MathF.Max(1f, bounds.Height), 0f, 1f);
        var sky = Vector4.Lerp(palette.Top, palette.Bottom, skyFraction);
        var sheen = 0.06f * MathF.Max(0f, 1f - skyFraction / 0.30f) * opacity;
        sky = Vector4.Lerp(sky, palette.Glow, sheen);
        WeatherGlyph.Draw(kind, center, radius, palette, isDay, sky);
        Halo(drawList, center, radius * 1.4f, palette.Glow, (0.55f + 0.35f * Pulse.Wave(Pulse.Breath)) * opacity);
    }

    private static void DrawDriftingClouds(ImDrawListPtr drawList, in Rect bounds, bool isDay, int puffCount,
        float strength, float opacity)
    {
        var fill = isDay ? White : NightCloudFill;
        for (var puffIndex = 0; puffIndex < puffCount; puffIndex++)
        {
            var laneY = bounds.Min.Y + (0.14f + Frac(puffIndex * GoldenStep + 0.21f) * 0.55f) * bounds.Height;
            var progress = Frac(Pulse.Phase(26000.0 + puffIndex * 7300.0) + puffIndex * 0.37f);
            var radius = bounds.Width * (0.085f + Frac(puffIndex * GoldenStepConjugate + 0.4f) * 0.07f);
            var center = new Vector2(bounds.Min.X + (progress * 1.5f - 0.25f) * bounds.Width, laneY);
            var alpha = (0.06f + Frac(puffIndex * 0.271f) * 0.08f) * strength * opacity;
            DrawPuff(drawList, center, radius, fill, alpha);
        }
    }

    private static void DrawPuff(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 fill, float alpha)
    {
        var color = Tone(fill, alpha);
        drawList.AddCircleFilled(center, radius * 0.58f, color, 24);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.62f, radius * 0.16f), radius * 0.42f, color, 20);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.60f, radius * 0.12f), radius * 0.46f, color, 20);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.10f, radius * 0.30f), radius * 0.40f, color, 20);
    }

    private static void DrawStars(ImDrawListPtr drawList, in Rect bounds, Vector4 glow, float scale, float opacity)
    {
        for (var starIndex = 0; starIndex < StarCount; starIndex++)
        {
            var position = new Vector2(bounds.Min.X + Hash(starIndex, 12.9898f) * bounds.Width,
                bounds.Min.Y + MathF.Pow(Hash(starIndex, 78.233f), 1.35f) * bounds.Height * 0.72f);
            var twinkle = 0.62f + 0.38f * Pulse.Wave(2300.0 + Hash(starIndex, 3.7f) * 2600.0);
            var bright = starIndex % 6 == 0;
            var radius = (bright ? 1.35f : 0.65f + Hash(starIndex, 9.1f) * 0.5f) * scale;
            var alpha = (bright ? 0.85f : 0.20f + 0.35f * Hash(starIndex, 51.3f)) * twinkle * opacity;
            drawList.AddCircleFilled(position, radius, Tone(White, alpha), 10);
            if (bright)
            {
                drawList.AddCircleFilled(position, radius * 3.2f, Tone(glow, alpha * 0.12f), 16);
                var reach = radius * 3.4f;
                var beam = Tone(White, alpha * 0.38f);
                drawList.AddLine(position - new Vector2(reach, 0f), position + new Vector2(reach, 0f), beam,
                    0.8f * scale);
                drawList.AddLine(position - new Vector2(0f, reach), position + new Vector2(0f, reach), beam,
                    0.8f * scale);
            }
        }
    }

    private static float Hash(int index, float salt) => Frac(MathF.Sin(index * 127.1f + salt) * 43758.547f);

    private static void DrawShootingStar(ImDrawListPtr drawList, in Rect bounds, Vector4 glow, float scale,
        float opacity)
    {
        var cycle = Pulse.Phase(12600.0);
        if (cycle > 0.055f)
        {
            return;
        }

        var travel = cycle / 0.055f;
        var start = new Vector2(bounds.Min.X + bounds.Width * 0.14f, bounds.Min.Y + bounds.Height * 0.12f);
        var sweep = new Vector2(bounds.Width * 0.5f, bounds.Height * 0.26f);
        var head = start + sweep * travel;
        var tail = head - Vector2.Normalize(sweep) * 16f * scale * (1f - travel * 0.5f);
        var alpha = MathF.Sin(travel * MathF.PI) * 0.85f * opacity;
        drawList.AddLine(tail, head, Tone(White, alpha), 1.4f * scale);
        drawList.AddCircleFilled(head, 1.6f * scale, Tone(glow, alpha), 8);
    }

    private static void DrawRainfall(ImDrawListPtr drawList, in Rect bounds, float scale, int dropCount, float opacity)
    {
        for (var dropIndex = 0; dropIndex < dropCount; dropIndex++)
        {
            var near = (dropIndex & 1) == 0;
            var fall = Frac(Pulse.Phase(near ? 640.0 : 980.0) + Frac(dropIndex * 0.731f + 0.13f));
            var laneX = bounds.Min.X + Frac(dropIndex * GoldenStep + 0.05f) * bounds.Width;
            var top = new Vector2(laneX - fall * bounds.Height * 0.10f,
                bounds.Min.Y + (fall * 1.3f - 0.15f) * bounds.Height);
            var length = (near ? 10f : 6.5f) * scale;
            var alpha = (near ? 0.38f : 0.22f) * opacity;
            drawList.AddLine(top, top + new Vector2(-0.12f, 1f) * length, Tone(RainTint, alpha),
                (near ? 1.5f : 1f) * scale);
        }
    }

    private static void DrawSnowfall(ImDrawListPtr drawList, in Rect bounds, float scale, float opacity)
    {
        for (var flakeIndex = 0; flakeIndex < SnowflakeCount; flakeIndex++)
        {
            var near = (flakeIndex & 1) == 0;
            var fall = Frac(Pulse.Phase(near ? 3600.0 : 5600.0) + Frac(flakeIndex * 0.731f + 0.09f));
            var laneX = bounds.Min.X + Frac(flakeIndex * GoldenStep + 0.03f) * bounds.Width;
            var sway = MathF.Sin((fall + flakeIndex * 0.7f) * MathF.PI * 2f) * bounds.Width * 0.03f;
            var position = new Vector2(laneX + sway, bounds.Min.Y + (fall * 1.25f - 0.12f) * bounds.Height);
            var alpha = (near ? 0.55f : 0.30f) * opacity;
            drawList.AddCircleFilled(position, (near ? 1.8f : 1.15f) * scale, Tone(White, alpha), 10);
        }
    }

    private static void DrawHazeBands(ImDrawListPtr drawList, in Rect bounds, Vector4 fill, int bandCount,
        float baseAlpha, float opacity)
    {
        var laneStep = 0.64f / MathF.Max(1, bandCount - 1);
        for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
        {
            var centerY = bounds.Min.Y + (0.18f + bandIndex * laneStep) * bounds.Height;
            var drift = MathF.Sin(Pulse.Phase(5200.0 + bandIndex * 1400.0) * MathF.PI * 2f + bandIndex * 1.7f) *
                        bounds.Width * 0.06f;
            var halfHeight = bounds.Height * 0.05f;
            var alpha = (baseAlpha + 0.04f * Pulse.Wave(4300.0 + bandIndex * 800.0)) * opacity;
            drawList.AddRectFilled(new Vector2(bounds.Min.X - bounds.Width * 0.08f + drift, centerY - halfHeight),
                new Vector2(bounds.Max.X + bounds.Width * 0.08f + drift, centerY + halfHeight), Tone(fill, alpha),
                halfHeight);
        }
    }

    private static void DrawWindStreams(ImDrawListPtr drawList, in Rect bounds, Vector4 stroke, float scale,
        float opacity)
    {
        const int segmentCount = 9;
        for (var streamIndex = 0; streamIndex < WindStreamCount; streamIndex++)
        {
            var progress = Frac(Pulse.Phase(4600.0 + streamIndex * 1300.0) + streamIndex * 0.31f);
            var laneY = bounds.Min.Y + (0.26f + streamIndex * 0.24f) * bounds.Height;
            var headX = bounds.Min.X + (progress * 1.6f - 0.3f) * bounds.Width;
            var tailLength = bounds.Width * 0.30f;
            var headAlpha = MathF.Sin(progress * MathF.PI) * 0.34f * opacity;
            var previous = new Vector2(headX - tailLength, laneY + WaveY(headX - tailLength, bounds, streamIndex));
            for (var segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
            {
                var pointX = headX - tailLength + tailLength * segmentIndex / segmentCount;
                var current = new Vector2(pointX, laneY + WaveY(pointX, bounds, streamIndex));
                drawList.AddLine(previous, current, Tone(stroke, headAlpha * segmentIndex / segmentCount),
                    1.4f * scale);
                previous = current;
            }
        }

        for (var moteIndex = 0; moteIndex < WindMoteCount; moteIndex++)
        {
            var progress = Frac(Pulse.Phase(2200.0 + moteIndex * 430.0) + moteIndex * 0.41f);
            var position = new Vector2(bounds.Min.X + (progress * 1.3f - 0.15f) * bounds.Width,
                bounds.Min.Y + (Frac(moteIndex * GoldenStep + 0.33f) * 0.7f + 0.15f) * bounds.Height +
                MathF.Sin(progress * MathF.PI * 3f + moteIndex) * bounds.Height * 0.04f);
            drawList.AddCircleFilled(position, 1.2f * scale,
                Tone(stroke, MathF.Sin(progress * MathF.PI) * 0.4f * opacity), 8);
        }
    }

    private static float WaveY(float pointX, in Rect bounds, int streamIndex) =>
        MathF.Sin(pointX / MathF.Max(1f, bounds.Width) * 7f + streamIndex * 1.9f) * bounds.Height * 0.02f;

    private static void DrawSandstream(ImDrawListPtr drawList, in Rect bounds, Vector4 tint, float scale,
        float opacity)
    {
        DrawHazeBands(drawList, bounds, tint, 2, 0.07f, opacity);
        for (var moteIndex = 0; moteIndex < SandMoteCount; moteIndex++)
        {
            var progress = Frac(Pulse.Phase(1500.0 + moteIndex * 260.0) + moteIndex * 0.37f);
            var laneY = bounds.Min.Y + (0.12f + Frac(moteIndex * GoldenStep + 0.24f) * 0.76f) * bounds.Height;
            var position = new Vector2(bounds.Min.X + (progress * 1.3f - 0.15f) * bounds.Width,
                laneY + MathF.Sin(progress * MathF.PI * 4f + moteIndex) * bounds.Height * 0.03f);
            var radius = (1.0f + moteIndex % 3 * 0.4f) * scale;
            drawList.AddCircleFilled(position, radius, Tone(tint, MathF.Sin(progress * MathF.PI) * 0.42f * opacity),
                8);
        }
    }

    private static void DrawHeatShimmer(ImDrawListPtr drawList, in Rect bounds, Vector4 tint, float scale,
        float opacity)
    {
        const int pointCount = 12;
        for (var rowIndex = 0; rowIndex < ShimmerRowCount; rowIndex++)
        {
            var baseY = bounds.Min.Y + (0.52f + rowIndex * 0.16f) * bounds.Height;
            var phase = Pulse.Phase(3000.0 + rowIndex * 420.0) * MathF.PI * 2f;
            var alpha = (0.10f + 0.05f * Pulse.Wave(2400.0 + rowIndex * 300.0)) * opacity;
            var color = Tone(tint, alpha);
            var previous = ShimmerPoint(bounds, baseY, 0, pointCount, phase, rowIndex);
            for (var pointIndex = 1; pointIndex < pointCount; pointIndex++)
            {
                var current = ShimmerPoint(bounds, baseY, pointIndex, pointCount, phase, rowIndex);
                drawList.AddLine(previous, current, color, 1.3f * scale);
                previous = current;
            }
        }

        for (var emberIndex = 0; emberIndex < EmberCount; emberIndex++)
        {
            var rise = Frac(Pulse.Phase(3800.0 + emberIndex * 640.0) + emberIndex * 0.29f);
            var position = new Vector2(
                bounds.Min.X + (Frac(emberIndex * GoldenStep + 0.19f) * 0.8f + 0.1f) * bounds.Width +
                MathF.Sin(rise * MathF.PI * 3f + emberIndex) * bounds.Width * 0.03f,
                bounds.Min.Y + (1.08f - 1.25f * rise) * bounds.Height);
            drawList.AddCircleFilled(position, 1.2f * scale,
                Tone(EmberTint, MathF.Sin(rise * MathF.PI) * 0.4f * opacity), 8);
        }
    }

    private static Vector2 ShimmerPoint(in Rect bounds, float baseY, int pointIndex, int pointCount, float phase,
        int rowIndex)
    {
        var fraction = pointIndex / (float)(pointCount - 1);
        return new Vector2(bounds.Min.X + fraction * bounds.Width,
            baseY + MathF.Sin(fraction * 5.2f * MathF.PI + phase + rowIndex * 1.3f) * bounds.Height * 0.022f);
    }

    private static void DrawGloomMotes(ImDrawListPtr drawList, in Rect bounds, Vector4 tint, float scale,
        float opacity)
    {
        DrawHazeBands(drawList, bounds, tint, 2, 0.05f, opacity);
        for (var moteIndex = 0; moteIndex < GloomMoteCount; moteIndex++)
        {
            var angle = Pulse.Phase(9000.0 + moteIndex * 900.0) * MathF.PI * 2f;
            var anchor = new Vector2(bounds.Min.X + Hash(moteIndex, 23.7f) * bounds.Width,
                bounds.Min.Y + (0.15f + Hash(moteIndex, 61.9f) * 0.7f) * bounds.Height);
            var position = anchor + new Vector2(MathF.Cos(angle) * bounds.Width * 0.035f,
                MathF.Sin(angle) * bounds.Height * 0.05f);
            var alpha = (0.14f + 0.14f * Pulse.Wave(2800.0 + moteIndex * 340.0)) * opacity;
            drawList.AddCircleFilled(position, (1.2f + moteIndex % 2 * 0.5f) * scale, Tone(tint, alpha), 8);
        }
    }

    private static void DrawLightningGlow(ImDrawListPtr drawList, in Rect bounds, Vector4 glow, float opacity)
    {
        var flash = FlashCurve(Pulse.Phase(LightningPeriodMs));
        if (flash <= 0.02f)
        {
            return;
        }

        drawList.AddCircleFilled(
            new Vector2(bounds.Min.X + bounds.Width * 0.30f, bounds.Min.Y + bounds.Height * 0.22f),
            bounds.Width * 0.30f, Tone(glow, 0.20f * flash * opacity), 24);
    }

    private static void DrawLightningVeil(ImDrawListPtr drawList, in Rect bounds, float rounding, float opacity)
    {
        var flash = FlashCurve(Pulse.Phase(LightningPeriodMs));
        if (flash <= 0.02f)
        {
            return;
        }

        Squircle.Fill(drawList, bounds.Min, bounds.Max, rounding, Tone(FlashFill, 0.13f * flash * opacity));
    }

    private static float FlashCurve(float cycle) =>
        MathF.Min(1f, Spike(cycle, 0.030f, 0.026f) + Spike(cycle, 0.085f, 0.050f) * 0.6f);

    private static float Spike(float cycle, float center, float width)
    {
        var distance = MathF.Abs(cycle - center);
        return distance >= width ? 0f : 1f - distance / width;
    }

    private static float Frac(float value) => value - MathF.Floor(value);

    private static uint Tone(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });
}
