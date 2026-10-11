namespace Aetherphone.Core.Casino;

internal static class PlinkoRules
{
    public const string Feature = "plinko";
    public const string PegPurpose = "peg";

    public const int Low = 0;
    public const int Medium = 1;
    public const int High = 2;
    public const int RiskCount = 3;
    public const int DefaultRisk = Medium;

    public const int MinRows = 8;
    public const int MaxRows = 16;
    public const int DefaultRows = 12;
    public const int MaxSlots = MaxRows + 1;

    public const long MinBet = 100;
    public const int MaxInFlight = 10;
    public const int TenthsPerMultiple = 10;
    public const int TopTenths = 10_000;
    public const int MinReturnBasisPoints = 9_890;
    public const int MaxReturnBasisPoints = 9_920;

    public static readonly int[] RowCounts = { 8, 12, 16 };

    private static readonly int[][] Eight =
    {
        new[] { 56, 21, 11, 10, 5, 10, 11, 21, 56 },
        new[] { 130, 30, 13, 7, 4, 7, 13, 30, 130 },
        new[] { 290, 40, 15, 3, 2, 3, 15, 40, 290 },
    };

    private static readonly int[][] Twelve =
    {
        new[] { 100, 30, 16, 14, 11, 10, 5, 10, 11, 14, 16, 30, 100 },
        new[] { 330, 110, 40, 20, 11, 6, 3, 6, 11, 20, 40, 110, 330 },
        new[] { 1700, 240, 81, 20, 7, 2, 2, 2, 7, 20, 81, 240, 1700 },
    };

    private static readonly int[][] Sixteen =
    {
        new[] { 160, 90, 20, 14, 14, 12, 11, 10, 5, 10, 11, 12, 14, 14, 20, 90, 160 },
        new[] { 1100, 410, 100, 50, 30, 15, 10, 5, 3, 5, 10, 15, 30, 50, 100, 410, 1100 },
        new[] { 10000, 1300, 260, 90, 40, 20, 2, 2, 2, 2, 2, 20, 40, 90, 260, 1300, 10000 },
    };

    public static bool IsValidRows(int rows) => rows is 8 or 12 or 16;

    public static bool IsValidRisk(int risk) => risk is >= Low and <= High;

    public static bool IsValidBoard(int rows, int risk) => IsValidRows(rows) && IsValidRisk(risk);

    public static int RowsIndex(int rows) => rows switch
    {
        8 => 0,
        12 => 1,
        16 => 2,
        _ => -1,
    };

    public static int SlotCount(int rows) => rows + 1;

    public static ReadOnlySpan<int> Strip(int rows, int risk)
    {
        if (!IsValidBoard(rows, risk))
        {
            return ReadOnlySpan<int>.Empty;
        }

        return rows switch
        {
            8 => Eight[risk],
            12 => Twelve[risk],
            _ => Sixteen[risk],
        };
    }

    public static int MultiplierTenths(int rows, int risk, int slot)
    {
        var strip = Strip(rows, risk);
        return slot >= 0 && slot < strip.Length ? strip[slot] : 0;
    }

    public static int EdgeTenths(int rows, int risk) => MultiplierTenths(rows, risk, 0);

    public static long Payout(long stake, int tenths) => stake <= 0 || tenths <= 0 ? 0 : stake * tenths / TenthsPerMultiple;

    public static long EdgeOneIn(int rows) => IsValidRows(rows) ? 1L << (rows - 1) : 0;

    public static double ReturnOf(int rows, int risk)
    {
        var strip = Strip(rows, risk);
        if (strip.Length == 0)
        {
            return 0.0;
        }

        var total = 0.0;
        var combinations = 1.0;
        for (var slot = 0; slot <= rows; slot++)
        {
            total += combinations * strip[slot];
            combinations = combinations * (rows - slot) / (slot + 1);
        }

        return total / Math.Pow(2, rows) / TenthsPerMultiple;
    }

    public static int ReturnBasisPoints(int rows, int risk) => (int)Math.Round(ReturnOf(rows, risk) * 10_000);

    public static int ReturnTenths(int rows, int risk) => (int)Math.Round(ReturnOf(rows, risk) * 1_000);

    public static int SlotOf(ReadOnlySpan<int> path)
    {
        var slot = 0;
        for (var row = 0; row < path.Length; row++)
        {
            slot += path[row];
        }

        return slot;
    }

    public static bool IsPath(ReadOnlySpan<int> path, int rows, int slot)
    {
        if (!IsValidRows(rows) || path.Length != rows)
        {
            return false;
        }

        var rights = 0;
        for (var row = 0; row < path.Length; row++)
        {
            var bit = path[row];
            if (bit is not (0 or 1))
            {
                return false;
            }

            rights += bit;
        }

        return rights == slot;
    }
}
