namespace Aetherphone.Core.Casino;

internal static class CasinoLimits
{
    public const long SelfLimitFloor = 50 * CasinoChipLots.ChipPerCoin;
    public const long FallbackCeiling = 100_000 * CasinoChipLots.ChipPerCoin;
    public const long SuggestedLimit = 500 * CasinoChipLots.ChipPerCoin;
}
