namespace Aetherphone.Core.Theme;

internal static class SeasonalTheme
{
    private const int HalloweenStartMonth = 10;
    private const int HalloweenStartDay = 15;
    private const int HalloweenEndMonth = 11;
    private const int HalloweenEndDay = 2;
    private const int HalloweenNightDay = 31;

    public static bool Halloween { get; private set; }

    public static bool HalloweenByDate { get; private set; }

    public static bool BlackletterNames { get; private set; }

    public static bool Parallax { get; private set; }

    public static bool IsHalloweenDate(DateTime date) => DayOfHalloween(date) >= 0;

    public static int DayOfHalloween(DateTime date)
    {
        if (date.Month == HalloweenStartMonth && date.Day >= HalloweenStartDay)
        {
            return date.Day - HalloweenStartDay;
        }

        return IsAfterHalloweenNight(date)
            ? DateTime.DaysInMonth(date.Year, HalloweenStartMonth) - HalloweenStartDay + date.Day
            : -1;
    }

    public static DateTime HalloweenNightOf(int year) => new(year, HalloweenStartMonth, HalloweenNightDay);

    public static bool IsAfterHalloweenNight(DateTime date) =>
        date.Month == HalloweenEndMonth && date.Day <= HalloweenEndDay;

    public static bool IsHalloweenNight(DateTime date) =>
        Halloween && date.Month == HalloweenStartMonth && date.Day == HalloweenNightDay;

    public static void Update(Configuration configuration, DateTime now)
    {
        HalloweenByDate = IsHalloweenDate(now);
        var previewing = AepConstants.IsPrerelease && configuration.PreviewHalloween;
        Halloween = configuration.SeasonalDecorations && (HalloweenByDate || previewing);
        BlackletterNames = Halloween && configuration.SeasonalNameFont;
        Parallax = Halloween && configuration.SeasonalParallax;
    }
}
