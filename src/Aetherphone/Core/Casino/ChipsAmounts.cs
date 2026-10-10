namespace Aetherphone.Core.Casino;

internal static class ChipsAmounts
{
    public const long MinimumCoins = 1;
    public const int QuickCount = 3;

    private static readonly int[] WalletShares = { 100, 20, 4 };
    private static readonly long[] NiceSteps = { 1, 2, 5 };

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
