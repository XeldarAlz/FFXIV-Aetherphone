using Aetherphone.Core.Game;

namespace Aetherphone.Core.Fishing;

internal readonly record struct FishWindowRule(
    ushort StartMinute,
    ushort EndMinute,
    byte[] Weather,
    byte[] PreviousWeather)
{
    public const int MinutesPerDay = 1440;

    public bool AllDay => StartMinute == EndMinute || (StartMinute == 0 && EndMinute == MinutesPerDay);

    public bool NeedsWeather => Weather.Length > 0 || PreviousWeather.Length > 0;

    public bool AlwaysOpen => AllDay && !NeedsWeather;
}

internal readonly record struct FishWindow(long StartUnix, long EndUnix)
{
    public static readonly FishWindow None = new(0, 0);

    public bool Exists => EndUnix > StartUnix;

    public bool IsOpen(long nowUnix) => Exists && nowUnix >= StartUnix && nowUnix < EndUnix;
}

internal readonly record struct WeatherOdds(byte Id, int Cumulative);

internal static class EorzeaClock
{
    public const long PeriodSeconds = 1400;
    public const int PeriodMinutes = 480;
    private const long RealSecondsPerEorzeaDay = 4200;
    private const long SecondsPerMinuteNumerator = 175;
    private const long SecondsPerMinuteDenominator = 60;

    public static long PeriodStart(long unixSeconds) => unixSeconds - Mod(unixSeconds, PeriodSeconds);

    public static int MinuteOfDay(long unixSeconds) =>
        (int)(Mod(unixSeconds, RealSecondsPerEorzeaDay) * SecondsPerMinuteDenominator / SecondsPerMinuteNumerator);

    public static int PeriodFirstMinute(long periodStartUnix) => MinuteOfDay(periodStartUnix) / PeriodMinutes *
                                                                  PeriodMinutes;

    public static long SecondsForMinutes(int minutes) =>
        (minutes * SecondsPerMinuteNumerator + SecondsPerMinuteDenominator - 1) / SecondsPerMinuteDenominator;

    private static long Mod(long value, long modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
}

internal static class FishWindowMath
{
    public const int DefaultSearchPeriods = 4320;
    private const int MaxMergedPeriods = 72;

    public static byte WeatherAt(ReadOnlySpan<WeatherOdds> odds, long unixSeconds)
    {
        if (odds.IsEmpty)
        {
            return 0;
        }

        var target = WeatherService.ForecastTarget(EorzeaClock.PeriodStart(unixSeconds));
        for (var index = 0; index < odds.Length; index++)
        {
            if (target < odds[index].Cumulative)
            {
                return odds[index].Id;
            }
        }

        return odds[^1].Id;
    }

    public static FishWindow Next(in FishWindowRule rule, ReadOnlySpan<WeatherOdds> odds, long fromUnix,
        int maxPeriods = DefaultSearchPeriods)
    {
        if (rule.NeedsWeather && odds.IsEmpty)
        {
            return FishWindow.None;
        }

        var firstPeriod = EorzeaClock.PeriodStart(fromUnix);
        Span<int> segments = stackalloc int[4];
        for (var periodIndex = 0; periodIndex < maxPeriods; periodIndex++)
        {
            var periodStart = firstPeriod + periodIndex * EorzeaClock.PeriodSeconds;
            if (!PeriodMatches(rule, odds, periodStart))
            {
                continue;
            }

            var count = Segments(rule, EorzeaClock.PeriodFirstMinute(periodStart), segments);
            for (var segment = 0; segment < count; segment++)
            {
                var startMinute = segments[segment * 2];
                var endMinute = segments[segment * 2 + 1];
                var periodFirstMinute = EorzeaClock.PeriodFirstMinute(periodStart);
                var startUnix = periodStart + EorzeaClock.SecondsForMinutes(startMinute - periodFirstMinute);
                var endUnix = periodStart + EorzeaClock.SecondsForMinutes(endMinute - periodFirstMinute);
                if (endMinute == periodFirstMinute + EorzeaClock.PeriodMinutes)
                {
                    endUnix = ExtendForward(rule, odds, periodStart);
                }

                if (endUnix <= fromUnix)
                {
                    continue;
                }

                if (startMinute == periodFirstMinute)
                {
                    startUnix = ExtendBackward(rule, odds, periodStart);
                }

                return new FishWindow(startUnix, endUnix);
            }
        }

        return FishWindow.None;
    }

