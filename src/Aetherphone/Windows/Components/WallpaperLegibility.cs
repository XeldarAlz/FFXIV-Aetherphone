using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperLegibility
{
    private const float CalmBrightness = 0.30f;
    private const float HarshBrightness = 0.72f;

    private static int cachedFrame = -1;
    private static float cachedStrength;

    public static float Strength(PhoneTheme theme)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cachedStrength;
        }

        var brightness = Plugin.Wallpapers.HomeBrightness(theme.LightWallpaperId, theme.DarkWallpaperId);
        var normalized = Math.Clamp((brightness - CalmBrightness) / (HarshBrightness - CalmBrightness), 0f, 1f);
        cachedFrame = frame;
        cachedStrength = normalized * normalized * (3f - 2f * normalized);
        return cachedStrength;
    }
}
