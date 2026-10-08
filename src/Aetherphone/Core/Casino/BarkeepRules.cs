namespace Aetherphone.Core.Casino;

internal static class BarkeepRules
{
    public const int ShiftSeconds = 60;

    public const int MinFinishSeconds = 60;

    public const int MaxFinishSeconds = 300;

    public const int SecondsPerOrder = 4;

    public const int MaxPatrons = 14;

    public const int StepKindCount = 4;

    public const int PourKind = 0;

    public const int ShakeKind = 1;

    public const int LayerKind = 2;

    public const int GarnishKind = 3;

    public const int PerfectGrade = 100;

    public const long MaxPayout = 30_000;

    public const long EntryChips = 20_000;

    public const long DailyNetWinCap = 100_000;

    public static readonly int[] GradeDomain = { 0, 40, 70, 100 };

    public static readonly int[] PatronBuckets = { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 13, 14 };

    public static readonly int[] LadderScoreFloors = { 450, 750, 1000, 1200 };

    public static readonly long[] LadderPays = { 9_000, 15_000, 23_000, MaxPayout };

    public static bool IsValidGrade(int grade)
    {
        for (var domainIndex = 0; domainIndex < GradeDomain.Length; domainIndex++)
        {
            if (GradeDomain[domainIndex] == grade)
            {
                return true;
            }
        }

        return false;
    }

    public static long LadderPayout(int score)
    {
        var band = LadderBand(score);
        return band < 0 ? 0 : LadderPays[band];
    }

    public static int LadderBand(int score)
    {
        var band = -1;
        for (var floorIndex = 0; floorIndex < LadderScoreFloors.Length; floorIndex++)
        {
            if (score >= LadderScoreFloors[floorIndex])
            {
                band = floorIndex;
            }
        }

        return band;
    }

    public static int OrderScore(int[] grades)
    {
        var sum = 0;
        for (var gradeIndex = 0; gradeIndex < grades.Length; gradeIndex++)
        {
            sum += grades[gradeIndex];
        }

        return sum / grades.Length;
    }

    public static int MaxOrdersForElapsed(int elapsedSeconds, int patronCount)
    {
        var byTime = elapsedSeconds / SecondsPerOrder;
        return byTime < patronCount ? byTime : patronCount;
    }
}
