using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Thrust;

internal enum ThrustPattern : byte
{
    Rest,
    Post,
    Beam,
    Slant,
    Gate,
    Slalom,
    Spinner,
    Stairs,
}

internal struct ThrustZapper
{
    public Vector2 Center;
    public float HalfLength;
    public float Angle;
    public float Spin;
    public float Closest;
    public bool Passed;

    public readonly bool Rotating => Spin != 0f;

    public readonly Vector2 Direction(float time) =>
        new(MathF.Cos(Angle + Spin * time), MathF.Sin(Angle + Spin * time));

    public readonly float Left => Rotating ? Center.X - HalfLength : Center.X - MathF.Abs(MathF.Cos(Angle)) * HalfLength;

    public readonly float Right => Rotating ? Center.X + HalfLength : Center.X + MathF.Abs(MathF.Cos(Angle)) * HalfLength;
}

internal static class ThrustGenerator
{
    public const float Height = 10f;
    public const float ChunkWidth = 12f;
    public const float PlayerRadius = 0.42f;
    public const float ZapperRadius = 0.26f;
    public const float CoinRadius = 0.3f;
    public const float MinGap = 1.3f;
    public const float MaxShift = 3f;
    public const float ColumnStep = 0.25f;
    public const int MaxZappers = 4;
    public const int MaxCoins = 16;
    private const float SampleStep = 0.1f;
    private const float CoinSpacing = 0.8f;
    private const float CoinClearance = ZapperRadius + CoinRadius + 0.25f;
    private const int Attempts = 8;
    private const float FirstTierDistance = 300f;
    private const float SecondTierDistance = 1000f;

    private static readonly ThrustPattern[] TierZero =
    {
        ThrustPattern.Rest, ThrustPattern.Post, ThrustPattern.Post, ThrustPattern.Beam, ThrustPattern.Beam,
        ThrustPattern.Slant,
    };

    private static readonly ThrustPattern[] TierOne =
    {
        ThrustPattern.Rest, ThrustPattern.Post, ThrustPattern.Beam, ThrustPattern.Slant, ThrustPattern.Slant,
        ThrustPattern.Gate, ThrustPattern.Gate, ThrustPattern.Gate, ThrustPattern.Slalom, ThrustPattern.Slalom,
    };

    private static readonly ThrustPattern[] TierTwo =
    {
        ThrustPattern.Rest, ThrustPattern.Post, ThrustPattern.Beam, ThrustPattern.Slant, ThrustPattern.Gate,
        ThrustPattern.Gate, ThrustPattern.Slalom, ThrustPattern.Spinner, ThrustPattern.Spinner, ThrustPattern.Stairs,
        ThrustPattern.Stairs,
    };

    public static float Inflate => ZapperRadius + PlayerRadius;

    public static int Tier(float startX)
    {
        if (startX < FirstTierDistance)
        {
            return 0;
        }

        return startX < SecondTierDistance ? 1 : 2;
    }

    public static int Generate(ref GameRandom random, int ordinal, float startX, ref float pathY, Span<ThrustZapper> zappers,
        Span<Vector2> coins, out int coinCount, out ThrustPattern pattern)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            pattern = ordinal == 0 ? ThrustPattern.Rest : Pick(ref random, Tier(startX));
            var count = Build(pattern, ref random, startX, pathY, zappers, out var nextPathY);
            var placed = zappers[..count];
            if (!Passable(placed, startX))
            {
                continue;
            }

