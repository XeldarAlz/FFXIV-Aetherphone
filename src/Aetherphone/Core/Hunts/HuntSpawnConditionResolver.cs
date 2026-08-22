using Aetherphone.Core.Game;

namespace Aetherphone.Core.Hunts;

internal readonly record struct HuntConditionWindow(DateTimeOffset Start, DateTimeOffset End)
{
    public bool ActiveAt(DateTimeOffset instant) => instant >= Start && instant < End;
}

internal static class HuntSpawnConditionResolver
{
    private const double EorzeaSecondsPerRealSecond = 144.0 / 7.0;
    private const long EorzeaSecondsPerHour = 3600;
    private const long EorzeaSecondsPerDay = 86400;

    private const long MoonPhaseEorzeaSeconds = 4 * EorzeaSecondsPerDay;
    private const long MoonCycleEorzeaSeconds = 8 * MoonPhaseEorzeaSeconds;
    private const int FullMoonPhaseNumber = 5;
    private const int NewMoonPhaseNumber = 1;

    private const long RealSecondsPerWeatherWindow = 1400;

    private const int MaxCycleAttempts = 500;
    private const int MaxWeatherStepAttempts = 1000;
    private const int MaxWeatherStreakEndAttempts = 5000;

    public static HuntConditionWindow? ResolveGate(IReadOnlyList<HuntMobConditionWindow> automatic,
        DateTimeOffset at)
    {
        HuntConditionWindow? earliest = null;
        for (var index = 0; index < automatic.Count; index++)
        {
            if (Resolve(automatic[index].RuleSet, at) is not { } window)
            {
                continue;
            }

            if (window.ActiveAt(at))
            {
                return window;
            }

            if (earliest is null || window.Start < earliest.Value.Start)
            {
                earliest = window;
            }
        }

        return earliest;
    }

    public static HuntConditionWindow? Resolve(IReadOnlyList<HuntSpawnConditionRule> rules, DateTimeOffset at)
    {
        if (rules.Count == 0)
        {
            return null;
        }

        if (rules.Count == 1)
        {
            return ResolveRule(rules[0], at);
        }

        var cursor = at;
        Span<HuntConditionWindow> windows = stackalloc HuntConditionWindow[rules.Count];
        for (var attempt = 0; attempt < MaxCycleAttempts; attempt++)
        {
            for (var index = 0; index < rules.Count; index++)
            {
                if (ResolveRule(rules[index], cursor) is not { } window)
                {
                    return null;
                }

                windows[index] = window;
            }

            if (AnyOverlap(windows))
            {
                var latestStart = windows[0].Start;
                var earliestEnd = windows[0].End;
                for (var index = 1; index < windows.Length; index++)
                {
                    if (windows[index].Start > latestStart)
                    {
                        latestStart = windows[index].Start;
                    }

                    if (windows[index].End < earliestEnd)
                    {
                        earliestEnd = windows[index].End;
                    }
                }

                return new HuntConditionWindow(latestStart, earliestEnd);
            }

            var minEnd = windows[0].End;
            for (var index = 1; index < windows.Length; index++)
            {
                if (windows[index].End < minEnd)
                {
                    minEnd = windows[index].End;
                }
            }

            cursor = minEnd;
        }

        return null;
    }

