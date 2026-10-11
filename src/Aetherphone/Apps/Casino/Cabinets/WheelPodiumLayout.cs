using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct WheelPodiumLayout
{
    public const float PadTop = 6f;
    public const float PadBottom = 6f;
    public const float InsetX = 4f;
    public const float HeaderHeight = 28f;
    public const float HeaderGap = 2f;
    public const float StakeHeight = 20f;
    public const float DividerGap = 5f;
    public const float TableRowHeight = 18f;

    public readonly Rect Header;
    public readonly Rect Stake;
    public readonly float DividerY;
    public readonly Rect Crowd;
    public readonly Rect Total;

    private WheelPodiumLayout(Rect header, Rect stake, float dividerY, Rect crowd, Rect total)
    {
        Header = header;
        Stake = stake;
        DividerY = dividerY;
        Crowd = crowd;
        Total = total;
    }

    public static float Height(float scale) =>
        (PadTop + HeaderHeight + HeaderGap + StakeHeight + DividerGap * 2f + TableRowHeight * 2f + PadBottom)
        * scale;

    public static WheelPodiumLayout Compute(Rect podium, float scale)
    {
        var inset = InsetX * scale;
        var left = podium.Min.X + inset;
        var right = MathF.Max(left, podium.Max.X - inset);
        var top = podium.Min.Y + PadTop * scale;
        var header = Row(left, right, ref top, HeaderHeight * scale, HeaderGap * scale);
        var stake = Row(left, right, ref top, StakeHeight * scale, DividerGap * scale);
        var dividerY = top;
        top += DividerGap * scale;
        var crowd = Row(left, right, ref top, TableRowHeight * scale, 0f);
        var total = Row(left, right, ref top, TableRowHeight * scale, 0f);
        return new WheelPodiumLayout(header, stake, dividerY, crowd, total);
    }

    private static Rect Row(float left, float right, ref float top, float height, float gap)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(right, top + height));
        top = row.Max.Y + gap;
        return row;
    }
}
