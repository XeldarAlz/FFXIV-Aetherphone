namespace Aetherphone.Core.Casino;

internal enum ChipsNeedKind : byte
{
    Bet,
    BuyIn,
}

internal readonly record struct ChipsOption(long Coins, long Chips, long Covers);

internal static class ChipsAmounts
{
    public const long MinimumCoins = 1;
    public const int QuickCount = 3;
    public const int OptionCount = 3;

    public static readonly int[] BetMultiples = { 10, 50, 100 };
    public static readonly int[] BuyInMultiples = { 1, 2, 5 };

    private static readonly int[] WalletShares = { 100, 20, 4 };
    private static readonly long[] NiceSteps = { 1, 2, 5 };

    public static int[] MultiplesFor(ChipsNeedKind kind) => kind == ChipsNeedKind.BuyIn ? BuyInMultiples : BetMultiples;

    public static int ForNeed(long needChips, long stackChips, long walletCoins, long rate, ChipsNeedKind kind,
        Span<ChipsOption> into)
    {
        if (needChips <= 0 || rate <= 0 || walletCoins < MinimumCoins)
        {
            return 0;
        }

        var multiples = MultiplesFor(kind);
        var stack = Math.Max(0, stackChips);
        var count = 0;
        for (var multipleIndex = 0; multipleIndex < multiples.Length && count < into.Length; multipleIndex++)
        {
            var coins = Math.Min(walletCoins, CoinsToReach(needChips * multiples[multipleIndex], stack, rate));
            if (count > 0 && into[count - 1].Coins >= coins)
            {
                continue;
            }

            var chips = coins * rate;
            into[count] = new ChipsOption(coins, chips, (stack + chips) / needChips);
            count++;
        }

        return count;
    }

    public static long CoinsToReach(long targetChips, long stackChips, long rate) =>
        Math.Max(MinimumCoins, CoinsFor(targetChips - Math.Max(0, stackChips), rate));

    public static long CoinsFor(long chips, long rate)
    {
        if (chips <= 0 || rate <= 0)
        {
            return 0;
        }

        return chips / rate + (chips % rate == 0 ? 0 : 1);
    }

    public static long NiceFloor(long value)
    {
        if (value < 1)
        {
            return 0;
        }

        var magnitude = 1L;
        while (magnitude <= value / 10)
        {
            magnitude *= 10;
        }

        var floored = magnitude;
        for (var stepIndex = 0; stepIndex < NiceSteps.Length; stepIndex++)
        {
            var candidate = NiceSteps[stepIndex] * magnitude;
            if (candidate <= value)
            {
                floored = candidate;
            }
        }

        return floored;
    }

    public static int Quick(long walletCoins, Span<long> into)
    {
        var count = 0;
        if (walletCoins < MinimumCoins)
        {
            return 0;
        }

        for (var shareIndex = 0; shareIndex < WalletShares.Length && count < into.Length; shareIndex++)
        {
            var amount = Math.Max(MinimumCoins, NiceFloor(walletCoins / WalletShares[shareIndex]));
            if (amount > walletCoins || (count > 0 && into[count - 1] >= amount))
            {
                continue;
            }

            into[count] = amount;
            count++;
        }

        return count;
    }
}
