namespace Aetherphone.Core.Housing;

internal readonly record struct HousingLotteryState(HousingLotteryPhase Phase, DateTime? EndsUtc)
{
    public static readonly HousingLotteryState Unknown = new(HousingLotteryPhase.Unknown, null);

    public bool IsKnown => Phase != HousingLotteryPhase.Unknown && EndsUtc is not null;
}

internal readonly record struct HousingDistrictStats(
    int Open,
    int Small,
    int Medium,
    int Large,
    int Wards,
    int FewestEntries,
    int Lottery)
{
    public bool HasFewestEntries => FewestEntries >= 0;
}

internal static class HousingLottery
{
    public const int EntryDays = 5;
    public const int ResultsDays = 4;

    public static TimeSpan? PhaseLength(HousingLotteryPhase phase) => phase switch
    {
        HousingLotteryPhase.Entry => TimeSpan.FromDays(EntryDays),
        HousingLotteryPhase.Results => TimeSpan.FromDays(ResultsDays),
        _ => null,
    };

    public static float Progress(HousingLotteryPhase phase, DateTime? endsUtc, DateTime nowUtc)
    {
        if (PhaseLength(phase) is not { } length || endsUtc is not { } ends || ends == default)
        {
            return -1f;
        }

        var remaining = (ends - nowUtc).TotalSeconds;
        var fraction = 1d - remaining / length.TotalSeconds;
        return (float)Math.Clamp(fraction, 0d, 1d);
    }

    public static HousingLotteryState Resolve(IReadOnlyList<HousingPlot> plots)
    {
        var best = HousingLotteryState.Unknown;
        var bestIsCycle = false;
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            if (plot.PhaseEndsUtc is not { } ends || ends == default || plot.Phase == HousingLotteryPhase.Unknown)
            {
                continue;
            }

            var isCycle = IsCycle(plot.Phase);
            if (bestIsCycle && !isCycle)
            {
                continue;
            }

            if (best.EndsUtc is { } current && isCycle == bestIsCycle && ends >= current)
            {
                continue;
            }

            best = new HousingLotteryState(plot.Phase, ends);
            bestIsCycle = isCycle;
        }

        return best;
    }

    public static HousingLotteryState Prefer(HousingLotteryState current, HousingLotteryState candidate)
    {
        if (!candidate.IsKnown)
        {
            return current;
        }

        if (!current.IsKnown)
        {
            return candidate;
        }

        var currentCycle = IsCycle(current.Phase);
        var candidateCycle = IsCycle(candidate.Phase);
        if (currentCycle != candidateCycle)
        {
            return candidateCycle ? candidate : current;
        }

        return candidate.EndsUtc < current.EndsUtc ? candidate : current;
    }

    private static bool IsCycle(HousingLotteryPhase phase) =>
        phase is HousingLotteryPhase.Entry or HousingLotteryPhase.Results;

    public static HousingDistrictStats Summarize(IReadOnlyList<HousingPlot> plots)
    {
        var small = 0;
        var medium = 0;
        var large = 0;
        var lottery = 0;
        var fewest = -1;
        Span<bool> wards = stackalloc bool[HousingDistricts.DefaultWards + 1];
        var wardCount = 0;
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            switch (plot.Size)
            {
                case HousingPlotSize.Small:
                    small++;
                    break;
                case HousingPlotSize.Medium:
                    medium++;
                    break;
                case HousingPlotSize.Large:
                    large++;
                    break;
            }

            var ward = plot.Key.Ward;
            if (ward >= 1 && ward < wards.Length && !wards[ward])
            {
                wards[ward] = true;
                wardCount++;
            }

            if (plot.Phase != HousingLotteryPhase.Entry)
            {
                continue;
            }

            lottery++;
            if (plot.Entries is { } entries && (fewest < 0 || entries < fewest))
            {
                fewest = entries;
            }
        }

        return new HousingDistrictStats(plots.Count, small, medium, large, wardCount, fewest, lottery);
    }
}
