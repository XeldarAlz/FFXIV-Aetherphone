namespace Aetherphone.Core.Casino;

internal static class RaceRules
{
    public const int FieldSize = 8;

    public const int BirdBank = 24;

    public const int StrengthBase = 60;

    public const int StrengthSpread = 140;

    public const int OverroundBasisPoints = 10200;

    public const int OverroundPercent = 102;

    public const int MinOddsHundredths = 130;

    public const int MaxOddsHundredths = 4000;

    public const int MinPlaceOddsHundredths = 110;

    public const int MaxPlaceOddsHundredths = 4000;

    public const long MinBet = 100;

    public const int MaxTickets = 6;

    public const int PlacesPaid = 3;

    public const int FormLength = 5;

    public const int MinRating = 1;

    public const int MaxRating = 5;

    public const int ColourCount = 12;

    public const int SilkCount = 6;

    public const int MillisecondsPerTick = 1000 / RaceScript.TicksPerSecond;

    public const int ReturnTenths = 980;

    public const int KindWin = 0;

    public const int KindPlace = 1;

    public const int KindForecast = 2;

    public const int KindReverse = 3;

    public const int KindCount = 4;

    public const int NoRunner = -1;

    public const int ResultRows = 6;

    private const long HundredthsPerUnit = 100;

    private const long ForecastScale = 10000;

    public static bool IsKind(int kind) => kind >= KindWin && kind < KindCount;

    public static bool IsRunner(int slot) => slot >= 0 && slot < FieldSize;

    public static bool IsPair(int kind) => kind is KindForecast or KindReverse;

    public static bool IsTicket(int kind, int runner, int runnerB)
    {
        if (!IsKind(kind) || !IsRunner(runner))
        {
            return false;
        }

        if (!IsPair(kind))
        {
            return runnerB == NoRunner;
        }

        return IsRunner(runnerB) && runnerB != runner;
    }

    public static int TicketKey(int kind, int runner, int runnerB) =>
        kind * FieldSize * (FieldSize + 1) + runner * (FieldSize + 1) + runnerB + 1;

    public static int WinOddsHundredths(int strengthSum, int strength)
    {
        if (strength <= 0)
        {
            return MaxOddsHundredths;
        }

        var odds = ForecastScale * strengthSum / ((long)OverroundPercent * strength);
        return (int)Math.Clamp(odds, MinOddsHundredths, MaxOddsHundredths);
    }

    public static long ForecastPayHundredths(int firstOddsHundredths, int secondOddsHundredths)
    {
        var margin = (long)OverroundPercent * firstOddsHundredths - ForecastScale;
        if (margin <= 0)
        {
            return 0;
        }

        return secondOddsHundredths * margin / ForecastScale;
    }

    public static long ReversePayHundredths(int firstOddsHundredths, int secondOddsHundredths) =>
        ForecastPayHundredths(firstOddsHundredths, secondOddsHundredths) / 2;

    public static long Payout(long amount, long payHundredths) =>
        amount <= 0 || payHundredths <= 0 ? 0 : amount * payHundredths / HundredthsPerUnit;

    public static long PotentialPayHundredths(int kind, int runnerOdds, int runnerPlaceOdds, int partnerOdds)
    {
        return kind switch
        {
            KindWin => runnerOdds,
            KindPlace => runnerPlaceOdds,
            KindForecast => ForecastPayHundredths(runnerOdds, partnerOdds),
            KindReverse => Math.Max(ReversePayHundredths(runnerOdds, partnerOdds),
                ReversePayHundredths(partnerOdds, runnerOdds)),
            _ => 0,
        };
    }

    public static int Rating(int strength)
    {
        var rating = 1 + (strength - StrengthBase) * MaxRating / StrengthSpread;
        return Math.Clamp(rating, MinRating, MaxRating);
    }

    public static long ElapsedSubTicks(long serverNowUnixMs, long raceStartUnixMs)
    {
        if (raceStartUnixMs <= 0 || serverNowUnixMs <= raceStartUnixMs)
        {
            return 0;
        }

        return (serverNowUnixMs - raceStartUnixMs) * RaceScript.SubTicks / MillisecondsPerTick;
    }

    public static long MillisecondsOf(long subTicks) => subTicks * MillisecondsPerTick / RaceScript.SubTicks;

    public static int PlaceOf(ReadOnlySpan<int> order, int slot)
    {
        for (var place = 0; place < order.Length; place++)
        {
            if (order[place] == slot)
            {
                return place;
            }
        }

        return -1;
    }
}
