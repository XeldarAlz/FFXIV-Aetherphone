namespace Aetherphone.Core.Casino;

internal static class GoldenBirdRules
{
    public const int SymbolCount = 8;
    public const int Bird = 0;
    public const int Wild = 8;
    public const int Scatter = 9;
    public const int TriggerScatters = 3;
    public const int FreeSpins = 10;
    public const int RetriggerSpins = 10;
    public const int FreeSpinCap = 60;
    public const int ExpanderMinReels = 3;
    public const int ExpanderWeightTotal = 104;

    public static readonly long[][] LinePays =
    {
        new long[] { 38_000, 200_000, 5_000_000 },
        new long[] { 23_000, 75_000, 1_000_000 },
        new long[] { 15_000, 50_000, 500_000 },
        new long[] { 11_000, 40_000, 300_000 },
        new long[] { 10_500, 19_000, 50_000 },
        new long[] { 10_500, 19_000, 50_000 },
        new long[] { 10_500, 19_000, 50_000 },
        new long[] { 10_500, 19_000, 50_000 },
    };

    public static readonly long[] ScatterPays = { 0, 0, 0, 20_000, 200_000, 1_000_000 };

    public static readonly int[] ExpanderWeights = { 3, 5, 7, 9, 20, 20, 20, 20 };

    public static readonly int[] StripLengths = { 68, 72, 72, 72, 68 };

    public static readonly int[] FreeStripLengths = { 48, 51, 51, 51, 48 };

    public static long ScatterPay(int scatters)
    {
        if (scatters <= 0)
        {
            return 0;
        }

        return scatters < ScatterPays.Length ? ScatterPays[scatters] : ScatterPays[^1];
    }

    public static long ExpanderPay(int symbol, int reels)
    {
        if (symbol < 0 || symbol >= SymbolCount || reels < ExpanderMinReels)
        {
            return 0;
        }

        return LinePays[symbol][Math.Min(reels, SlotsRules.ReelCount) - SlotsRules.MinLineMatch]
            * SlotsRules.PaylineCount;
    }
}
