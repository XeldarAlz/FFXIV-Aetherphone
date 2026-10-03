using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal static class SwoopSky
{
    private const int StarCount = 72;
    private const int CloudCount = 6;
    private const int LayerCount = 3;
    private const int LayerColumns = 36;
    private const float CloudParallax = 0.05f;
    private static readonly float[] StarX = new float[StarCount];
    private static readonly float[] StarY = new float[StarCount];
    private static readonly float[] StarSize = new float[StarCount];
    private static readonly float[] StarPhase = new float[StarCount];
    private static readonly float[] CloudY = { 0.09f, 0.16f, 0.24f, 0.31f, 0.13f, 0.37f };
    private static readonly float[] CloudSize = { 0.09f, 0.07f, 0.11f, 0.06f, 0.05f, 0.08f };
    private static readonly float[] CloudOffset = { 0.05f, 0.42f, 0.78f, 1.12f, 0.95f, 0.25f };
    private static readonly float[] CloudDrift = { 9f, 6f, 11f, 5f, 7f, 8f };
    private static readonly float[] LayerParallax = { 0.12f, 0.24f, 0.42f };
    private static readonly float[] LayerBase = { 0.50f, 0.58f, 0.66f };
    private static readonly float[] LayerAmplitude = { 0.07f, 0.065f, 0.06f };
    private static readonly float[] LayerWavelength = { 1.1f, 0.8f, 0.6f };
    private static readonly float[] LayerHaze = { 0.62f, 0.42f, 0.24f };
    private static readonly float[] LayerPhase = { 0.4f, 2.1f, 4.4f };
    private static readonly Vector4[] LayerColor =
    {
        new(0.42f, 0.58f, 0.70f, 1f), new(0.36f, 0.60f, 0.52f, 1f), new(0.30f, 0.58f, 0.40f, 1f),
    };

    static SwoopSky()
    {
        var random = new SwoopRandom(0xC0FFEEu);
        for (var star = 0; star < StarCount; star++)
        {
            StarX[star] = random.NextUnit();
            StarY[star] = random.NextUnit() * random.NextUnit() * 0.62f;
            StarSize[star] = 0.6f + random.NextUnit() * random.NextUnit() * 1.4f;
            StarPhase[star] = random.NextUnit() * MathF.Tau;
        }
    }

    public static void Draw(ImDrawListPtr drawList, Rect area, in SwoopCamera camera, in SwoopLighting lighting, float time,
        float scale)
    {
        var top = ImGui.GetColorU32(lighting.SkyTop);
        var bottom = ImGui.GetColorU32(lighting.SkyBottom);
        drawList.AddRectFilledMultiColor(area.Min, area.Max, top, top, bottom, bottom);
        DrawStars(drawList, area, lighting, time, scale);
        DrawSun(drawList, area, lighting);
        DrawClouds(drawList, area, camera, lighting, time);
        for (var layer = 0; layer < LayerCount; layer++)
        {
            DrawLayer(drawList, area, camera, lighting, layer);
        }
    }

    private static void DrawStars(ImDrawListPtr drawList, Rect area, in SwoopLighting lighting, float time, float scale)
    {
        var visibility = SwoopShapes.Smooth(0.8f, 0.97f, lighting.Progress);
        if (visibility <= 0f)
        {
            return;
        }

        for (var star = 0; star < StarCount; star++)
        {
            var twinkle = 0.55f + 0.45f * MathF.Sin(time * (1.3f + StarSize[star]) + StarPhase[star]);
            var position = new Vector2(area.Min.X + StarX[star] * area.Width, area.Min.Y + StarY[star] * area.Height);
            var color = new Vector4(1f, 0.97f, 0.9f, visibility * twinkle);
            drawList.AddCircleFilled(position, StarSize[star] * scale, ImGui.GetColorU32(color), 8);
            if (StarSize[star] > 1.5f)
            {
                var arm = StarSize[star] * 2.6f * scale * twinkle;
                var line = ImGui.GetColorU32(color with { W = color.W * 0.5f });
                drawList.AddLine(position - new Vector2(arm, 0f), position + new Vector2(arm, 0f), line, MathF.Max(1f, scale * 0.8f));
                drawList.AddLine(position - new Vector2(0f, arm), position + new Vector2(0f, arm), line, MathF.Max(1f, scale * 0.8f));
            }
        }
    }

    private static void DrawSun(ImDrawListPtr drawList, Rect area, in SwoopLighting lighting)
    {
        var progress = lighting.Progress;
        var sun = new Vector2(area.Min.X + area.Width * (0.78f - 0.16f * progress),
            area.Min.Y + area.Height * (0.12f + 0.68f * progress * progress));
        var radius = area.Width * (0.075f + 0.03f * progress);
        var horizon = lighting.SkyBottom with { W = 1f };
        SwoopShapes.Glow(drawList, sun, radius * (4.2f + 2f * lighting.Golden), SwoopShapes.Mix(lighting.Sun, horizon, 0.3f),
            0.9f + lighting.Golden);
        SwoopShapes.Glow(drawList, sun, radius * 2f, lighting.Sun, 1.4f);
        drawList.AddCircleFilled(sun, radius, ImGui.GetColorU32(lighting.Sun), 40);
        drawList.AddCircleFilled(sun - new Vector2(radius * 0.18f, radius * 0.2f), radius * 0.62f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 0.95f, 0.35f * (1f - lighting.Night))), 32);
        if (lighting.Golden > 0f)
        {
            var band = ImGui.GetColorU32(lighting.Sun with { W = 0.28f * lighting.Golden });
            var clear = ImGui.GetColorU32(lighting.Sun with { W = 0f });
            var bandTop = new Vector2(area.Min.X, sun.Y - area.Height * 0.18f);
            drawList.AddRectFilledMultiColor(bandTop, new Vector2(area.Max.X, sun.Y), clear, clear, band, band);
            drawList.AddRectFilledMultiColor(new Vector2(area.Min.X, sun.Y), new Vector2(area.Max.X, sun.Y + area.Height * 0.2f), band,
                band, clear, clear);
        }

        if (lighting.Night <= 0f)
        {
            return;
        }

        var moon = new Vector2(area.Min.X + area.Width * 0.24f, area.Min.Y + area.Height * (0.2f - 0.06f * lighting.Night));
        var moonRadius = area.Width * 0.055f;
        var moonColor = new Vector4(0.96f, 0.95f, 0.86f, lighting.Night);
        SwoopShapes.Glow(drawList, moon, moonRadius * 3.4f, moonColor, 0.8f * lighting.Night);
        drawList.AddCircleFilled(moon, moonRadius, ImGui.GetColorU32(moonColor), 36);
        drawList.AddCircleFilled(moon + new Vector2(moonRadius * 0.42f, -moonRadius * 0.2f), moonRadius * 0.86f,
            ImGui.GetColorU32(lighting.SkyTop with { W = lighting.Night }), 36);
    }

    private static void DrawClouds(ImDrawListPtr drawList, Rect area, in SwoopCamera camera, in SwoopLighting lighting, float time)
    {
        var span = area.Width * 1.6f;
        var shift = (float)(camera.FocusX * camera.BasePixelsPerMeter * CloudParallax % span);
        var tone = SwoopShapes.Mix(new Vector4(1f, 1f, 1f, 1f), lighting.SkyBottom, 0.35f + 0.4f * lighting.Golden);
        tone = SwoopShapes.Multiply(tone, SwoopShapes.Mix(new Vector4(1f, 1f, 1f, 1f), lighting.Tint, 0.6f));
        var alpha = 0.88f - 0.5f * lighting.Night;
        var body = ImGui.GetColorU32(tone with { W = alpha });
        var shade = ImGui.GetColorU32(SwoopShapes.Multiply(tone, new Vector4(0.82f, 0.82f, 0.92f, 1f)) with { W = alpha });
        for (var cloud = 0; cloud < CloudCount; cloud++)
        {
            var travel = CloudOffset[cloud] * span - shift - time * CloudDrift[cloud];
            var wrapped = travel - MathF.Floor(travel / span) * span;
            var center = new Vector2(area.Min.X - area.Width * 0.3f + wrapped, area.Min.Y + CloudY[cloud] * area.Height);
            DrawCloud(drawList, center, CloudSize[cloud] * area.Width, body, shade);
        }
    }

    private static void DrawCloud(ImDrawListPtr drawList, Vector2 center, float radius, uint body, uint shade)
    {
        var lift = new Vector2(0f, radius * 0.18f);
        drawList.AddCircleFilled(center + lift + new Vector2(radius * 0.95f, radius * 0.2f), radius * 0.72f, shade, 24);
        drawList.AddCircleFilled(center + lift - new Vector2(radius * 0.95f, -radius * 0.25f), radius * 0.66f, shade, 24);
        drawList.AddRectFilled(center + new Vector2(-radius * 1.5f, radius * 0.1f), center + new Vector2(radius * 1.6f, radius * 0.9f), shade,
            radius * 0.4f);
        drawList.AddCircleFilled(center, radius, body, 28);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.95f, radius * 0.2f), radius * 0.72f, body, 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.95f, -radius * 0.25f), radius * 0.66f, body, 24);
        drawList.AddRectFilled(center + new Vector2(-radius * 1.5f, radius * 0.05f), center + new Vector2(radius * 1.6f, radius * 0.72f), body,
            radius * 0.34f);
    }

    private static void DrawLayer(ImDrawListPtr drawList, Rect area, in SwoopCamera camera, in SwoopLighting lighting, int layer)
    {
        var wavelength = LayerWavelength[layer] * area.Width;
        var shift = camera.FocusX * camera.BasePixelsPerMeter * LayerParallax[layer];
        var vertical = Math.Clamp(camera.FocusY * camera.BasePixelsPerMeter * LayerParallax[layer] * 0.4f, -area.Height * 0.2f,
            area.Height * 0.2f);
        var zoomSink = (camera.Zoom - 1f) * area.Height * 0.03f * (layer + 1);
        var baseY = area.Min.Y + area.Height * LayerBase[layer] + vertical + zoomSink;
        var amplitude = area.Height * LayerAmplitude[layer];
        var tone = SwoopShapes.Mix(LayerColor[layer], lighting.SkyBottom, LayerHaze[layer]);
        tone = SwoopShapes.Multiply(tone, lighting.Tint);
        var fill = ImGui.GetColorU32(tone);
        var crest = ImGui.GetColorU32(SwoopShapes.Mix(tone, new Vector4(1f, 1f, 1f, 1f), 0.12f));
        var step = area.Width / LayerColumns;
        var previous = new Vector2(area.Min.X, LayerHeight(baseY, amplitude, wavelength, shift, 0f, layer));
        for (var column = 1; column <= LayerColumns; column++)
        {
            var offset = column * step;
            var current = new Vector2(area.Min.X + offset, LayerHeight(baseY, amplitude, wavelength, shift, offset, layer));
            drawList.AddQuadFilled(previous, current, new Vector2(current.X, area.Max.Y), new Vector2(previous.X, area.Max.Y), fill);
            drawList.AddLine(previous, current, crest, MathF.Max(1f, amplitude * 0.05f));
            previous = current;
        }
    }

    private static float LayerHeight(float baseY, float amplitude, float wavelength, double shift, float offset, int layer)
    {
        var phase = (offset + shift) / wavelength * Math.Tau + LayerPhase[layer];
        var wave = 0.62 * Math.Sin(phase) + 0.28 * Math.Sin(phase * 2.13 + 1.7) + 0.1 * Math.Sin(phase * 4.7 + 0.3);
        return baseY - amplitude * (float)wave;
    }
}
