namespace Aetherphone.Core.Casino;

internal readonly record struct ScratchPrizeRow(long Chips, int CountPerMillion);

internal static class ScratchRules
{
    public const int TierCount = 4;

    public const int CellCount = 9;

    public const int GridSide = 3;

    public const int PrizeSymbolCount = 4;

    public const int SymbolCount = 7;

    public const int TableScale = 1_000_000;

    public const int MatchesToWin = 3;

    public static readonly long[] Prices = { 500, 1_000, 2_500, 5_000 };

    public static readonly ScratchPrizeRow[][] PrizeTables =
    {
        new ScratchPrizeRow[]
        {
            new(1_000, 285_000),
            new(2_500, 50_000),
            new(5_000, 7_500),
            new(10_000, 1_400),
        },
        new ScratchPrizeRow[]
        {
            new(2_000, 285_000),
            new(5_000, 50_000),
            new(10_000, 7_500),
            new(20_000, 1_400),
        },
        new ScratchPrizeRow[]
        {
            new(5_000, 285_000),
            new(12_500, 51_000),
            new(25_000, 7_600),
            new(50_000, 1_450),
        },
        new ScratchPrizeRow[]
        {
            new(10_000, 286_000),
            new(25_000, 52_000),
            new(50_000, 7_800),
            new(100_000, 1_500),
        },
    };

    public static bool IsValidTier(int tier)
    {
        return tier >= 0 && tier < TierCount;
    }

    public static int TierForPrice(long price)
    {
        for (var tier = 0; tier < TierCount; tier++)
        {
            if (Prices[tier] == price)
            {
                return tier;
            }
        }

        return -1;
    }

    public static int ReturnTenths(int tier)
    {
        if (!IsValidTier(tier))
        {
            return 0;
        }

        var table = PrizeTables[tier];
        var returned = 0L;
        for (var prizeIndex = 0; prizeIndex < table.Length; prizeIndex++)
        {
            returned += table[prizeIndex].Chips * table[prizeIndex].CountPerMillion;
        }

        return (int)(returned * 1000 / ((long)TableScale * Prices[tier]));
    }

    public static long WinCountPerMillion(int tier)
    {
        var table = PrizeTables[tier];
        var total = 0L;
        for (var prizeIndex = 0; prizeIndex < table.Length; prizeIndex++)
        {
            total += table[prizeIndex].CountPerMillion;
        }

        return total;
    }

    public static bool AreValidCells(ReadOnlySpan<int> cells)
    {
        if (cells.Length != CellCount)
        {
            return false;
        }

        for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            if (cells[cellIndex] < 0 || cells[cellIndex] >= SymbolCount)
            {
                return false;
            }
        }

        return true;
    }

    public static int WinningSymbol(ReadOnlySpan<int> cells)
    {
        Span<int> counts = stackalloc int[SymbolCount];
        for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            counts[cells[cellIndex]]++;
        }

        for (var symbol = 0; symbol < SymbolCount; symbol++)
        {
            if (counts[symbol] >= MatchesToWin)
            {
                return symbol;
            }
        }

        return -1;
    }
}
