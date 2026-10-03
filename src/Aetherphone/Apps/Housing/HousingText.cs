using System.Globalization;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Housing;

internal static class HousingText
{
    private const int CachedCounts = 512;

    private static readonly string[] Counts = new string[CachedCounts];

    public static string Count(int value)
    {
        if (value < 0 || value >= CachedCounts)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return Counts[value] ??= value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Countdown(ref CachedText cache, DateTime? endsUtc, DateTime nowUtc)
    {
        if (endsUtc is not { } ends || ends == default)
        {
            return cache.IsCurrent(long.MinValue) ? cache.Value : cache.Store(long.MinValue, Loc.T(L.Housing.TimeUnknown));
        }

        if (ends <= nowUtc)
        {
            return cache.IsCurrent(-1L) ? cache.Value : cache.Store(-1L, Loc.T(L.Housing.PhaseExpired));
        }

        var remaining = ends - nowUtc;
        var key = (long)remaining.TotalSeconds;
        return cache.IsCurrent(key)
            ? cache.Value
            : cache.Store(key, HousingFormat.Countdown(TimeSpan.FromSeconds(key)));
    }

    public static string Moment(ref CachedText cache, DateTime? utcMoment)
    {
        var key = utcMoment?.Ticks ?? 0L;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        return cache.Store(key, utcMoment is { } moment && moment != default
            ? HousingFormat.ExactLocalTime(moment)
            : Loc.T(L.Housing.NotReported));
    }

    public static string Updated(ref CachedText cache, DateTime fetchedUtc, DateTime nowUtc)
    {
        var minutes = fetchedUtc == default ? -1L : (long)Math.Max(0d, (nowUtc - fetchedUtc).TotalMinutes);
        if (cache.IsCurrent(minutes))
        {
            return cache.Value;
        }

        return cache.Store(minutes, fetchedUtc == default
            ? Loc.T(L.Housing.NotReported)
            : Loc.T(L.Housing.UpdatedAgo, HousingFormat.AgeRelative(fetchedUtc, nowUtc)));
    }
}
