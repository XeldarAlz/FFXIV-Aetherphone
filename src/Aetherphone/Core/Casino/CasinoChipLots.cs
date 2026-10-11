namespace Aetherphone.Core.Casino;

internal static class CasinoChipLots
{
    public const long ChipPerCoin = 1000;

    public static long CoinsFor(long chips)
    {
        return chips / ChipPerCoin;
    }
}
