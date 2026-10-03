namespace Aetherphone.Core.Fishing;

[Serializable]
internal sealed class FishingVoyageReminder
{
    public long BoardingUnix { get; set; }
    public int Route { get; set; }
    public bool Notified { get; set; }
}
