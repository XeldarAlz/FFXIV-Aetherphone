using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Lander;

internal readonly struct LanderPadSpot
{
    public readonly float Left;
    public readonly float Right;
    public readonly float Y;
    public readonly int Multiplier;

    public LanderPadSpot(float left, float right, float surface, int multiplier)
    {
        Left = left;
        Right = right;
        Y = surface;
        Multiplier = multiplier;
    }

    public float Center => (Left + Right) * 0.5f;

    public float Width => Right - Left;

    public bool Holds(float left, float right) => left >= Left && right <= Right;
}

internal sealed class LanderTerrain
{
    public const float Width = 48f;
    public const float Height = 48f;
    public const int MaxPoints = 256;
    public const int MaxPads = 4;
    public const float MinSegment = 0.9f;
    public const float HighestGround = 15f;
    public const float LowestGround = 46.5f;
    public const float PadHighest = 29f;
    public const float PadLowest = 41f;
    private const float EdgeMargin = 4f;
    private const float PadClearance = 1.5f;
    private const float CliffInset = 2.2f;
    private const float CliffTopHighest = 14f;
    private const float CliffTopLowest = 18f;
    private const float CliffFootHighest = 22f;
    private const float CliffFootLowest = 28f;
    private const float BaseRoughness = 0.5f;
    private const float RoughnessPerLevel = 0.03f;
    private const float MaxRoughness = 0.75f;
    private const float MinRidgeGap = 3f;
    private const float RidgeLowest = 2.5f;
    private const float RidgeHighest = 8f;
    private const float RidgePerLevel = 0.6f;
    private const float RidgeSteepness = 1.6f;
    private const float MidpointJitter = 0.15f;
    private const float WidthShrinkPerLevel = 0.05f;
    private const float MinWidthShrink = 0.72f;
    private const int StackCapacity = 64;
    private static readonly int[] FourPads = { 2, 3, 4, 5 };
    private static readonly int[] ThreePads = { 2, 3, 5 };

    private readonly Vector2[] points = new Vector2[MaxPoints];
    private readonly LanderPadSpot[] pads = new LanderPadSpot[MaxPads];
    private readonly int[] multipliers = new int[MaxPads];
    private readonly Vector2[] stackStart = new Vector2[StackCapacity];
    private readonly Vector2[] stackEnd = new Vector2[StackCapacity];
    private int pointCount;
    private int padCount;

    public int Version { get; private set; }

    public ReadOnlySpan<Vector2> Points => new(points, 0, pointCount);

    public ReadOnlySpan<LanderPadSpot> Pads => new(pads, 0, padCount);

    public static int PadCountFor(int level) => level <= 2 ? 4 : 3;

    public static float PadWidth(int multiplier, int level)
    {
        var baseWidth = multiplier switch
        {
            2 => 5f,
            3 => 3.8f,
            4 => 3f,
            _ => 2.4f,
        };
        return baseWidth * MathF.Max(MinWidthShrink, 1f - WidthShrinkPerLevel * (level - 1));
    }

    public static float RoughnessFor(int level) =>
        MathF.Min(MaxRoughness, BaseRoughness + RoughnessPerLevel * (level - 1));

    public void Generate(ref GameRandom random, int level)
    {
        Version++;
        LayOutPads(ref random, level);
        pointCount = 0;
        var roughness = RoughnessFor(level);
        var leftTop = new Vector2(0f, random.Range(CliffTopHighest, CliffTopLowest));
        var leftFoot = new Vector2(CliffInset, random.Range(CliffFootHighest, CliffFootLowest));
        var rightFoot = new Vector2(Width - CliffInset, random.Range(CliffFootHighest, CliffFootLowest));
        var rightTop = new Vector2(Width, random.Range(CliffTopHighest, CliffTopLowest));
        Emit(leftTop);
        Displace(ref random, leftTop, leftFoot, roughness);
        var previous = leftFoot;
        for (var index = 0; index < padCount; index++)
        {
            var pad = pads[index];
            var padLeft = new Vector2(pad.Left, pad.Y);
            Ridge(ref random, previous, padLeft, roughness, level);
            Emit(new Vector2(pad.Right, pad.Y));
            previous = new Vector2(pad.Right, pad.Y);
        }

        Ridge(ref random, previous, rightFoot, roughness, level);
        Displace(ref random, rightFoot, rightTop, roughness);
    }

    private void Ridge(ref GameRandom random, Vector2 from, Vector2 to, float roughness, int level)
    {
        var gap = to.X - from.X;
        if (gap < MinRidgeGap)
        {
            Displace(ref random, from, to, roughness);
            return;
        }

        var peakX = from.X + gap * random.Range(0.3f, 0.7f);
        var rise = random.Range(RidgeLowest, RidgeHighest) + RidgePerLevel * (level - 1);
        var peakY = MathF.Max(HighestGround, MathF.Min(from.Y, to.Y) - MathF.Min(rise, gap * RidgeSteepness));
        var peak = new Vector2(peakX, peakY);
        Displace(ref random, from, peak, roughness);
        Displace(ref random, peak, to, roughness);
    }

