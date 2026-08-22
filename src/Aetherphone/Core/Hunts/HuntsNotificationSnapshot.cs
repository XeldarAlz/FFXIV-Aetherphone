namespace Aetherphone.Core.Hunts;

internal sealed class HuntsNotificationSnapshot
{
    public bool RankF { get; set; }
    public bool RankB { get; set; }
    public bool RankA { get; set; }
    public bool RankS { get; set; }
    public bool RankSS { get; set; } = true;
    public List<bool> ExpansionActive { get; set; } = new();
    public Dictionary<string, bool> WorldEnabled { get; set; } = new();

    public Dictionary<string, HuntMobOverrideSnapshot> MobOverrides { get; set; } = new();
}

internal sealed class HuntMobOverrideSnapshot
{
    public HuntMobNotificationMode Mode { get; set; }
    public string? WorldId { get; set; }
}
