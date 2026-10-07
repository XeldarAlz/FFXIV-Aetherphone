using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Games.Hub;

internal struct DailyCountdown
{
    private const int SecondsPerMinute = 60;
    private const int MinutesPerHour = 60;
    private const int MinutesPerDay = 24 * MinutesPerHour;

    private CachedText text;

    public static int MinutesUntilReset(long utcTicks)
    {
        var secondsIntoDay = utcTicks % TimeSpan.TicksPerDay / TimeSpan.TicksPerSecond;
        var minutesIntoDay = (int)(secondsIntoDay / SecondsPerMinute);
        return MinutesPerDay - minutesIntoDay;
    }

    public string Label() => Label(DateTime.UtcNow.Ticks);

    public string Label(long utcTicks)
    {
        var minutes = MinutesUntilReset(utcTicks);
        if (text.IsCurrent(minutes))
        {
            return text.Value;
        }

        return text.Store(minutes, Loc.T(L.GamesHub.ResetsIn, Duration(minutes)));
    }

    private static string Duration(int minutes)
    {
        if (minutes < MinutesPerHour)
        {
            return Loc.T(L.GamesHub.MinutesShort, GameNumber.Label(minutes));
        }

        return Loc.T(L.GamesHub.HoursMinutes, GameNumber.Label(minutes / MinutesPerHour),
            GameNumber.Label(minutes % MinutesPerHour));
    }
}