            coinCount = PlaceCoins(pattern, ref random, startX, pathY, nextPathY, placed, coins);
            pathY = nextPathY;
            return count;
        }

        pattern = ThrustPattern.Rest;
        coinCount = PlaceCoins(ThrustPattern.Rest, ref random, startX, pathY, pathY, ReadOnlySpan<ThrustZapper>.Empty, coins);
        return 0;
    }

    public static bool Passable(ReadOnlySpan<ThrustZapper> zappers, float startX)
    {
        for (var x = startX; x <= startX + ChunkWidth; x += ColumnStep)
        {
            if (LargestGap(zappers, x, 0f, true) < MinGap)
            {
                return false;
            }
        }

        return true;
    }

    public static float LargestGap(ReadOnlySpan<ThrustZapper> zappers, float x, float time, bool sweep)
    {
        var best = 0f;
        var runStart = float.NaN;
        var top = PlayerRadius;
        var bottom = Height - PlayerRadius;
        for (var y = top; y <= bottom + 0.001f; y += SampleStep)
        {
            if (Blocked(zappers, new Vector2(x, y), Inflate, time, sweep))
            {
                if (!float.IsNaN(runStart))
                {
                    best = MathF.Max(best, y - SampleStep - runStart);
                    runStart = float.NaN;
                }

                continue;
            }

            if (float.IsNaN(runStart))
            {
                runStart = y;
            }
        }

        if (!float.IsNaN(runStart))
        {
            best = MathF.Max(best, bottom - runStart);
        }

        return best;
    }

    public static bool Blocked(ReadOnlySpan<ThrustZapper> zappers, Vector2 point, float inflate, float time, bool sweep)
    {
        for (var index = 0; index < zappers.Length; index++)
        {
            ref readonly var zapper = ref zappers[index];
            if (zapper.Rotating && sweep)
            {
                if (Vector2.Distance(point, zapper.Center) <= zapper.HalfLength + inflate)
                {
                    return true;
                }

                continue;
            }

            if (SegmentDistance(point, zapper, time) <= inflate)
            {
                return true;
            }
        }

        return false;
    }

    public static float SegmentDistance(Vector2 point, in ThrustZapper zapper, float time)
    {
        var direction = zapper.Direction(time);
        var start = zapper.Center - direction * zapper.HalfLength;
        var along = Math.Clamp(Vector2.Dot(point - start, direction), 0f, zapper.HalfLength * 2f);
        return Vector2.Distance(point, start + direction * along);
    }

    private static ThrustPattern Pick(ref GameRandom random, int tier)
    {
        var table = tier switch
        {
            0 => TierZero,
            1 => TierOne,
            _ => TierTwo,
        };
        return table[random.Next(table.Length)];
    }

    private static int Build(ThrustPattern pattern, ref GameRandom random, float startX, float pathY,
        Span<ThrustZapper> zappers, out float nextPathY)
    {
        nextPathY = pathY;
        var middle = startX + ChunkWidth * 0.5f;
        switch (pattern)
        {
            case ThrustPattern.Post:
            {
                var length = random.Range(3f, 4.6f);
                var anchor = random.Next(3);
                var center = anchor switch
                {
                    0 => length * 0.5f,
                    1 => Height - length * 0.5f,
                    _ => random.Range(length * 0.5f + 1.5f, Height - length * 0.5f - 1.5f),
                };
                zappers[0] = Straight(middle + random.Range(-1.5f, 1.5f), center, length, MathF.PI * 0.5f);
                nextPathY = OpenSide(center, length, pathY);
                return 1;
            }
            case ThrustPattern.Beam:
            {
                var length = random.Range(3f, 5f);
                zappers[0] = Straight(middle, random.Range(1.8f, Height - 1.8f), length, 0f);
                return 1;
            }
            case ThrustPattern.Slant:
            {
                var length = random.Range(3.2f, 4.8f);
                var angle = random.Chance(0.5f) ? MathF.PI * 0.25f : -MathF.PI * 0.25f;
                zappers[0] = Straight(middle, random.Range(2.6f, Height - 2.6f), length, angle);
                return 1;
            }
            case ThrustPattern.Gate:
            {
                var gap = Tier(startX) >= 2 ? random.Range(2.9f, 3.3f) : random.Range(3.3f, 3.8f);
                var low = MathF.Max(gap * 0.5f + 0.4f, pathY - MaxShift);
                var high = MathF.Min(Height - gap * 0.5f - 0.4f, pathY + MaxShift);
                var center = random.Range(MathF.Min(low, high), MathF.Max(low, high));
                var topLength = center - gap * 0.5f;
                var bottomLength = Height - (center + gap * 0.5f);
                zappers[0] = Straight(middle, topLength * 0.5f, topLength, MathF.PI * 0.5f);
                zappers[1] = Straight(middle, Height - bottomLength * 0.5f, bottomLength, MathF.PI * 0.5f);
                nextPathY = center;
                return 2;
            }
            case ThrustPattern.Slalom:
            {
                var length = random.Range(4.6f, 5.6f);
                var ceilingFirst = random.Chance(0.5f);
                zappers[0] = Straight(startX + 2.8f, ceilingFirst ? length * 0.5f : Height - length * 0.5f, length,
                    MathF.PI * 0.5f);
                zappers[1] = Straight(startX + ChunkWidth - 2.8f, ceilingFirst ? Height - length * 0.5f : length * 0.5f,
                    length, MathF.PI * 0.5f);
                nextPathY = ceilingFirst ? length * 0.5f : Height - length * 0.5f;
                return 2;
            }
            case ThrustPattern.Spinner:
            {
                var halfLength = random.Range(1.6f, 2.3f);
                var spin = random.Range(0.9f, 1.6f) * random.Sign();
                var zapper = Straight(middle, random.Range(halfLength + 1.6f, Height - halfLength - 1.6f), halfLength * 2f,
                    random.NextFloat() * MathF.PI);
                zapper.Spin = spin;
                zappers[0] = zapper;
                return 1;
            }
            case ThrustPattern.Stairs:
            {
                var descending = random.Chance(0.5f);
                var angle = descending ? MathF.PI * 0.25f : -MathF.PI * 0.25f;
                for (var step = 0; step < 3; step++)
                {
                    var y = descending ? 2.2f + step * 2.6f : Height - 2.2f - step * 2.6f;
                    zappers[step] = Straight(startX + 2f + step * 4f, y, 2.4f, angle);
                }

                return 3;
            }
            default:
                return 0;
        }
    }

    private static float OpenSide(float center, float length, float pathY)
    {
        var top = center - length * 0.5f;
        var bottom = center + length * 0.5f;
        var above = top * 0.5f;
        var below = (bottom + Height) * 0.5f;
        if (top < 1.5f)
        {
            return below;
        }

        if (bottom > Height - 1.5f)
        {
            return above;
        }

        return MathF.Abs(above - pathY) < MathF.Abs(below - pathY) ? above : below;
    }

    private static ThrustZapper Straight(float x, float y, float length, float angle) => new()
    {
        Center = new Vector2(x, y),
        HalfLength = length * 0.5f,
        Angle = angle,
        Spin = 0f,
        Closest = float.MaxValue,
        Passed = false,
    };

    private static int PlaceCoins(ThrustPattern pattern, ref GameRandom random, float startX, float pathY, float nextPathY,
        ReadOnlySpan<ThrustZapper> zappers, Span<Vector2> coins)
    {
        var count = 0;
        var wave = pattern == ThrustPattern.Rest;
        var total = wave ? 12 : 6;
        var phase = random.NextFloat() * MathF.Tau;
        var amplitude = wave ? random.Range(0.8f, 2f) : 0f;
        var firstX = wave ? startX + 1.2f : startX + (pattern == ThrustPattern.Gate ? ChunkWidth * 0.5f - 2f : 0.6f);
        for (var coin = 0; coin < total && count < coins.Length; coin++)
        {
            var x = firstX + coin * CoinSpacing;
            var progress = coin / (float)(total - 1);
            var baseY = pattern == ThrustPattern.Gate ? nextPathY : pathY + (nextPathY - pathY) * progress * 0.3f;
            var y = Math.Clamp(baseY + MathF.Sin(phase + coin * 0.55f) * amplitude, 0.8f, Height - 0.8f);
            var position = new Vector2(x, y);
            if (Blocked(zappers, position, CoinClearance, 0f, true))
            {
                continue;
            }

            coins[count++] = position;
        }

        return count;
    }
}
