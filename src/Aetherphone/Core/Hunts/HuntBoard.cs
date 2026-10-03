namespace Aetherphone.Core.Hunts;

internal enum HuntBoardSection : byte
{
    Live,
    Open,
    Soon,
    Waiting,
}

internal readonly record struct HuntTimeline(float Minimum, float Average, float Cap, float Now, bool PastEnd)
{
    public bool HasAverage => Average >= 0f;
}

internal static class HuntBoard
{
    public const float TimelineHeadroom = 1.12f;

    public static HuntBoardSection SectionFor(HuntWindowStatus status) => status switch
    {
        HuntWindowStatus.Spawned or HuntWindowStatus.Scheduled => HuntBoardSection.Live,
        HuntWindowStatus.Open or HuntWindowStatus.Capped => HuntBoardSection.Open,
        HuntWindowStatus.Unmet => HuntBoardSection.Waiting,
        _ => HuntBoardSection.Soon,
    };

    public static float RingFraction(HuntWindowStatus status, HuntWindowDto window, HuntMobDefinition? mob,
        DateTimeOffset now)
    {
        switch (status)
        {
            case HuntWindowStatus.Spawned:
            case HuntWindowStatus.Scheduled:
            case HuntWindowStatus.Capped:
                return 1f;
            case HuntWindowStatus.Open:
            case HuntWindowStatus.Unmet:
                return HuntWindowMath.RawPercentage(window, mob, now) is { } percentage
                    ? (float)Math.Clamp(percentage / 100d, 0d, 1d)
                    : 0f;
            case HuntWindowStatus.Closed:
                return ClosedFraction(window, mob, now);
            default:
                return 0f;
        }
    }

    public static float ClosedFraction(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
    {
        if (window.StartedAt == default || HuntWindowMath.MinimumReachedAt(window, mob) is not { } opensAt)
        {
            return 0f;
        }

        var total = (opensAt - window.StartedAt).TotalSeconds;
        if (total <= 0d)
        {
            return 1f;
        }

        return (float)Math.Clamp((now - window.StartedAt).TotalSeconds / total, 0d, 1d);
    }

    public static HuntTimeline? TimelineFor(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
    {
        if (window.StartedAt == default || HuntWindowMath.TimingFor(window, mob) is not { Cap: { } cap } timing ||
            cap <= 0d)
        {
            return null;
        }

        var axisHours = cap * TimelineHeadroom;
        var elapsedHours = (now - window.StartedAt).TotalHours;
        var average = timing.Avg is { } averageHours && averageHours > timing.Min && averageHours < cap
            ? (float)(averageHours / axisHours)
            : -1f;
        var pastEnd = elapsedHours > axisHours;
        return new HuntTimeline((float)(timing.Min / axisHours), average, (float)(cap / axisHours),
            (float)Math.Clamp(elapsedHours / axisHours, 0d, 1d), pastEnd);
    }

    public static double SortKey(HuntBoardSection section, HuntWindowStatus status, HuntWindowDto window,
        HuntMobDefinition? mob, DateTimeOffset now, DateTimeOffset? spawnedSince, TimeSpan? untilGate)
    {
        switch (section)
        {
            case HuntBoardSection.Live:
                return spawnedSince is { } since ? -since.ToUnixTimeSeconds() : 0d;
            case HuntBoardSection.Open:
                return -(HuntWindowMath.RawPercentage(window, mob, now) ?? (status == HuntWindowStatus.Capped ? 100d : 0d));
            case HuntBoardSection.Waiting:
                return untilGate?.TotalSeconds ?? double.MaxValue;
            default:
                return HuntWindowMath.TimeUntilMinimum(window, mob, now)?.TotalSeconds ?? double.MaxValue;
        }
    }
}
