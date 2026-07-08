using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class IconTile
{
    private const float MaxSurfaceLuminance = 0.30f;

    public static void Draw(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var drawList = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half),
            size * Metrics.Radius.TileFactor, ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.50f);
    }

    public static Vector4 Surface(Vector4 accent)
    {
        var luminance = 0.2126f * LinearChannel(accent.X) + 0.7152f * LinearChannel(accent.Y) +
                        0.0722f * LinearChannel(accent.Z);
        if (luminance <= MaxSurfaceLuminance)
        {
            return accent with { W = 1f };
        }

        var factor = MathF.Pow(MaxSurfaceLuminance / luminance, 1f / 2.4f);
        return new Vector4(accent.X * factor, accent.Y * factor, accent.Z * factor, 1f);
    }

    public static void FillShaded(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, Vector4 surface,
        float alpha = 1f)
    {
        var top = Palette.Lighten(surface, 0.10f);
        var bottom = Palette.Darken(surface, 0.14f);
        Squircle.FillVerticalGradient(drawList, min, max, radius,
            ImGui.GetColorU32(top with { W = top.W * alpha }),
            ImGui.GetColorU32(bottom with { W = bottom.W * alpha }));
    }

    private static float LinearChannel(float channel) =>
        channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
}
