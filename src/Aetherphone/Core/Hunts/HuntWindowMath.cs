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
        if (ResolveCore(window, mob, now) is not { } core)
        {
            return null;
        }

        if (core.HoursSinceCap is not { } hoursSinceCap)
        {
            return core.OpenPercentage;
        }

        return OvertimePercentage(core.Min, core.Cap, hoursSinceCap) is { } overtime
            ? core.OpenPercentage + overtime
            : null;
    }

    public static double? RawPercentage(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
    {
        if (ResolveCore(window, mob, now) is not { } core)
        {
            return null;
        }

        if (core.HoursSinceCap is not { } hoursSinceCap)
        {
            return core.OpenPercentage;
        }

        return core.OpenPercentage + RawOvertimePercentage(core.Min, core.Cap, hoursSinceCap);
    }

    private static PercentageCore? ResolveCore(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
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
            return new PercentageCore(timing.Min, cap, 0d, null);
        }

        var cappedAt = window.StartedAt + TimeSpan.FromHours(cap);
        var openPercentage = Math.Clamp((now - minimumReachedAt).TotalHours / span * 100d, 0d, 100d);
        var hoursSinceCap = (now - cappedAt).TotalHours;
        return new PercentageCore(timing.Min, cap, openPercentage, hoursSinceCap > 0d ? hoursSinceCap : null);
    }

    private static double? OvertimePercentage(double min, double cap, double hoursSinceCap)
    {
        var cycle = Math.Ceiling(hoursSinceCap / cap);
        var span = cap - min;
        var previousCapEdge = cap * (cycle - 1d);
        var minEdge = min * cycle - span;
        var capEdge = cap * cycle;

        if (minEdge < previousCapEdge)
        {
            return null;
        }

        var cycles = 2d * cycle - 2d;
        cycles += hoursSinceCap < minEdge ? (hoursSinceCap - previousCapEdge) / (minEdge - previousCapEdge) : 1d;
        if (hoursSinceCap > minEdge)
        {
            cycles += (hoursSinceCap - minEdge) / (capEdge - minEdge);
        }

        return cycles * 100d;
    }

    private static double RawOvertimePercentage(double min, double cap, double hoursSinceCap)
    {
        var cycle = Math.Ceiling(hoursSinceCap / cap);
        var span = cap - min;
        var previousCapEdge = cap * (cycle - 1d);
        var minEdge = min * cycle - span;
        var capEdge = cap * cycle;

        var cycles = 2d * cycle - 2d;
        if (minEdge >= previousCapEdge)
        {
            cycles += hoursSinceCap < minEdge ? (hoursSinceCap - previousCapEdge) / (minEdge - previousCapEdge) : 1d;
            if (hoursSinceCap > minEdge)
            {
                cycles += (hoursSinceCap - minEdge) / (capEdge - minEdge);
            }
        }
        else
        {
            cycles += Math.Clamp(2d * (hoursSinceCap - previousCapEdge) / cap, 0d, 2d);
        }

        return cycles * 100d;
    }

    private readonly record struct PercentageCore(double Min, double Cap, double OpenPercentage, double? HoursSinceCap);

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
