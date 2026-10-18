namespace Aetherphone.Windows.Components;

internal struct SeekHold
{
    public const long HoldMilliseconds = 3000;
    public const float ArriveSeconds = 1.5f;

    private float targetSeconds;
    private long until;

    public readonly bool Active => until != 0;

    public void Begin(float seconds, long nowMilliseconds)
    {
        targetSeconds = seconds;
        until = nowMilliseconds + HoldMilliseconds;
    }

    public void Clear()
    {
        until = 0;
    }

    public float Shown(float reportedSeconds, long nowMilliseconds)
    {
        if (until == 0)
        {
            return reportedSeconds;
        }

        if (nowMilliseconds >= until || MathF.Abs(reportedSeconds - targetSeconds) <= ArriveSeconds)
        {
            until = 0;
            return reportedSeconds;
        }

        return targetSeconds;
    }
}
