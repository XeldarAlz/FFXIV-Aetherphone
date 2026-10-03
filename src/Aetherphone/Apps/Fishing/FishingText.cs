using System.Text;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Apps.Fishing;

internal static class FishingText
{
    private const string Separator = " · ";
    private const int MinutesPerHour = 60;

    public static string Predators(FishingCatalog catalog, FishPredator[] predators)
    {
        if (predators.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < predators.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(Separator);
            }

            var predator = predators[index];
            builder.Append(Loc.T(L.Fishing.PredatorCount, predator.Count, catalog.ItemName(predator.ItemId)));
        }

        return builder.ToString();
    }

    public static string TimeOfDay(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => Loc.T(L.Fishing.Sunset),
            OceanTimeOfDay.Night => Loc.T(L.Fishing.Night),
            _ => Loc.T(L.Fishing.Day),
        };

    public static Vector4 TimeOfDayTint(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => Core.Theme.Accent.Rose,
            OceanTimeOfDay.Night => Core.Theme.Accent.Violet,
            _ => Core.Theme.Accent.Amber,
        };

    public static FontAwesomeIcon TimeOfDayIcon(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => FontAwesomeIcon.CloudSun,
            OceanTimeOfDay.Night => FontAwesomeIcon.Moon,
            _ => FontAwesomeIcon.Sun,
        };

    public static string LocalDayClock(long unixSeconds)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
        return string.Concat(local.ToString("ddd", Loc.Culture), " ", TimeText.Clock(local));
    }

    public static string LocalClock(long unixSeconds) =>
        TimeText.Clock(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime);

    public static string Relative(long seconds) => TimeFormat.Relative(TimeSpan.FromSeconds(Math.Max(0L, seconds)));

    public static string EorzeaClock(int minuteOfDay)
    {
        var wrapped = minuteOfDay % FishWindowRule.MinutesPerDay;
        return TimeText.Clock(new DateTime(2000, 1, 1, wrapped / MinutesPerHour, wrapped % MinutesPerHour, 0,
            DateTimeKind.Unspecified));
    }

    public static string Hookset(FishHookset hookset) =>
        hookset switch
        {
            FishHookset.Precision => Loc.T(L.Fishing.PrecisionHookset),
            FishHookset.Powerful => Loc.T(L.Fishing.PowerfulHookset),
            _ => string.Empty,
        };

    public static string Tug(FishTug tug) =>
        tug switch
        {
            FishTug.Light => Loc.T(L.Fishing.LightTug),
            FishTug.Medium => Loc.T(L.Fishing.MediumTug),
            FishTug.Heavy => Loc.T(L.Fishing.HeavyTug),
            _ => string.Empty,
        };

    public static string Join(string first, string second)
    {
        if (first.Length == 0)
        {
            return second;
        }

        return second.Length == 0 ? first : string.Concat(first, Separator, second);
    }
}
