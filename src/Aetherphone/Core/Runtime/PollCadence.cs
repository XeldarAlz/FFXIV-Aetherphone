namespace Aetherphone.Core.Runtime;

internal sealed class PollCadence
{
    private readonly PhoneVisibility visibility;
    private readonly TimeSpan foregroundInterval;
    private readonly TimeSpan backgroundInterval;
    private DateTime lastPollUtc = DateTime.MinValue;
    private DateTime scheduledUtc = DateTime.MaxValue;
    private volatile bool immediate;

    public PollCadence(PhoneVisibility visibility, TimeSpan foregroundInterval, TimeSpan backgroundInterval)
    {
        this.visibility = visibility;
        this.foregroundInterval = foregroundInterval;
        this.backgroundInterval = backgroundInterval;
    }

    public TimeSpan CurrentInterval => visibility.IsVisible ? foregroundInterval : backgroundInterval;

    public void RequestImmediate()
    {
        immediate = true;
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