    public float GroundY(float worldX)
    {
        if (pointCount == 0)
        {
            return Height;
        }

        if (worldX <= points[0].X)
        {
            return points[0].Y;
        }

        if (worldX >= points[pointCount - 1].X)
        {
            return points[pointCount - 1].Y;
        }

        var low = 0;
        var high = pointCount - 1;
        while (high - low > 1)
        {
            var middle = (low + high) >> 1;
            if (points[middle].X <= worldX)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        var start = points[low];
        var end = points[high];
        var span = end.X - start.X;
        return span <= 1e-5f ? MathF.Min(start.Y, end.Y) : start.Y + (end.Y - start.Y) * ((worldX - start.X) / span);
    }

    public float PeakBetween(float fromX, float toX)
    {
        var left = MathF.Min(fromX, toX);
        var right = MathF.Max(fromX, toX);
        var peak = MathF.Min(GroundY(left), GroundY(right));
        for (var index = 0; index < pointCount; index++)
        {
            var point = points[index];
            if (point.X > left && point.X < right)
            {
                peak = MathF.Min(peak, point.Y);
            }
        }

        return peak;
    }

    public int PadUnder(float left, float right)
    {
        for (var index = 0; index < padCount; index++)
        {
            if (pads[index].Holds(left, right))
            {
                return index;
            }
        }

        return -1;
    }

    public void Stamp(TerrainMask mask)
    {
        mask.Clear();
        StampColumns(mask, 0, mask.Width - 1, false);
    }

    public void Restore(TerrainMask mask, float fromX, float toX)
    {
        StampColumns(mask, Math.Max(0, mask.ColumnOf(fromX)), Math.Min(mask.Width - 1, mask.ColumnOf(toX)), true);
    }

    private void StampColumns(TerrainMask mask, int firstColumn, int lastColumn, bool clearFirst)
    {
        var cell = mask.MetresPerCell;
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            var left = column * cell;
            var right = (column + 1) * cell;
            if (clearFirst)
            {
                mask.CarveRect(new Rect(new Vector2(left, 0f), new Vector2(right, mask.WorldHeight)));
            }

            var ground = GroundY((column + 0.5f) * cell);
            mask.FillRect(new Rect(new Vector2(left, ground), new Vector2(right, mask.WorldHeight)));
        }
    }

    private void LayOutPads(ref GameRandom random, int level)
    {
        padCount = PadCountFor(level);
        var source = padCount == FourPads.Length ? FourPads : ThreePads;
        for (var index = 0; index < padCount; index++)
        {
            multipliers[index] = source[index];
        }

        for (var index = padCount - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (multipliers[index], multipliers[swap]) = (multipliers[swap], multipliers[index]);
        }

        var slot = (Width - EdgeMargin * 2f) / padCount;
        for (var index = 0; index < padCount; index++)
        {
            var width = PadWidth(multipliers[index], level);
            var slack = MathF.Max(0f, (slot - width) * 0.5f - PadClearance);
            var center = EdgeMargin + slot * (index + 0.5f) + random.Range(-slack, slack);
            var surface = random.Range(PadHighest, PadLowest);
            pads[index] = new LanderPadSpot(center - width * 0.5f, center + width * 0.5f, surface, multipliers[index]);
        }
    }

    private void Displace(ref GameRandom random, Vector2 from, Vector2 to, float roughness)
    {
        var top = 0;
        stackStart[top] = from;
        stackEnd[top] = to;
        top++;
        while (top > 0)
        {
            top--;
            var start = stackStart[top];
            var end = stackEnd[top];
            var span = end.X - start.X;
            if (span < MinSegment * 2f || top >= StackCapacity - 2)
            {
                Emit(end);
                continue;
            }

            var middleX = (start.X + end.X) * 0.5f + random.Range(-MidpointJitter, MidpointJitter) * span;
            var middleY = (start.Y + end.Y) * 0.5f + random.Range(-1f, 1f) * span * roughness;
            var middle = new Vector2(middleX, Math.Clamp(middleY, HighestGround, LowestGround));
            stackStart[top] = middle;
            stackEnd[top] = end;
            top++;
            stackStart[top] = start;
            stackEnd[top] = middle;
            top++;
        }
    }

    private void Emit(Vector2 point)
    {
        if (pointCount >= MaxPoints)
        {
            return;
        }

        if (pointCount > 0 && point.X <= points[pointCount - 1].X)
        {
            return;
        }

        points[pointCount++] = point;
    }
}
