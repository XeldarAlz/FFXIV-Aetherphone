using Aetherphone.Core.Clock;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Clock;

internal static class AlarmSchedule
{
    private const byte WeekdayMask =
        (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Tuesday) | (1 << (int)DayOfWeek.Wednesday) |
        (1 << (int)DayOfWeek.Thursday) | (1 << (int)DayOfWeek.Friday);

    private const byte WeekendMask = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);
    private const byte EveryDayMask = 0x7F;

    private static readonly DayOfWeek[] DisplayOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    public static long MinuteKey(DateTime localTime) =>
        (localTime.Year * 366L + localTime.DayOfYear) * 1440L + localTime.Hour * 60L + localTime.Minute;

    public static string RepeatLabel(AlarmEntry alarm)
    {
        if (alarm.RepeatDays == 0)
        {
            return Loc.T(L.Clock.RepeatNever);
        }

        if (alarm.RepeatDays == EveryDayMask)
        {
            return Loc.T(L.Clock.RepeatEveryDay);
        }

        if (alarm.RepeatDays == WeekdayMask)
        {
            return Loc.T(L.Clock.RepeatWeekdays);
        }

        if (alarm.RepeatDays == WeekendMask)
        {
            return Loc.T(L.Clock.RepeatWeekends);
        }

        var abbreviations = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        var names = new List<string>(7);
        for (var index = 0; index < DisplayOrder.Length; index++)
        {
            var day = DisplayOrder[index];
            if (alarm.RepeatsOn(day))
            {
                names.Add(abbreviations[(int)day]);
            }
        }

        return string.Join(", ", names);
    }

    public static DateTime NextOccurrence(AlarmEntry alarm, DateTime nowLocal)
    {
        var today = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, alarm.Hour, alarm.Minute, 0);
        if (alarm.RepeatDays == 0)
        {
            return today > nowLocal ? today : today.AddDays(1);
        }

        for (var offset = 0; offset < 8; offset++)
        {
            var candidate = today.AddDays(offset);
            if (alarm.RepeatsOn(candidate.DayOfWeek) && candidate > nowLocal)
            {
                return candidate;
            }
        }

        return today.AddDays(1);
    }
}
