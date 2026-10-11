namespace Aetherphone.Core.Casino;

internal enum SlotsLayout : byte
{
    Lines,
    Cluster,
    Hold,
}

internal readonly record struct SlotsMachineInfo(
    string Id,
    SlotsLayout Layout,
    int Reels,
    int Rows,
    int VolatilityBars,
    int MaxWinMultiple,
    int ReturnBasisPoints,
    int AnteReturnBasisPoints,
    int BuyReturnBasisPoints,
    int HitBasisPoints,
    int AnteHitBasisPoints,
    int BonusOneIn,
    int AnteBonusOneIn,
    int SecondBonusOneIn,
    int BaseShareBasisPoints,
    bool OffersAnte,
    bool OffersBuy)
{
    public int CellCount => Reels * Rows;

    public int ReturnFor(string mode)
    {
        if (OffersAnte && string.Equals(mode, SlotsRules.AnteMode, StringComparison.Ordinal))
        {
            return AnteReturnBasisPoints;
        }

        if (OffersBuy && string.Equals(mode, SlotsRules.BuyMode, StringComparison.Ordinal))
        {
            return BuyReturnBasisPoints;
        }

        return ReturnBasisPoints;
    }
}

internal static class SlotsMachines
{
    public static readonly SlotsMachineInfo Bird = new(SlotsRules.BirdId, SlotsLayout.Lines, SlotsRules.ReelCount,
        SlotsRules.RowCount, 4, 5_000, 9_638, 0, 0, 2_930, 0, 183, 0, 0, 6_280, false, false);

    public static readonly SlotsMachineInfo Cascade = new(SlotsRules.CascadeId, SlotsLayout.Cluster,
        CrystalCascadeRules.Reels, CrystalCascadeRules.Rows, 5, 10_000, 9_622, 9_664, 9_611, 2_560, 710, 449, 222, 0,
        6_380, true, true);

    public static readonly SlotsMachineInfo Moogle = new(SlotsRules.MoogleId, SlotsLayout.Hold, SlotsRules.ReelCount,
        SlotsRules.RowCount, 3, 5_000, 9_676, 0, 0, 2_820, 0, 121, 0, 441, 4_790, false, false);

    public static readonly SlotsMachineInfo[] All = { Bird, Cascade, Moogle };

    public static SlotsMachineInfo For(string machineId)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Id, machineId, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return Bird;
    }
}
