namespace Aetherphone.Core.Hunts;

internal static class HuntWindowMath
{
    public static DateTimeOffset? MinimumReachedAt(HuntWindowDto window, HuntMobDefinition? mob)
    {
        var timing = ResolveTiming(window, mob);
        if (timing is null)
        {
            return null;
        }

        return window.StartedAt + TimeSpan.FromHours(timing.Min);
    }

    public static TimeSpan? TimeUntilMinimum(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now) =>
        MinimumReachedAt(window, mob) - now;

    public static TimeSpan? TimeSinceCap(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
    {
        var timing = ResolveTiming(window, mob);
        if (timing?.Cap is not { } cap)
        {
            return null;
        }

        return now - (window.StartedAt + TimeSpan.FromHours(cap));
    }

    public static HuntWindowStatus Status(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
    {
        var timing = ResolveTiming(window, mob);
        if (timing is null)
        {
            return HuntWindowStatus.Unknown;
        }

        if (now < window.StartedAt + TimeSpan.FromHours(timing.Min))
        {
            return HuntWindowStatus.Closed;
        }

        if (timing.Cap is { } cap && now >= window.StartedAt + TimeSpan.FromHours(cap))
        {
            return HuntWindowStatus.Capped;
        }

        return HuntWindowStatus.Open;
    }

    public static double? Percentage(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
    {
        var timing = ResolveTiming(window, mob);
        if (timing?.Cap is not { } cap)
        {
            return null;
        }

        var span = cap - timing.Min;
        if (span <= 0d)
        {
            return null;
        }

        var minimumReachedAt = window.StartedAt + TimeSpan.FromHours(timing.Min);
        if (now < minimumReachedAt)
        {
            return 0d;
        }

        return (now - minimumReachedAt).TotalHours / span * 100d;
    }

    private static HuntMobTimingWindow? ResolveTiming(HuntWindowDto window, HuntMobDefinition? mob)
    {
        if (mob is null || mob.Windows.Length == 0)
        {
            return null;
        }

        var index = window.Num - 1;
        var windowDef = index >= 0 && index < mob.Windows.Length ? mob.Windows[index] : mob.Windows[0];
        var baseTiming = window.UseMaintenanceTiming ? windowDef.Timing?.Maintenance : windowDef.Timing?.Normal;
        if (window.SnipedNum is not { } snipedNum)
        {
            return baseTiming;
        }

        var normal = windowDef.Timing?.Normal;
        if (normal?.Cap is not { } normalCap)
        {
            return baseTiming;
        }

        var span = normalCap - normal.Min;
        return new HuntMobTimingWindow
        {
            Min = normal.Min,
            Cap = normal.Min + span * (snipedNum + 1),
        };
    }
}
