using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AppBadge
{
    private const float Radius = 9f;
    private const float BorderThickness = 1.5f;
    private const int ShadowLayerCount = 4;
    private const float ShadowSpread = 1.4f;
    private const float ShadowStrength = 0.14f;
    private static readonly Vector4 ShadowColor = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 BorderColor = new(1f, 1f, 1f, 0.9f);

    public static void Draw(Vector2 center, int count, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = Radius * scale;
        DrawShadow(drawList, center, radius, scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.Danger), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(BorderColor), 24, BorderThickness * scale);
        var label = count > 99 ? "99+" : count.ToString();
        Typography.DrawCentered(center, label, theme.TextStrong, 0.7f, FontWeight.SemiBold);
    }

    private static void DrawShadow(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var shadowCenter = new Vector2(center.X, center.Y + 1.2f * scale);
        for (var layer = ShadowLayerCount; layer >= 1; layer--)
        {
            var spread = radius + layer * ShadowSpread * scale;
            var alpha = ShadowStrength * (ShadowLayerCount - layer + 1) / ShadowLayerCount;
            drawList.AddCircleFilled(shadowCenter, spread, ImGui.GetColorU32(Palette.WithAlpha(ShadowColor, alpha)), 24);
        }
    }
}
