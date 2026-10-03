using System.Globalization;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Fishing;

internal static class FishingClock
{
    private const long SecondsPerHour = 3600;
    private const long SecondsPerMinute = 60;

    public static string Countdown(long seconds)
    {
        var clamped = Math.Max(0L, seconds);
        if (clamped < SecondsPerHour)
        {
            return TimeText.MinutesSeconds((int)clamped);
        }

        var hours = clamped / SecondsPerHour;
        var minutes = clamped / SecondsPerMinute % SecondsPerMinute;
        var remainder = clamped % SecondsPerMinute;
        return string.Concat(hours.ToString(CultureInfo.InvariantCulture), ":",
            minutes.ToString("D2", CultureInfo.InvariantCulture), ":",
            remainder.ToString("D2", CultureInfo.InvariantCulture));
    }

    public static string IslandStatus(FishingIslandKind kind, bool open, string countdown)
    {
        if (kind == FishingIslandKind.Voyage)
        {
            return open
                ? Loc.T(L.Fishing.BoardingClosesIn, countdown)
                : Loc.T(L.Fishing.BoardingIn, countdown);
        }

        return open ? Loc.T(L.Fishing.UpForCountdown, countdown) : Loc.T(L.Fishing.OpensInCountdown, countdown);
    }
}