    public static int Upcoming(in FishWindowRule rule, ReadOnlySpan<WeatherOdds> odds, long fromUnix,
        Span<FishWindow> into, int maxPeriods = DefaultSearchPeriods)
    {
        var written = 0;
        var cursor = fromUnix;
        while (written < into.Length)
        {
            var window = Next(rule, odds, cursor, maxPeriods);
            if (!window.Exists)
            {
                break;
            }

            into[written++] = window;
            cursor = window.EndUnix;
        }

        return written;
    }

    public static bool PeriodMatches(in FishWindowRule rule, ReadOnlySpan<WeatherOdds> odds, long periodStart)
    {
        if (rule.Weather.Length > 0 && !Contains(rule.Weather, WeatherAt(odds, periodStart)))
        {
            return false;
        }

        return rule.PreviousWeather.Length == 0 ||
               Contains(rule.PreviousWeather, WeatherAt(odds, periodStart - EorzeaClock.PeriodSeconds));
    }

    public static int Segments(in FishWindowRule rule, int periodFirstMinute, Span<int> into)
    {
        var periodEnd = periodFirstMinute + EorzeaClock.PeriodMinutes;
        if (rule.AllDay)
        {
            into[0] = periodFirstMinute;
            into[1] = periodEnd;
            return 1;
        }

        var count = 0;
        if (rule.StartMinute < rule.EndMinute)
        {
            AddIntersection(rule.StartMinute, rule.EndMinute, periodFirstMinute, periodEnd, into, ref count);
            return count;
        }

        AddIntersection(0, rule.EndMinute, periodFirstMinute, periodEnd, into, ref count);
        AddIntersection(rule.StartMinute, FishWindowRule.MinutesPerDay, periodFirstMinute, periodEnd, into,
            ref count);
        return count;
    }

    private static void AddIntersection(int start, int end, int periodStart, int periodEnd, Span<int> into,
        ref int count)
    {
        var low = Math.Max(start, periodStart);
        var high = Math.Min(end, periodEnd);
        if (high <= low)
        {
            return;
        }

        into[count * 2] = low;
        into[count * 2 + 1] = high;
        count++;
    }

    private static long ExtendForward(in FishWindowRule rule, ReadOnlySpan<WeatherOdds> odds, long periodStart)
    {
        Span<int> segments = stackalloc int[4];
        var end = periodStart + EorzeaClock.PeriodSeconds;
        for (var step = 0; step < MaxMergedPeriods; step++)
        {
            if (!PeriodMatches(rule, odds, end))
            {
                return end;
            }

            var firstMinute = EorzeaClock.PeriodFirstMinute(end);
            var count = Segments(rule, firstMinute, segments);
            if (count == 0 || segments[0] != firstMinute)
            {
                return end;
            }

            if (segments[1] != firstMinute + EorzeaClock.PeriodMinutes)
            {
                return end + EorzeaClock.SecondsForMinutes(segments[1] - firstMinute);
            }

            end += EorzeaClock.PeriodSeconds;
        }

        return end;
    }

    private static long ExtendBackward(in FishWindowRule rule, ReadOnlySpan<WeatherOdds> odds, long periodStart)
    {
        Span<int> segments = stackalloc int[4];
        var start = periodStart;
        for (var step = 0; step < MaxMergedPeriods; step++)
        {
            var previous = start - EorzeaClock.PeriodSeconds;
            if (!PeriodMatches(rule, odds, previous))
            {
                return start;
            }

            var firstMinute = EorzeaClock.PeriodFirstMinute(previous);
            var count = Segments(rule, firstMinute, segments);
            if (count == 0)
            {
                return start;
            }

            var lastStart = segments[(count - 1) * 2];
            var lastEnd = segments[(count - 1) * 2 + 1];
            if (lastEnd != firstMinute + EorzeaClock.PeriodMinutes)
            {
                return start;
            }

            if (lastStart != firstMinute)
            {
                return previous + EorzeaClock.SecondsForMinutes(lastStart - firstMinute);
            }

            start = previous;
        }

        return start;
    }

    private static bool Contains(byte[] set, byte value)
    {
        for (var index = 0; index < set.Length; index++)
        {
            if (set[index] == value)
            {
                return true;
            }
        }

        return false;
    }
}
