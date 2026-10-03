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
        cachedFrame = frame;
        cachedStrength = Normalize(brightness);
        return cachedStrength;
    }

    public static float Normalize(float brightness)
    {
        var normalized = Math.Clamp((brightness - CalmBrightness) / (HarshBrightness - CalmBrightness), 0f, 1f);
        return normalized * normalized * (3f - 2f * normalized);
    }
}
