namespace Aetherphone.Core.Casino;

internal static class CrystalCascadeRules
{
    public const int Reels = 6;
    public const int Rows = 5;
    public const int CellCount = 30;
    public const int SymbolCount = 9;
    public const int Scatter = 9;
    public const int Orb = 10;
    public const int TriggerScatters = 4;
    public const int FreeSpins = 10;
    public const int RetriggerScatters = 3;
    public const int RetriggerSpins = 5;
    public const int FreeSpinCap = 50;
    public const long BuyCostMultiple = 100;

    public static readonly int[] BandMinimums = { 8, 10, 12 };

    public static readonly long[][] ClusterPays =
    {
        new long[] { 25_000, 50_000, 150_000 },
        new long[] { 18_000, 35_000, 80_000 },
        new long[] { 14_000, 25_000, 60_000 },
        new long[] { 11_500, 20_000, 40_000 },
        new long[] { 10_500, 12_000, 20_000 },
        new long[] { 10_500, 12_000, 20_000 },
        new long[] { 10_500, 12_000, 20_000 },
        new long[] { 10_500, 12_000, 20_000 },
        new long[] { 10_500, 12_000, 20_000 },
    };

    public static readonly long[] ScatterPays = { 0, 0, 0, 0, 30_000, 50_000, 1_000_000 };

    public static readonly int[] TumbleLadder = { 1, 2, 3, 5 };

    public static readonly int[] OrbValues = { 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 50, 100, 250, 500 };

    public static readonly int[] OrbWeights = { 300, 250, 200, 160, 120, 100, 80, 60, 40, 25, 15, 8, 4, 2, 1 };

    public static readonly int[] BaseWeights = { 90, 110, 130, 150, 244, 244, 244, 244, 244, 29, 0 };

    public static readonly int[] AnteWeights = { 90, 110, 130, 150, 225, 225, 225, 225, 225, 34, 0 };

    public static readonly int[] FreeWeights = { 40, 50, 60, 70, 146, 146, 146, 146, 146, 14, 63 };

    public static readonly int[] BuyWeights = { 40, 50, 60, 70, 132, 132, 132, 132, 132, 14, 37 };

    public static int BandOf(int count)
    {
        for (var band = BandMinimums.Length - 1; band >= 0; band--)
        {
            if (count >= BandMinimums[band])
            {
                return band;
            }
        }

        return -1;
    }

    public static long ScatterPay(int scatters)
    {
        if (scatters <= 0)
        {
            return 0;
        }

        return scatters < ScatterPays.Length ? ScatterPays[scatters] : ScatterPays[^1];
    }

    public static int LadderRung(int tumble)
    {
        return TumbleLadder[Math.Clamp(tumble, 0, TumbleLadder.Length - 1)];
    }

    public static int Total(int[] weights)
    {
        var total = 0;
        for (var index = 0; index < weights.Length; index++)
        {
            total += weights[index];
        }

        return total;
    }
}
