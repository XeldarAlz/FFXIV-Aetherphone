using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal static class SwoopSky
{
    private const int LayerCount = 3;
    private const int LayerColumns = 36;
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

    public static void DrawHills(ImDrawListPtr drawList, Rect area, Vector2 origin, float basePixelsPerMeter,
        float zoomOut, in SwoopLighting lighting)
    {
        for (var layer = 0; layer < LayerCount; layer++)
        {
            DrawLayer(drawList, area, origin, basePixelsPerMeter, zoomOut, lighting, layer);
        }
    }

    private static void DrawLayer(ImDrawListPtr drawList, Rect area, Vector2 origin, float basePixelsPerMeter,
        float zoomOut, in SwoopLighting lighting, int layer)
    {
        var wavelength = LayerWavelength[layer] * area.Width;
        var shift = origin.X * basePixelsPerMeter * LayerParallax[layer];
        var vertical = Math.Clamp(-origin.Y * basePixelsPerMeter * LayerParallax[layer] * 0.4f, -area.Height * 0.2f,
            area.Height * 0.2f);
        var zoomSink = (zoomOut - 1f) * area.Height * 0.03f * (layer + 1);
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
