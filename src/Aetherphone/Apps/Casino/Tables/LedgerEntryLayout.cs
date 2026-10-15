using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

internal static class LedgerEntryLayout
{
    public const float PadX = 14f;
    public const float PadY = 11f;
    public const float Icon = 36f;
    public const float Gap = 12f;
    public const float LineGap = 2f;
    public const float PillGap = 3f;
    public const float RowGap = 8f;
    public const float PillPadX = 6f;
    public const float PillPadY = 2f;

    public static float Height(float nameHeight, float metaHeight, float amountHeight, float pillHeight, float scale)
    {
        var text = nameHeight + LineGap * scale + metaHeight;
        var value = amountHeight + PillGap * scale + pillHeight;
        return MathF.Max(Icon * scale, MathF.Max(text, value)) + PadY * 2f * scale;
    }

    public static float PillHeight(float lineHeight, float scale) => lineHeight + PillPadY * 2f * scale;

    public static Vector2 IconCenter(in Rect row, float scale) =>
        new(row.Min.X + PadX * scale + Icon * 0.5f * scale, row.Center.Y);

    public static float TextLeft(in Rect row, float scale) => row.Min.X + (PadX + Icon + Gap) * scale;

    public static float TextWidth(in Rect row, float valueWidth, float scale) =>
        MathF.Max(1f, row.Max.X - PadX * scale - valueWidth - Gap * scale - TextLeft(row, scale));

    public static float ValueRight(in Rect row, float scale) => row.Max.X - PadX * scale;
}
