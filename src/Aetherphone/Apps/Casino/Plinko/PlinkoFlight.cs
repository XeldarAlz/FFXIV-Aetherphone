using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Plinko;

internal enum PlinkoEventKind : byte
{
    Peg,
    Landed,
}

internal readonly record struct PlinkoEvent(
    PlinkoEventKind Kind,
    int Ball,
    int Row,
    int Column,
    PlinkoDrop Drop);

internal readonly record struct PlinkoDrop(
    int Rows,
    int Risk,
    int Slot,
    int Tenths,
    long Stake,
    long Payout,
    string RoundId)
{
    public bool Edge => Slot == 0 || Slot == Rows;

    public bool Won => Payout > Stake;
}

internal struct PlinkoBall
{
    public bool Active;
    public PlinkoDrop Drop;
    public int PathBits;
    public float Elapsed;
    public int Reported;
}

internal sealed class PlinkoFlight
{
    public const int Capacity = PlinkoRules.MaxInFlight;
    public const float SegmentSeconds = 0.22f;
    public const float MaxWobble = 0.09f;
    public const float ArcHeight = 0.32f;

    private const int MaxSegments = PlinkoRules.MaxRows + 1;
    private const int EventCapacity = Capacity * (MaxSegments + 1);
    private const uint FnvOffset = 2166136261;
    private const uint FnvPrime = 16777619;

    private readonly PlinkoBall[] balls = new PlinkoBall[Capacity];
    private readonly float[] wobbles = new float[Capacity * MaxSegments];
    private readonly PlinkoEvent[] events = new PlinkoEvent[EventCapacity];
    private int eventCount;

    public int ActiveCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < balls.Length; index++)
            {
                if (balls[index].Active)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int EventCount => eventCount;

    public static float FlightSeconds(int rows) => (rows + 1) * SegmentSeconds;

    public static ulong SeedOf(string roundId)
    {
        var hash = FnvOffset;
        for (var index = 0; index < roundId.Length; index++)
        {
            hash = (hash ^ roundId[index]) * FnvPrime;
        }

        return ((ulong)hash << 32) | (hash ^ 0x9E3779B9u);
    }

    public bool IsActive(int ball) => ball >= 0 && ball < balls.Length && balls[ball].Active;

    public ref readonly PlinkoBall Ball(int ball) => ref balls[ball];

    public PlinkoEvent Event(int index) => events[index];

    public void Clear()
    {
        Array.Clear(balls);
        eventCount = 0;
    }

    public bool Launch(in PlinkoDrop drop, ReadOnlySpan<int> path, ulong seed, float startSeconds, out int ball)
    {
        ball = -1;
        if (!PlinkoRules.IsPath(path, drop.Rows, drop.Slot))
        {
            return false;
        }

        for (var index = 0; index < balls.Length; index++)
        {
            if (balls[index].Active)
            {
                continue;
            }

            ball = index;
            break;
        }

        if (ball < 0)
        {
            return false;
        }

        var bits = 0;
        for (var row = 0; row < path.Length; row++)
        {
            bits |= path[row] << row;
        }

        var random = GameRandom.FromSeed(seed);
        var offset = ball * MaxSegments;
        for (var segment = 0; segment < MaxSegments; segment++)
        {
            wobbles[offset + segment] = random.Range(-MaxWobble, MaxWobble);
        }

        var elapsed = MathF.Max(0f, startSeconds);
        balls[ball] = new PlinkoBall
        {
            Active = true,
            Drop = drop,
            PathBits = bits,
            Elapsed = elapsed,
            Reported = Math.Min(drop.Rows, (int)(elapsed / SegmentSeconds)),
        };
        return true;
    }

    public void Advance(float deltaSeconds)
    {
        eventCount = 0;
        for (var index = 0; index < balls.Length; index++)
        {
            ref var ball = ref balls[index];
            if (!ball.Active)
            {
                continue;
            }

            ball.Elapsed += MathF.Max(0f, deltaSeconds);
            Report(index, ref ball);
        }
    }

    public void SnapAll()
    {
        eventCount = 0;
        for (var index = 0; index < balls.Length; index++)
        {
            ref var ball = ref balls[index];
            if (!ball.Active)
            {
                continue;
            }

            ball.Reported = ball.Drop.Rows;
            ball.Elapsed = FlightSeconds(ball.Drop.Rows);
            Report(index, ref ball);
        }
    }

    public Vector2 Position(int ball)
    {
        ref readonly var state = ref balls[ball];
        return PositionAt(state.Drop.Rows, state.PathBits, wobbles.AsSpan(ball * MaxSegments, MaxSegments),
            state.Elapsed);
    }

    public static Vector2 PositionAt(int rows, int pathBits, ReadOnlySpan<float> wobble, float elapsed)
    {
        var segments = rows + 1;
        var progress = MathF.Max(0f, elapsed) / SegmentSeconds;
        var segment = Math.Min(segments - 1, (int)progress);
        var fraction = Math.Clamp(progress - segment, 0f, 1f);
        if (progress >= segments)
        {
            segment = segments - 1;
            fraction = 1f;
        }

        var from = Waypoint(rows, pathBits, segment);
        var to = Waypoint(rows, pathBits, segment + 1);
        var x = from.X + (to.X - from.X) * fraction;
        if (segment > 0 && segment < wobble.Length)
        {
            x += wobble[segment] * MathF.Sin(MathF.PI * fraction);
        }

        var y = segment == 0
            ? from.Y + (to.Y - from.Y) * fraction * fraction
            : from.Y + (to.Y - from.Y) * fraction - ArcHeight * 4f * fraction * (1f - fraction);
        return new Vector2(x, y);
    }

    public static Vector2 Waypoint(int rows, int pathBits, int index)
    {
        if (index <= 0)
        {
            return PlinkoBoardLayout.StartUnit;
        }

        if (index > rows)
        {
            return PlinkoBoardLayout.SlotUnit(rows, RightsBefore(pathBits, rows));
        }

        var row = index - 1;
        return PlinkoBoardLayout.ContactUnit(row, RightsBefore(pathBits, row));
    }

    public static int RightsBefore(int pathBits, int row)
    {
        var mask = row >= 31 ? -1 : (1 << row) - 1;
        return System.Numerics.BitOperations.PopCount((uint)(pathBits & mask));
    }

    private void Report(int index, ref PlinkoBall ball)
    {
        var rows = ball.Drop.Rows;
        var reached = Math.Min(rows, (int)(ball.Elapsed / SegmentSeconds));
        while (ball.Reported < reached)
        {
            var row = ball.Reported;
            Push(new PlinkoEvent(PlinkoEventKind.Peg, index, row, RightsBefore(ball.PathBits, row) + 1, ball.Drop));
            ball.Reported++;
        }

        if (ball.Elapsed < FlightSeconds(rows))
        {
            return;
        }

        Push(new PlinkoEvent(PlinkoEventKind.Landed, index, rows, ball.Drop.Slot, ball.Drop));
        ball.Active = false;
    }

    private void Push(in PlinkoEvent entry)
    {
        if (eventCount >= events.Length)
        {
            return;
        }

        events[eventCount] = entry;
        eventCount++;
    }
}
