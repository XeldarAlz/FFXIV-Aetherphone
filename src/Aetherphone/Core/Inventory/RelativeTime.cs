using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Inventory;

internal static class RelativeTime
{
    public static string Ago(DateTime utc)
    {
        if (utc == default)
        {
            return "—";
        }

        var delta = DateTime.UtcNow - utc;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta.TotalSeconds < 60)
        {
            return Loc.T(L.Time.JustNow);
        }

        if (delta.TotalMinutes < 60)
        {
            return Loc.T(L.Time.MinutesAgo, (int)delta.TotalMinutes);
        }

        if (delta.TotalHours < 24)
        {
            return Loc.T(L.Time.HoursAgo, (int)delta.TotalHours);
        }

        return Loc.T(L.Time.DaysAgo, (int)delta.TotalDays);
    }
}
