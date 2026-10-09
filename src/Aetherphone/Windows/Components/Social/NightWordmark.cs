using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class NightWordmark
{
    private const float HeightRatio = 1.3f;
    private const int LatinLimit = 0x024F;

    public static bool Usable(string text)
    {
        if (!SeasonalTheme.Halloween || !Plugin.Fonts.DisplayReady)
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] > LatinLimit)
            {
                return false;
            }
        }

        return true;
    }

    public static bool Fits(string text, float maxWidth, float lineHeight, out Vector2 size)
    {
        size = default;
        if (!Usable(text))
        {
            return false;
        }

        size = Measure(text, lineHeight);
        return size.X <= maxWidth;
    }

    public static Vector2 Measure(string text, float lineHeight)
    {
        var pixels = lineHeight * HeightRatio;
        using (Plugin.Fonts.PushDisplay())
        {
            return ImGui.CalcTextSize(text) * (pixels / ImGui.GetFontSize());
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float lineHeight)
    {
        var pixels = lineHeight * HeightRatio;
        using (Plugin.Fonts.PushDisplay())
        {
            drawList.AddText(ImGui.GetFont(), pixels, position, ImGui.GetColorU32(color), text, 0f);
        }
    }
}
