namespace Aetherphone.Core.Theme;

internal static class SeasonalTheme
{
    private const int HalloweenStartMonth = 10;
    private const int HalloweenStartDay = 15;
    private const int HalloweenEndMonth = 11;
    private const int HalloweenEndDay = 2;
    private const int HalloweenNightDay = 31;

    private static long checkedMinute = -1;
    private static bool halloweenDate;

    public static bool Halloween { get; private set; }

    public static bool BlackletterNames { get; private set; }

    public static bool Parallax { get; private set; }

    public static bool IsHalloweenDate(DateTime date) =>
        (date.Month == HalloweenStartMonth && date.Day >= HalloweenStartDay) ||
        (date.Month == HalloweenEndMonth && date.Day <= HalloweenEndDay);

    public static bool HalloweenByDate => halloweenDate;

    public static int DayOfHalloween(DateTime date)
    {
        if (date.Month == HalloweenStartMonth && date.Day >= HalloweenStartDay)
        {
            return date.Day - HalloweenStartDay;
        }

        if (date.Month == HalloweenEndMonth && date.Day <= HalloweenEndDay)
        {
            return DateTime.DaysInMonth(date.Year, HalloweenStartMonth) - HalloweenStartDay + date.Day;
        }

        return -1;
    }

    public static bool IsHalloweenNight(DateTime date) =>
        Halloween && date.Month == HalloweenStartMonth && date.Day == HalloweenNightDay;

    public static void Update(Configuration configuration, DateTime now)
    {
        var minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (minute != checkedMinute)
        {
            checkedMinute = minute;
            halloweenDate = IsHalloweenDate(now);
        }

        var previewing = AepConstants.IsPrerelease && configuration.PreviewHalloween;
        Halloween = configuration.SeasonalDecorations && (halloweenDate || previewing);
        BlackletterNames = Halloween && configuration.SeasonalNameFont;
        Parallax = Halloween && configuration.SeasonalParallax;
    }
}
