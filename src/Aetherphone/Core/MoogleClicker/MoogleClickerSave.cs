namespace Aetherphone.Core.MoogleClicker;

[Serializable]
internal sealed class MoogleClickerSave
{
    public double Kupo { get; set; }
    public double LedgerKupo { get; set; }
    public double LifetimeKupo { get; set; }
    public double TapKupo { get; set; }
    public long Taps { get; set; }
    public int[] Owned { get; set; } = new int[KupoBuildings.Count];
    public long Upgrades { get; set; }
    public double Stamps { get; set; }
    public int LedgerPages { get; set; }
    public int MinionsCaught { get; set; }
    public KupoReward Buff { get; set; }
    public long BuffEndsUnixMilliseconds { get; set; }
    public long LastTickUnixMilliseconds { get; set; }
    public double AwaySeconds { get; set; }
    public double AwayKupo { get; set; }
    public bool AwayCapped { get; set; }
}
