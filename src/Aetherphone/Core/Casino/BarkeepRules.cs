namespace Aetherphone.Core.Casino;

internal static class BarkeepRules
{
    public const int ShiftSeconds = 60;

    public const int MinFinishSeconds = 60;

    public const int MaxFinishSeconds = 300;

    public const int SecondsPerOrder = 4;

    public const int MinPatrons = 8;

    public const int MaxPatrons = 14;

    public const int StepKindCount = 4;

    public const int PourKind = 0;

    public const int ShakeKind = 1;

    public const int LayerKind = 2;

    public const int GarnishKind = 3;

    public const int PerfectGrade = 100;

    public const int RatioScale = 1000;

    public const long EntryChips = 2_000;

    public const long MaxPayout = 4_000;

    public const long DailyNetWinCap = 20_000;

    public const int TopPayTenths = (int)(MaxPayout * 10 / EntryChips);

    public static readonly int[] GradeDomain = { 0, 40, 70, 100 };

    public static readonly int[] PatronBuckets = { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 13, 14 };

    public static readonly int[] LadderRatioFloors = { 400, 600, 800, 950 };

    public static readonly long[] LadderPays = { 1_000, EntryChips, 3_000, MaxPayout };

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

    public static int RatioOf(int score, int maxScore)
    {
        if (maxScore <= 0 || score <= 0)
        {
            return 0;
        }

        return (int)(score * (long)RatioScale / maxScore);
    }

    public static int LadderBand(int score, int maxScore)
    {
        var ratio = RatioOf(score, maxScore);
        var band = -1;
        for (var floorIndex = 0; floorIndex < LadderRatioFloors.Length; floorIndex++)
        {
            if (ratio >= LadderRatioFloors[floorIndex])
            {
                band = floorIndex;
            }
        }

        return band;
    }

    public static long LadderPayout(int score, int maxScore)
    {
        var band = LadderBand(score, maxScore);
        return band < 0 ? 0 : LadderPays[band];
    }

    public static int PointsForBand(int band, int maxScore)
    {
        if (band < 0 || band >= LadderRatioFloors.Length || maxScore <= 0)
        {
            return 0;
        }

        var scaled = (long)LadderRatioFloors[band] * maxScore;
        return (int)((scaled + RatioScale - 1) / RatioScale);
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
