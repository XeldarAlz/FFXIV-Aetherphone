namespace Aetherphone.Core.Casino;

internal static class AutoTopUpRule
{
    public const long RetryAfterRefusalMilliseconds = 30_000;

    public static long CoinsFor(bool enabled, long needChips, long stackChips, long walletCoins, long rate)
    {
        var stack = Math.Max(0, stackChips);
        if (!enabled || needChips <= stack || rate <= 0 || walletCoins < ChipsAmounts.MinimumCoins)
        {
            return 0;
        }

        var target = needChips * ChipsAmounts.BetMultiples[0];
        var coins = Math.Min(walletCoins, ChipsAmounts.CoinsToReach(target, stack, rate));
        return stack + coins * rate >= needChips ? coins : 0;
    }

    public static bool Resting(long refusedAtTick, long nowTick) =>
        refusedAtTick != 0 && nowTick - refusedAtTick < RetryAfterRefusalMilliseconds;
}
