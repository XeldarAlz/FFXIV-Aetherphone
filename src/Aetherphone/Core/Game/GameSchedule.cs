namespace Aetherphone.Core.Game;

internal enum OceanTimeOfDay
{
    Day,
    Sunset,
    Night,
}

internal readonly record struct TimerWindow(bool Active, DateTime NextChangeUtc);

internal readonly record struct OceanVoyage(
    DateTime NextBoardingUtc,
    bool BoardingNow,
    string Route,
    OceanTimeOfDay TimeOfDay);

internal readonly record struct OceanVoyageSlot(DateTime BoardingUtc, bool BoardingNow, char Destination, char Time);

internal static class GameSchedule
{
    private const int DailyResetHour = 15;
    private const int GrandCompanyResetHour = 20;
    private const int WeeklyResetHour = 8;
    private const int FashionReportOpenHour = 8;
    private const int JumboCactpotHour = 8;
    private const int OceanVoyageEpochIndex = 88;
    private const int OceanVoyagePatternLength = 144;
    private const long OceanVoyageWindowSeconds = 7200;
    private static readonly TimeSpan OceanBoardingWindow = TimeSpan.FromMinutes(15);

    private static readonly string[] OceanPattern =
    {
        "BD", "TD", "ND", "RD", "BS", "TS", "NS", "RS", "BN", "TN", "NN", "RN", "TD", "ND", "RD", "BS", "TS", "NS",
        "RS", "BN", "TN", "NN", "RN", "BD", "ND", "RD", "BS", "TS", "NS", "RS", "BN", "TN", "NN", "RN", "BD", "TD",
        "RD", "BS", "TS", "NS", "RS", "BN", "TN", "NN", "RN", "BD", "TD", "ND", "BS", "TS", "NS", "RS", "BN", "TN",
        "NN", "RN", "BD", "TD", "ND", "RD", "TS", "NS", "RS", "BN", "TN", "NN", "RN", "BD", "TD", "ND", "RD", "BS",
        "NS", "RS", "BN", "TN", "NN", "RN", "BD", "TD", "ND", "RD", "BS", "TS", "RS", "BN", "TN", "NN", "RN", "BD",
        "TD", "ND", "RD", "BS", "TS", "NS", "BN", "TN", "NN", "RN", "BD", "TD", "ND", "RD", "BS", "TS", "NS", "RS",
        "TN", "NN", "RN", "BD", "TD", "ND", "RD", "BS", "TS", "NS", "RS", "BN", "NN", "RN", "BD", "TD", "ND", "RD",
        "BS", "TS", "NS", "RS", "BN", "TN", "RN", "BD", "TD", "ND", "RD", "BS", "TS", "NS", "RS", "BN", "TN", "NN",
    };

    public static DateTime NextDailyReset(DateTime utcNow) => NextDailyAt(utcNow, DailyResetHour);
    public static DateTime NextGrandCompanyReset(DateTime utcNow) => NextDailyAt(utcNow, GrandCompanyResetHour);
    public static DateTime NextWeeklyReset(DateTime utcNow) => NextWeeklyAt(utcNow, DayOfWeek.Tuesday, WeeklyResetHour);

    public static DateTime NextJumboCactpot(DateTime utcNow) =>
        NextWeeklyAt(utcNow, DayOfWeek.Saturday, JumboCactpotHour);

    public static TimerWindow FashionReport(DateTime utcNow)
    {
        var nextOpen = NextWeeklyAt(utcNow, DayOfWeek.Friday, FashionReportOpenHour);
        var nextClose = NextWeeklyAt(utcNow, DayOfWeek.Tuesday, WeeklyResetHour);
        return nextClose < nextOpen ? new TimerWindow(true, nextClose) : new TimerWindow(false, nextOpen);
    }

    public static OceanVoyage OceanFishing(DateTime utcNow)
    {
        var boarding = NextOceanBoarding(utcNow, out var boardingNow);
        var code = RouteCode(boarding);
        return new OceanVoyage(boarding, boardingNow, RouteName(code[0]), TimeOfDay(code[1]));
    }

    public static void UpcomingOceanVoyages(DateTime utcNow, OceanVoyageSlot[] destination)
    {
        if (destination.Length == 0)
        {
            return;
        }

        var firstBoarding = NextOceanBoarding(utcNow, out var firstBoardingNow);
        for (var index = 0; index < destination.Length; index++)
        {
            var boarding = firstBoarding.AddHours(2 * index);
            var code = RouteCode(boarding);
            var boardingNow = index == 0 && firstBoardingNow;
            destination[index] = new OceanVoyageSlot(boarding, boardingNow, code[0], code[1]);
        }
    }

    private static DateTime NextOceanBoarding(DateTime utcNow, out bool boardingNow)
    {
        var evenHour = utcNow.Hour - (utcNow.Hour % 2);
        var windowStart = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, evenHour, 0, 0, DateTimeKind.Utc);
        boardingNow = utcNow - windowStart < OceanBoardingWindow;
        return boardingNow ? windowStart : windowStart.AddHours(2);
    }

    private static string RouteCode(DateTime boardingUtc)
    {
        var voyageNumber = new DateTimeOffset(boardingUtc).ToUnixTimeSeconds() / OceanVoyageWindowSeconds;
        var index =
            (int)(((OceanVoyageEpochIndex + voyageNumber) % OceanVoyagePatternLength + OceanVoyagePatternLength) %
                  OceanVoyagePatternLength);
        return OceanPattern[index];
    }

    private static string RouteName(char destination) =>
        destination switch
        {
            'B' => "Bloodbrine",
            'T' => "Rothlyt",
            'N' => "Northern",
            'R' => "Rhotano",
            _ => string.Empty,
        };

    private static OceanTimeOfDay TimeOfDay(char time) =>
        time switch
        {
            'S' => OceanTimeOfDay.Sunset,
            'N' => OceanTimeOfDay.Night,
            _ => OceanTimeOfDay.Day,
        };

    private static DateTime NextDailyAt(DateTime utcNow, int hour)
    {
        var candidate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, hour, 0, 0, DateTimeKind.Utc);
        return candidate > utcNow ? candidate : candidate.AddDays(1);
    }

    private static DateTime NextWeeklyAt(DateTime utcNow, DayOfWeek day, int hour)
    {
        var todayAt = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, hour, 0, 0, DateTimeKind.Utc);
        var daysUntil = ((int)day - (int)utcNow.DayOfWeek + 7) % 7;
        var candidate = todayAt.AddDays(daysUntil);
        return candidate > utcNow ? candidate : candidate.AddDays(7);
    }
}
