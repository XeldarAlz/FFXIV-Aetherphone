using Aetherphone.Apps.Games.MiniGolf;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;

namespace Aetherphone.Apps.Games.Online;

internal sealed class OnlineMiniGolfReplay
{
    private const float SampleSeconds = 1f / GameRoomWire.MiniGolfSamplesPerSecond;
    private const int MarkStride = 3;

    private int[] path = Array.Empty<int>();
    private int[] marks = Array.Empty<int>();
    private long clockMs;
    private int nextMark;
    private float elapsed;

    public int SampleCount => path.Length / 2;

    public bool Active => path.Length >= 2;

    public bool Finished => elapsed >= DurationSeconds;

    public float Cursor => elapsed / SampleSeconds;

    public double ClockSeconds => clockMs / 1000d + elapsed;

    public float DurationSeconds => MathF.Max(0f, (SampleCount - 1) * SampleSeconds);

    public void Begin(MiniGolfShotDto shot)
    {
        path = shot.Path ?? Array.Empty<int>();
        marks = shot.Marks ?? Array.Empty<int>();
        clockMs = shot.ClockMs;
        nextMark = 0;
        elapsed = 0f;
    }

    public void Clear()
    {
        path = Array.Empty<int>();
        marks = Array.Empty<int>();
        nextMark = 0;
        elapsed = 0f;
    }

    public void Advance(float seconds)
    {
        if (seconds <= 0f || !Active)
        {
            return;
        }

        elapsed = MathF.Min(elapsed + seconds, DurationSeconds);
    }

    public void Skip()
    {
        elapsed = DurationSeconds;
    }

    public Vector2 Sample(int index)
    {
        if (!Active)
        {
            return Vector2.Zero;
        }

        var clamped = Math.Clamp(index, 0, SampleCount - 1);
        return new Vector2(path[clamped * 2], path[clamped * 2 + 1]) / GameRoomWire.MiniGolfPathScale;
    }

    public Vector2 Last => Sample(SampleCount - 1);

    // Between the two samples that straddle a tunnel the ball rolls into the mouth and out of the
    // exit instead of sliding across the green between them.
    public Vector2 Position(MiniGolfHole hole)
    {
        if (!Active)
        {
            return Vector2.Zero;
        }

        var cursor = Cursor;
        var from = Math.Clamp((int)MathF.Floor(cursor), 0, SampleCount - 1);
        var to = Math.Min(from + 1, SampleCount - 1);
        var along = Math.Clamp(cursor - from, 0f, 1f);
        var tunnel = TunnelInto(to);
        if (tunnel < 0 || tunnel >= hole.Tunnels.Length || to == from)
        {
            return Vector2.Lerp(Sample(from), Sample(to), along);
        }

        var passage = hole.Tunnels[tunnel];
        return along < 0.5f
            ? Vector2.Lerp(Sample(from), passage.Entry, along * 2f)
            : Vector2.Lerp(passage.Exit, Sample(to), (along - 0.5f) * 2f);
    }

    public float Speed()
    {
        if (SampleCount < 2)
        {
            return 0f;
        }

        var from = Math.Clamp((int)MathF.Floor(Cursor), 0, SampleCount - 2);
        if (TunnelInto(from + 1) >= 0)
        {
            return 0f;
        }

        return Vector2.Distance(Sample(from), Sample(from + 1)) * GameRoomWire.MiniGolfSamplesPerSecond;
    }

    public Vector2 Heading()
    {
        return SampleCount < 2 ? Vector2.Zero : (Sample(1) - Sample(0)) * GameRoomWire.MiniGolfSamplesPerSecond;
    }

    public bool TakeMark(out int kind, out int value, out int sample)
    {
        kind = 0;
        value = 0;
        sample = 0;
        if (nextMark + MarkStride > marks.Length || (!Finished && marks[nextMark] > Cursor))
        {
            return false;
        }

        sample = marks[nextMark];
        kind = marks[nextMark + 1];
        value = marks[nextMark + 2];
        nextMark += MarkStride;
        return true;
    }

    public static GolfEvents EventOf(int kind)
    {
        return kind switch
        {
            GameRoomWire.MiniGolfMarkWall => GolfEvents.Wall,
            GameRoomWire.MiniGolfMarkPost => GolfEvents.Post,
            GameRoomWire.MiniGolfMarkMill => GolfEvents.Mill,
            GameRoomWire.MiniGolfMarkSand => GolfEvents.Sand,
            GameRoomWire.MiniGolfMarkTunnel => GolfEvents.Tunnel,
            GameRoomWire.MiniGolfMarkSplash => GolfEvents.Splash,
            GameRoomWire.MiniGolfMarkOut => GolfEvents.Reset,
            GameRoomWire.MiniGolfMarkLipOut => GolfEvents.LipOut,
            GameRoomWire.MiniGolfMarkDrop => GolfEvents.Drop,
            _ => GolfEvents.None,
        };
    }

    private int TunnelInto(int sample)
    {
        for (var index = 0; index + MarkStride <= marks.Length; index += MarkStride)
        {
            if (marks[index] == sample && marks[index + 1] == GameRoomWire.MiniGolfMarkTunnel)
            {
                return marks[index + 2];
            }
        }

        return -1;
    }
}
