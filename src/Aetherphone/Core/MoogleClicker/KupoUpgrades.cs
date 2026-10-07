namespace Aetherphone.Core.MoogleClicker;

internal static class KupoUpgrades
{
    public const int TiersPerBuilding = 5;
    public const int BuildingUpgrades = KupoBuildings.Count * TiersPerBuilding;
    public const int TapUpgrades = 4;
    public const int Count = BuildingUpgrades + TapUpgrades;
    public const int TapDoublings = 2;
    private const ulong TierMask = (1UL << TiersPerBuilding) - 1UL;

    private static readonly int[] TierThresholds = { 1, 5, 25, 50, 100 };
    private static readonly double[] TierCostFactors = { 10d, 60d, 600d, 6_000d, 60_000d };
    private static readonly double[] TapCosts = { 100d, 5_000d, 50_000d, 5e7 };
    private static readonly double[] TapUnlocks = { 50d, 1_000d, 10_000d, 1e7 };
    private static readonly double[] TapShares = { 0d, 0d, 0.01d, 0.02d };

    public static bool IsBuilding(int upgrade) => upgrade < BuildingUpgrades;

    public static int Building(int upgrade) => upgrade / TiersPerBuilding;

    public static int Tier(int upgrade) => upgrade % TiersPerBuilding;

    public static int TapIndex(int upgrade) => upgrade - BuildingUpgrades;

    public static int ForBuilding(int building, int tier) => building * TiersPerBuilding + tier;

    public static int ForTap(int tapIndex) => BuildingUpgrades + tapIndex;

    public static ulong Bit(int upgrade) => 1UL << upgrade;

    public static int Threshold(int tier) => TierThresholds[tier];

    public static double TapShare(int tapIndex) => TapShares[tapIndex];

    public static bool Doubles(int upgrade) => IsBuilding(upgrade) || TapIndex(upgrade) < TapDoublings;

    public static double Cost(int upgrade) =>
        IsBuilding(upgrade)
            ? KupoBuildings.BaseCost(Building(upgrade)) * TierCostFactors[Tier(upgrade)]
            : TapCosts[TapIndex(upgrade)];

    public static bool Unlocked(int upgrade, ReadOnlySpan<int> owned, double ledgerKupo) =>
        IsBuilding(upgrade)
            ? owned[Building(upgrade)] >= TierThresholds[Tier(upgrade)]
            : ledgerKupo >= TapUnlocks[TapIndex(upgrade)];

    public static int TiersOwned(ulong mask, int building) =>
        BitOperations.PopCount((mask >> (building * TiersPerBuilding)) & TierMask);
}
