using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct WheelPodiumLayout
{
    public const float Pad = 6f;
    public const float RowGap = 4f;
    public const float HeaderHeight = 26f;
    public const float BetHeight = 22f;
    public const float BackHeight = 18f;

    public readonly Rect Header;
    public readonly Rect Bet;
    public readonly Rect Crowd;
    public readonly Rect Back;

    private WheelPodiumLayout(Rect header, Rect bet, Rect crowd, Rect back)
    {
        Header = header;
        Bet = bet;
        Crowd = crowd;
        Back = back;
    }

    public static float Height(float scale) =>
        (Pad * 2f + HeaderHeight + BetHeight + BackHeight + RowGap * 3f) * scale + WheelCrowdArt.Height(scale);

    public static WheelPodiumLayout Compute(Rect podium, float scale)
    {
        var pad = Pad * scale;
        var gap = RowGap * scale;
        var left = podium.Min.X + pad * 0.5f;
        var right = MathF.Max(left, podium.Max.X - pad * 0.5f);
        var top = podium.Min.Y + pad;
        var header = Row(left, right, ref top, HeaderHeight * scale, gap);
        var bet = Row(left, right, ref top, BetHeight * scale, gap);
        var crowd = Row(left, right, ref top, WheelCrowdArt.Height(scale), gap);
        var back = Row(left, right, ref top, BackHeight * scale, gap);
        return new WheelPodiumLayout(header, bet, crowd, back);
    }

    private static Rect Row(float left, float right, ref float top, float height, float gap)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(right, top + height));
        top = row.Max.Y + gap;
        return row;
    }
}
