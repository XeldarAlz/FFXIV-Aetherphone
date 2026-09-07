using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AccentGloss
{
    public const float GlowReach = 10f;

    private const int GlowRings = 10;
    private const float GlowRingAlpha = 0.035f;
    private const float SheenAlpha = 0.10f;
    private const float SheenCoverage = 0.55f;
    private const float SheenLift = 0.42f;
    private const float SheenSpan = 0.55f;
    private const int CircleSegments = 48;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static void Circle(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 top, Vector4 bottom,
        float scale, float eased, float glowReach = GlowReach)
    {
        if (glowReach > 0f)
        {
            var ringColor = ImGui.GetColorU32(Palette.WithAlpha(top, GlowRingAlpha * (1f + eased)));
            for (var ring = GlowRings; ring >= 1; ring--)
            {
                drawList.AddCircleFilled(center, radius + glowReach * scale * ring / GlowRings, ringColor,
                    CircleSegments);
            }
        }

        Squircle.FillCircleVerticalGradient(drawList, center, radius, ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom));
        drawList.AddCircleFilled(center - new Vector2(0f, radius * SheenLift), radius * SheenSpan,
            ImGui.GetColorU32(Palette.WithAlpha(White, SheenAlpha * top.W)), 32);
    }

    public static void Pill(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector4 top, Vector4 bottom,
        float scale, float eased, float glowReach = GlowReach)
    {
        var rounding = (max.Y - min.Y) * 0.5f;
        if (glowReach > 0f)
        {
            var ringColor = ImGui.GetColorU32(Palette.WithAlpha(top, GlowRingAlpha * (1f + eased)));
            for (var ring = GlowRings; ring >= 1; ring--)
            {
                var reach = glowReach * scale * ring / GlowRings;
                var spread = new Vector2(reach, reach);
                Squircle.Fill(drawList, min - spread, max + spread, rounding + reach, ringColor);
            }
        }

        Squircle.FillVerticalGradient(drawList, min, max, rounding, ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom));
        Material.TopGlow(drawList, min, max, rounding, White, SheenCoverage, SheenAlpha * top.W);
    }
}
