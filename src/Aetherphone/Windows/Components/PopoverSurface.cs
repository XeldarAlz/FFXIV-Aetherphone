using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PopoverSurface
{
    private const float OpacityBoost = 0.4f;
    private const float MaxOpacity = 0.98f;

    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, PhoneTheme theme,
        float scale, float alpha = 1f)
    {
        Elevation.Floating(drawList, min, max, rounding, scale, alpha);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Fill(theme, alpha)));
        Material.EdgeSquircle(drawList, min, max, rounding, scale, alpha);
    }

    public static Vector4 Fill(PhoneTheme theme, float alpha) =>
        Palette.WithAlpha(theme.GroupedCard, MathF.Min(MaxOpacity, theme.GroupedCard.W + OpacityBoost) * alpha);
}
