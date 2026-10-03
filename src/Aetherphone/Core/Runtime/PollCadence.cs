namespace Aetherphone.Core.Runtime;

internal sealed class PollCadence
{
    private const long WatchLeaseMilliseconds = 1000;
    private static readonly TimeSpan ReconnectSpread = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PushCoveredInterval = TimeSpan.FromMinutes(15);

    private readonly PhoneVisibility visibility;
    private readonly TimeSpan foregroundInterval;
    private readonly TimeSpan backgroundInterval;
    private readonly RealtimeSignalBus? pushSource;
    private DateTime lastPollUtc = DateTime.MinValue;
    private DateTime scheduledUtc = DateTime.MaxValue;
    private volatile bool immediate;
    private long watchedUntilMilliseconds;

    public PollCadence(PhoneVisibility visibility, TimeSpan foregroundInterval, TimeSpan backgroundInterval,
        RealtimeSignalBus? pushSource = null)
    {
        this.visibility = visibility;
        this.foregroundInterval = foregroundInterval;
        this.backgroundInterval = backgroundInterval;
        this.pushSource = pushSource;
    }

    public TimeSpan CurrentInterval
    {
        get
        {
            var visible = visibility.IsVisible;
            if (pushSource is { RealtimeActive: true } && !(visible && IsWatched))
            {
                return PushCoveredInterval;
            }

            return visible ? foregroundInterval : backgroundInterval;
        }
    }

    public bool IsWatched => Environment.TickCount64 < Volatile.Read(ref watchedUntilMilliseconds);

    public void NoteWatched()
    {
        Volatile.Write(ref watchedUntilMilliseconds, Environment.TickCount64 + WatchLeaseMilliseconds);
    }

    public void RequestImmediate()
    {
        immediate = true;
    }

    public void RequestAfterReconnect()
    {
        RequestAt(DateTime.UtcNow + ReconnectSpread * Random.Shared.NextDouble());
    }

    public void RequestAt(DateTime dueUtc)
    {
        if (dueUtc < scheduledUtc)
        {
            scheduledUtc = dueUtc;
        }
    }

    public bool Due(DateTime nowUtc)
    {
        if (immediate || nowUtc >= scheduledUtc)
        {
            immediate = false;
            scheduledUtc = DateTime.MaxValue;
            lastPollUtc = nowUtc;
            return true;
        }

        if (nowUtc - lastPollUtc < CurrentInterval)
        {
            return false;
        }

        lastPollUtc = nowUtc;
        return true;
    }

    public void Mark(DateTime nowUtc)
    {
        lastPollUtc = nowUtc;
    }

    public void Reset()
    {
        lastPollUtc = DateTime.MinValue;
    }
}
