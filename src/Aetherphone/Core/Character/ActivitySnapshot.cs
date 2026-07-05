namespace Aetherphone.Core.Character;

internal readonly record struct ActivitySnapshot(
    string JobName,
    int Level,
    bool MaxLevel,
    long CurrentExp,
    long NeededExp,
    int JobsAtMax,
    int JobsTotal,
    int MountsOwned,
    int MountsTotal,
    int MinionsOwned,
    int MinionsTotal,
    int RetainerCount,
    int RetainerVenturesReady,
    int RetainerVenturesActive)
{
    public float JobFraction =>
        MaxLevel || NeededExp <= 0 ? 1f : Math.Clamp((float)((double)CurrentExp / NeededExp), 0f, 1f);

    public float MasteryFraction => JobsTotal <= 0 ? 0f : Math.Clamp((float)((double)JobsAtMax / JobsTotal), 0f, 1f);
    public int CollectionOwned => MountsOwned + MinionsOwned;
    public int CollectionTotal => MountsTotal + MinionsTotal;

    public float CollectionFraction =>
        CollectionTotal <= 0 ? 0f : Math.Clamp((float)((double)CollectionOwned / CollectionTotal), 0f, 1f);
}