    private static bool AnyOverlap(Span<HuntConditionWindow> windows)
    {
        for (var left = 0; left < windows.Length - 1; left++)
        {
            for (var right = left + 1; right < windows.Length; right++)
            {
                var a = windows[left];
                var b = windows[right];
                if (b.Start >= a.Start && b.Start < a.End || b.End > a.Start && b.End <= a.End ||
                    b.Start < a.Start && a.End < b.End)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HuntConditionWindow? ResolveRule(HuntSpawnConditionRule rule, DateTimeOffset at) =>
        rule.Type switch
        {
            "moon" => ResolveMoon(rule, at),
            "time" => ResolveTime(rule, at),
            "weather" => ResolveWeather(rule, at),
            _ => null,
        };

    private static HuntConditionWindow? ResolveMoon(HuntSpawnConditionRule rule, DateTimeOffset at)
    {
        if (rule.Phase is not { Length: > 0 } phase)
        {
            return null;
        }

        var phaseNumber = string.Equals(phase, "full", StringComparison.OrdinalIgnoreCase)
            ? FullMoonPhaseNumber
            : NewMoonPhaseNumber;
        var nowEorzea = ToEorzeaSeconds(at);
        var cycleCursor = nowEorzea;

        for (var attempt = 0; attempt < MaxCycleAttempts; attempt++)
        {
            var cycleStart = FloorTo(cycleCursor, MoonCycleEorzeaSeconds) - EorzeaSecondsPerHour * 12;
            var phaseStart = cycleStart + MoonPhaseEorzeaSeconds * (phaseNumber - 1);

            long windowStart;
            long windowEnd;
            if (rule.Periods is { Length: > 0 } periods)
            {
                windowStart = phaseStart;
                windowEnd = phaseStart;
                for (var index = 0; index < periods.Length; index++)
                {
                    var period = periods[index];
                    if (period is null)
                    {
                        continue;
                    }

                    windowStart = phaseStart + period.From;
                    windowEnd = phaseStart + period.To;
                    if (windowEnd > nowEorzea)
                    {
                        break;
                    }
                }
            }
            else
            {
                windowStart = phaseStart;
                windowEnd = phaseStart + MoonPhaseEorzeaSeconds;
            }

            if (windowEnd > nowEorzea)
            {
                return new HuntConditionWindow(FromEorzeaSeconds(windowStart), FromEorzeaSeconds(windowEnd));
            }

            cycleCursor += MoonCycleEorzeaSeconds;
        }

        return null;
    }

    private static HuntConditionWindow? ResolveTime(HuntSpawnConditionRule rule, DateTimeOffset at)
    {
        if (rule.Hours is not { Length: > 0 } hours || rule.Duration is not { } durationEorzeaSeconds)
        {
            return null;
        }

        var nowEorzea = ToEorzeaSeconds(at);
        var dayCursor = FloorTo(nowEorzea, EorzeaSecondsPerDay) - EorzeaSecondsPerDay;

        for (var attempt = 0; attempt < MaxCycleAttempts; attempt++)
        {
            for (var index = 0; index < hours.Length; index++)
            {
                var windowStart = dayCursor + hours[index] * EorzeaSecondsPerHour;
                var windowEnd = windowStart + durationEorzeaSeconds;
                if (windowEnd > nowEorzea)
                {
                    return new HuntConditionWindow(FromEorzeaSeconds(windowStart), FromEorzeaSeconds(windowEnd));
                }
            }

            dayCursor += EorzeaSecondsPerDay;
        }

        return null;
    }

    private static HuntConditionWindow? ResolveWeather(HuntSpawnConditionRule rule, DateTimeOffset at)
    {
        if (rule.MatchingWeather is not { Length: > 0 } matching ||
            rule.Probabilities is not { Length: > 0 } probabilities)
        {
            return null;
        }

        var offsetSeconds = rule.Offset ?? 0;
        var cursor = BackdateToStreakStart(at.ToUnixTimeSeconds(), probabilities, matching);

        for (var attempt = 0; attempt < MaxCycleAttempts; attempt++)
        {
            switch (TryResolveWeatherStreak(ref cursor, probabilities, matching, offsetSeconds))
            {
                case { } window:
                    return window;
                case null when cursor < 0:
                    return null;
            }
        }

        return null;
    }

    private static long BackdateToStreakStart(long nowUnix, HuntWeatherProbability[] probabilities,
        string[] matching)
    {
        var windowStart = nowUnix - Mod(nowUnix, RealSecondsPerWeatherWindow);
        if (!IsMatching(windowStart, probabilities, matching))
        {
            return windowStart;
        }

        for (var attempt = 0; attempt < MaxWeatherStreakEndAttempts; attempt++)
        {
            var earlier = windowStart - RealSecondsPerWeatherWindow;
            if (!IsMatching(earlier, probabilities, matching))
            {
                return windowStart;
            }

            windowStart = earlier;
        }

        return windowStart;
    }

    private static HuntConditionWindow? TryResolveWeatherStreak(ref long cursor,
        HuntWeatherProbability[] probabilities, string[] matching, int offsetSeconds)
    {
        for (var attempt = 0; attempt < MaxWeatherStepAttempts; attempt++)
        {
            if (IsMatching(cursor, probabilities, matching))
            {
                break;
            }

            if (attempt == MaxWeatherStepAttempts - 1)
            {
                cursor = -1;
                return null;
            }

            cursor += RealSecondsPerWeatherWindow;
        }

        var remaining = offsetSeconds - RealSecondsPerWeatherWindow;
        for (var attempt = 0; remaining > 0 && attempt < MaxWeatherStepAttempts; attempt++)
        {
            cursor += RealSecondsPerWeatherWindow;
            if (!IsMatching(cursor, probabilities, matching))
            {
                return null;
            }

            remaining -= RealSecondsPerWeatherWindow;
        }

        var endUnix = cursor + RealSecondsPerWeatherWindow;
        var startUnix = cursor + remaining + RealSecondsPerWeatherWindow;
        for (var attempt = 0; attempt < MaxWeatherStreakEndAttempts; attempt++)
        {
            if (!IsMatching(endUnix, probabilities, matching))
            {
                break;
            }

            endUnix += RealSecondsPerWeatherWindow;
        }

        cursor = endUnix;
        return new HuntConditionWindow(DateTimeOffset.FromUnixTimeSeconds(startUnix),
            DateTimeOffset.FromUnixTimeSeconds(endUnix));
    }

    private static bool IsMatching(long unixSeconds, HuntWeatherProbability[] probabilities, string[] matching) =>
        Array.IndexOf(matching, ResolveWeatherId(unixSeconds, probabilities)) >= 0;

    private static string ResolveWeatherId(long unixSeconds, HuntWeatherProbability[] probabilities)
    {
        var target = WeatherService.ForecastTarget(unixSeconds);
        var cumulative = 0;
        for (var index = 0; index < probabilities.Length; index++)
        {
            cumulative += probabilities[index].Chance;
            if (target < cumulative)
            {
                return probabilities[index].Condition;
            }
        }

        return probabilities.Length > 0 ? probabilities[^1].Condition : string.Empty;
    }

    private static long ToEorzeaSeconds(DateTimeOffset at) =>
        (long)Math.Round(at.ToUnixTimeSeconds() * EorzeaSecondsPerRealSecond);

    private static DateTimeOffset FromEorzeaSeconds(long eorzeaSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds((long)Math.Round(eorzeaSeconds / EorzeaSecondsPerRealSecond));

    private static long FloorTo(long value, long step) => value - (((value % step) + step) % step);

    private static long Mod(long value, long step) => ((value % step) + step) % step;
}
