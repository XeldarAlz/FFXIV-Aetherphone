using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Machines;

internal readonly struct MachineFooterLayout
{
    public const float PadX = 14f;
    public const float PadY = 6f;
    public const float LeftShare = 0.45f;
    public const float ColumnGap = 12f;

    public readonly Rect Left;
    public readonly Rect Right;

    private MachineFooterLayout(Rect left, Rect right)
    {
        Left = left;
        Right = right;
    }

    public static MachineFooterLayout Compute(Rect strip, float scale)
    {
        var padX = MathF.Min(PadX * scale, strip.Width * 0.1f);
        var padY = MathF.Min(PadY * scale, strip.Height * 0.2f);
        var minX = strip.Min.X + padX;
        var maxX = MathF.Max(minX, strip.Max.X - padX);
        var minY = strip.Min.Y + padY;
        var maxY = MathF.Max(minY, strip.Max.Y - padY);
        var halfGap = MathF.Min(ColumnGap * scale, (maxX - minX) * 0.1f) * 0.5f;
        var split = minX + (maxX - minX) * LeftShare;
        var left = new Rect(new Vector2(minX, minY), new Vector2(MathF.Max(minX, split - halfGap), maxY));
        var right = new Rect(new Vector2(MathF.Min(maxX, split + halfGap), minY), new Vector2(maxX, maxY));
        return new MachineFooterLayout(left, right);
    }

    public static float BlockTop(Rect column, float eyebrowHeight, float valueHeight)
    {
        return column.Center.Y - (eyebrowHeight + valueHeight) * 0.5f;
    }
}
