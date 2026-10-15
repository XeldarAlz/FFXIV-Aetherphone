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
    public const int OptionCount = 3;

    public static readonly int[] BetMultiples = { 10, 50, 100 };
    public static readonly int[] BuyInMultiples = { 1, 2, 5 };


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
}
