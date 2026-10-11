namespace Aetherphone.Apps.Casino.Stage;

internal struct RollingAmount
{
    private const double FollowSpeed = 10.0;
    private const float PopKick = 0.16f;
    private const float PopLimit = 0.30f;
    private const float PopSettleSpeed = 5.5f;

    private double shown;
    private long target;
    private float pop;
    private bool initialized;

    public readonly long Display => (long)Math.Round(shown);

    public readonly long Target => target;

    public readonly float PopScale => 1f + pop;

    public readonly bool Settled => !initialized || Math.Abs(target - shown) < 0.5;

    public void Snap(long value)
    {
        shown = value;
        target = value;
        pop = 0f;
        initialized = true;
    }

    public bool Update(long value, float deltaSeconds) => Update(value, deltaSeconds, FollowSpeed);

    public bool Update(long value, float deltaSeconds, double followSpeed)
    {
        if (!initialized)
        {
            Snap(value);
            return false;
        }

        var changed = value != target;
        if (changed)
        {
            target = value;
            pop = MathF.Min(PopLimit, pop + PopKick);
        }

        var difference = target - shown;
        if (Math.Abs(difference) < 0.5)
        {
            shown = target;
        }
        else
        {
            shown += difference * Math.Min(1.0, deltaSeconds * followSpeed);
        }

        pop = MathF.Max(0f, pop - deltaSeconds * PopSettleSpeed * (pop + 0.12f));
        return changed;
    }

    public void CountUp(long value, float deltaSeconds, float durationSeconds)
    {
        if (!initialized)
        {
            Snap(0);
        }

        if (value != target)
        {
            target = value;
            pop = MathF.Min(PopLimit, pop + PopKick);
        }

        if (durationSeconds <= 0f)
        {
            shown = target;
        }
        else
        {
            var step = Math.Abs((double)target) * deltaSeconds / durationSeconds;
            var difference = target - shown;
            shown = Math.Abs(difference) <= step ? target : shown + Math.Sign(difference) * step;
        }

        pop = MathF.Max(0f, pop - deltaSeconds * PopSettleSpeed * (pop + 0.12f));
    }
}
