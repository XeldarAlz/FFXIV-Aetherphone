namespace Aetherphone.Core.Casino;

internal static class CasinoChipLots
{
    public const long ChipPerCoin = 1000;

    public const long MaxSingleWinCoins = 5_000;

    public const long MaxSingleWin = MaxSingleWinCoins * ChipPerCoin;

    public const long JackpotSeedCoins = 10_000;

    public const long JackpotCapCoins = 100_000;

    public static readonly long[] Chips =
    {
        20_000,
        50_000,
        100_000,
        250_000,
        500_000,
        1_000_000,
        2_000_000,
    };

    public static long CoinsFor(long chips)
    {
        return chips / ChipPerCoin;
    }

    public static long ToWholeCoins(long chips)
    {
        if (chips <= 0)
        {
            return 0;
        }

        return chips / ChipPerCoin * ChipPerCoin;
    }

    public static long PreselectFor(long minAmount, long ceiling)
    {
        var best = 0L;
        for (var index = 0; index < Chips.Length; index++)
        {
            var lot = Chips[index];
            if (lot >= minAmount && lot <= ceiling)
            {
                best = lot;
            }
        }

        return best;
    }

    public static bool IsAffordable(long lot, long minAmount, long ceiling)
    {
        return lot >= minAmount && lot <= ceiling;
    }

    public static long RemainderFor(long minAmount, long ceiling)
    {
        var whole = ToWholeCoins(ceiling);
        if (whole < minAmount)
        {
            return 0;
        }

        for (var index = 0; index < Chips.Length; index++)
        {
            if (Chips[index] == whole)
            {
                return 0;
            }
        }

        return whole;
    }
}
