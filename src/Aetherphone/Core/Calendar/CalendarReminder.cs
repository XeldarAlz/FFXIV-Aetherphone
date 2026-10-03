using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Calendar;

internal static class CalendarReminder
{
    public const int None = -1;
    public const int AtEventTime = 0;
    private const int AtEventTimeIndex = 1;

    public static ReadOnlySpan<int> LeadOptionsMinutes => new[] { None, AtEventTime, 5, 10, 15, 30, 60, 120, 1440 };

    private static readonly LocString[] LeadOptionLabels =
    {
        L.Calendar.AlertNone, L.Calendar.AlertAtTime, L.Calendar.AlertMinutes5, L.Calendar.AlertMinutes10,
        L.Calendar.AlertMinutes15, L.Calendar.AlertMinutes30, L.Calendar.AlertHour1, L.Calendar.AlertHours2,
        L.Calendar.AlertDay1,
    };

    public static int LeadIndexOf(int minutesBefore)
    {
        for (var index = 0; index < LeadOptionsMinutes.Length; index++)
        {
            if (LeadOptionsMinutes[index] == minutesBefore)
            {
                return index;
            }
        }

        return AtEventTimeIndex;
    }

    public static LocString LeadLabelAt(int leadIndex) => LeadOptionLabels[leadIndex];

    public static bool TryFireTime(CalendarCustomEvent calendarEvent, out DateTime fireTime)
    {
        if (calendarEvent.ReminderMinutesBefore == None)
        {
            fireTime = default;
            return false;
        }

        fireTime = calendarEvent.When.AddMinutes(-Math.Max(AtEventTime, calendarEvent.ReminderMinutesBefore));
        return true;
    }

    public static string Body(DateTime when, DateTime now)
    {
        if (when.Date == now.Date)
        {
            return TimeText.Clock(when);
        }

        return string.Concat(when.ToString("MMM d", Loc.Culture), ", ", TimeText.Clock(when));
    }
}
