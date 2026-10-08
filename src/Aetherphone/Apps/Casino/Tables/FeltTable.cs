using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class FeltTable
{
    public const float LampHeightFraction = 0.40f;
    public const float LampCenterLuminance = 0.22f;

    private const int PoolLayers = 7;
    private const float PoolRadiusFraction = 0.75f;
    private const float PoolAlpha = 0.045f;
    private const float RailWidth = 8f;
    private const float StitchInset = 14f;
    private const float VignetteDepth = 0.18f;

    private static readonly Vector4 Lamp = new(1f, 0.90f, 0.70f, 1f);
    private static readonly Vector4 RailInk = new(0.13f, 0.07f, 0.04f, 1f);
    private static readonly Vector4 RailShine = new(1f, 0.80f, 0.50f, 0.22f);
    private static readonly Vector4 StitchInk = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 Edge = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 EdgeDark = new(0f, 0f, 0f, 0.45f);

    public static void Draw(ImDrawListPtr drawList, in Rect full, float scale)
    {
        drawList.AddRectFilledMultiColor(full.Min, full.Max, ImGui.GetColorU32(CasinoColors.FeltTop),
            ImGui.GetColorU32(CasinoColors.FeltTop), ImGui.GetColorU32(CasinoColors.FeltBottom),
            ImGui.GetColorU32(CasinoColors.FeltBottom));
        DrawLampPool(drawList, full);
        DrawVignette(drawList, full);
        var rail = RailWidth * scale;
        var ink = ImGui.GetColorU32(RailInk);
        drawList.AddRectFilled(full.Min, new Vector2(full.Min.X + rail, full.Max.Y), ink);
        drawList.AddRectFilled(new Vector2(full.Max.X - rail, full.Min.Y), full.Max, ink);
        var shine = ImGui.GetColorU32(RailShine);
        var thin = MathF.Max(1f, Metrics.Stroke.Thin * scale);
        drawList.AddLine(new Vector2(full.Min.X + rail, full.Min.Y), new Vector2(full.Min.X + rail, full.Max.Y),
            shine, thin);
        drawList.AddLine(new Vector2(full.Max.X - rail, full.Min.Y), new Vector2(full.Max.X - rail, full.Max.Y),
            shine, thin);
        var stitch = (RailWidth + StitchInset) * scale;
        var stitchInk = ImGui.GetColorU32(StitchInk);
        drawList.AddLine(new Vector2(full.Min.X + stitch, full.Min.Y), new Vector2(full.Min.X + stitch, full.Max.Y),
            stitchInk, thin);
        drawList.AddLine(new Vector2(full.Max.X - stitch, full.Min.Y), new Vector2(full.Max.X - stitch, full.Max.Y),
            stitchInk, thin);
    }

    private static void DrawLampPool(ImDrawListPtr drawList, in Rect full)
    {
        var center = new Vector2(full.Center.X, full.Min.Y + full.Height * LampHeightFraction);
        var span = MathF.Max(full.Width, full.Height * 0.6f) * PoolRadiusFraction;
        for (var layer = PoolLayers; layer >= 1; layer--)
        {
            var radius = span * (layer / (float)PoolLayers);
            var alpha = PoolAlpha * (PoolLayers - layer + 1) / PoolLayers;
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(Lamp, alpha)), 64);
        }
    }

    private static void DrawVignette(ImDrawListPtr drawList, in Rect full)
    {
        var depth = full.Height * VignetteDepth;
        var clear = ImGui.GetColorU32(Edge);
        var dark = ImGui.GetColorU32(EdgeDark);
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Max.X, full.Min.Y + depth), dark, dark, clear,
            clear);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, full.Max.Y - depth), full.Max, clear, clear, dark,
            dark);
    }
}
