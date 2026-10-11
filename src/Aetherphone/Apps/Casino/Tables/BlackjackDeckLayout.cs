using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

internal static class BlackjackDeckLayout
{
    public const float UpperRowHeight = 44f;
    public const float Touch = 44f;
    public const float FitSlack = 0.5f;

    public static float DeckHeight(float composerHeight) =>
        MathF.Max(composerHeight,
            DeckActions.Pad * 2f + DeckActions.PrimaryHeight + DeckActions.Gap + UpperRowHeight);

    public static Rect UpperRow(Rect row, float scale) => DeckActions.Above(row, UpperRowHeight * scale, scale);

    public static float ButtonWidth(float labelWidth, float height, float scale) =>
        MathF.Max(Touch * scale, labelWidth + height);

    public static bool Split(Rect row, Rect upper, float secondaryLabel, float primaryLabel, float scale,
        out Rect secondary, out Rect primary)
    {
        var gap = DeckActions.Gap * scale;
        var secondaryWidth = ButtonWidth(secondaryLabel, row.Height, scale);
        var primaryWidth = ButtonWidth(primaryLabel, row.Height, scale);
        if (secondaryWidth + gap + primaryWidth <= row.Width + FitSlack)
        {
            secondary = new Rect(row.Min, new Vector2(row.Min.X + secondaryWidth, row.Max.Y));
            primary = new Rect(new Vector2(secondary.Max.X + gap, row.Min.Y), row.Max);
            return false;
        }

        secondary = upper;
        primary = row;
        return true;
    }
}
