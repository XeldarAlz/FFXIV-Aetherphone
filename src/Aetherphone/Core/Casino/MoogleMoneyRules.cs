namespace Aetherphone.Core.Casino;

internal static class MoogleMoneyRules
{
    public const int SymbolCount = 7;
    public const int Moogle = 0;
    public const int Wild = 7;
    public const int Scatter = 8;
    public const int Coin = 9;
    public const int CoinChance = 1_250;
    public const int RespinCoinChance = 330;
    public const int ChanceScale = 10_000;
    public const int TriggerCoins = 6;
    public const int AnticipationCoins = 5;
    public const int Respins = 3;
    public const long GrandUnits = 20_000_000;
    public const long MajorUnits = 5_000_000;
    public const int FreeGameScatters = 3;
    public const int FreeGames = 6;
    public const int GiantFirstReel = 1;
    public const int GiantLastReel = 3;
    public const int CoinWeightTotal = 9_339;
    public const long MiniResetUnits = 200_000;
    public const long MiniCeilingUnits = 300_000;
    public const long MiniStepUnits = 10;
    public const long MinorResetUnits = 500_000;
    public const long MinorCeilingUnits = 1_000_000;
    public const long MinorStepUnits = 8;

    public static readonly long[][] LinePays =
    {
        new long[] { 20_000, 100_000, 500_000 },
        new long[] { 15_000, 50_000, 250_000 },
        new long[] { 12_000, 40_000, 150_000 },
        new long[] { 11_000, 30_000, 120_000 },
        new long[] { 10_500, 20_000, 80_000 },
        new long[] { 10_500, 20_000, 60_000 },
        new long[] { 10_500, 15_000, 50_000 },
    };

    public static readonly long[] CoinValues =
    {
        10_000, 20_000, 30_000, 50_000, 80_000, 100_000, 150_000, 250_000, 500_000, 0, 0, 5_000_000,
    };

    public static readonly int[] CoinKinds = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3 };

    public static readonly int[] CoinWeights = { 1000, 1600, 1900, 1875, 1260, 915, 460, 193, 82, 40, 12, 2 };

    public static readonly int[] GiantWeights = { 6, 8, 10, 10, 14, 14, 14, 0, 0, 12 };

    public static readonly int[] StripLengths = { 37, 40, 40, 40, 38 };

    public static readonly int[] FreeStripLengths = { 37, 38, 38, 38, 37 };

    public static bool IsGiantReel(int reel)
    {
        return reel >= GiantFirstReel && reel <= GiantLastReel;
    }

    public static int GiantFor(int pick)
    {
        var remaining = pick;
        for (var index = 0; index < GiantWeights.Length; index++)
        {
            remaining -= GiantWeights[index];
            if (remaining < 0)
            {
                return index;
            }
        }

        return GiantWeights.Length - 1;
    }

    public static int GiantWeightTotal()
    {
        var total = 0;
        for (var index = 0; index < GiantWeights.Length; index++)
        {
            total += GiantWeights[index];
        }

        return total;
    }
}
