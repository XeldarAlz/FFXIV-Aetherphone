using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TablePairLayout
{
    public const float Gap = 8f;
    public const float FitSlack = 0.5f;

    public static bool SideBySide(float width, float height, float firstLabel, float secondLabel, float scale)
    {
        var half = (width - Gap * scale) * 0.5f;
        return BlackjackDeckLayout.ButtonWidth(firstLabel, height, scale) <= half + FitSlack
            && BlackjackDeckLayout.ButtonWidth(secondLabel, height, scale) <= half + FitSlack;
    }

    public static float Height(bool sideBySide, float height, float scale) =>
        sideBySide ? height : height * 2f + Gap * scale;

    public static Rect Cell(float left, float top, float width, float height, int index, bool sideBySide,
        float scale)
    {
        var gap = Gap * scale;
        if (!sideBySide)
        {
            var y = top + index * (height + gap);
            return new Rect(new Vector2(left, y), new Vector2(left + width, y + height));
        }

        var half = (width - gap) * 0.5f;
        var x = left + index * (half + gap);
        return new Rect(new Vector2(x, top), new Vector2(x + half, top + height));
    }
}
