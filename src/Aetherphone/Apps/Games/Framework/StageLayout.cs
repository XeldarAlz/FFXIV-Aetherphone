using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Framework;

internal static class StageLayout
{
    public const float ChromeBand = 52f;
    public const float ChipRadius = 18f;
    public const float ChipInsetX = 28f;
    public const float ChipCenterY = 26f;
    public const float CoinChipGap = 8f;
    public const float PrimaryReserve = 72f;
    public const float SecondaryY = 71f;
    public const float SecondaryHeight = 28f;
    public const float SecondaryGap = 8f;
    public const float CompactPillHeight = 36f;
    public const float CompactOffset = 64f;
    public const float SafeSide = 12f;
    public const float SafeTopStandard = 96f;
    public const float SafeTopCompact = 56f;
    public const float SafeBottom = 16f;
    public const float DPadBand = 110f;
    public const float ShooterBand = 70f;

    public static float SafeTop(HudStyle style) => style == HudStyle.Compact ? SafeTopCompact : SafeTopStandard;

    public static Rect Safe(Rect full, HudStyle style, float scale)
    {
        var min = new Vector2(full.Min.X + SafeSide * scale, full.Min.Y + SafeTop(style) * scale);
        var max = new Vector2(full.Max.X - SafeSide * scale, full.Max.Y - SafeBottom * scale);
        return new Rect(min, new Vector2(MathF.Max(min.X, max.X), MathF.Max(min.Y, max.Y)));
    }

    public static Vector2 BackChipCenter(Rect full, float scale) =>
        new(full.Min.X + ChipInsetX * scale, full.Min.Y + ChipCenterY * scale);

    public static Vector2 PauseChipCenter(Rect full, float scale) =>
        new(full.Max.X - ChipInsetX * scale, full.Min.Y + ChipCenterY * scale);

    public static Vector2 PrimaryCenter(Rect full, float scale) => new(full.Center.X, full.Min.Y + ChipCenterY * scale);

    public static float PrimaryMaxWidth(Rect full, float scale) =>
        MathF.Max(0f, full.Width - 2f * PrimaryReserve * scale);

    public static float SecondaryRowY(Rect full, float scale) => full.Min.Y + SecondaryY * scale;

    public static Vector2 CompactScoreCenter(Rect full, float scale) =>
        new(full.Center.X - CompactOffset * scale, full.Min.Y + ChipCenterY * scale);

    public static Vector2 CompactSecondaryCenter(Rect full, float scale) =>
        new(full.Center.X + CompactOffset * scale, full.Min.Y + ChipCenterY * scale);

    public static Rect PadBand(Rect full, float bandHeight, float scale) =>
        new(new Vector2(full.Min.X, MathF.Max(full.Min.Y, full.Max.Y - bandHeight * scale)), full.Max);
}
