namespace Aetherphone.Core.Character;

internal readonly record struct ActivitySnapshot(
    string JobName,
    int Level,
    bool MaxLevel,
    long CurrentExp,
    long NeededExp,
    int JobsAtMax,
    int JobsTotal,
    long TomestoneAmount,
    long TomestoneCap,
    string TomestoneName,
    int MountsOwned,
    int MountsTotal,
    int MinionsOwned,
    int MinionsTotal,
    long Gil,
    int RetainerCount,
    int RetainerVenturesReady,
    int RetainerVenturesActive)
{
    public float JobFraction => MaxLevel || NeededExp <= 0 ? 1f : Math.Clamp((float)((double)CurrentExp / NeededExp), 0f, 1f);

    public float TomestoneFraction => TomestoneCap <= 0 ? 0f : Math.Clamp((float)((double)TomestoneAmount / TomestoneCap), 0f, 1f);

    public int CollectionOwned => MountsOwned + MinionsOwned;

    public int CollectionTotal => MountsTotal + MinionsTotal;

    public float CollectionFraction => CollectionTotal <= 0 ? 0f : Math.Clamp((float)((double)CollectionOwned / CollectionTotal), 0f, 1f);
}
