namespace Aetherphone.Core.Hunts;

internal sealed class HuntsFilterSnapshot
{
    public bool RankF { get; set; }
    public bool RankB { get; set; }
    public bool RankA { get; set; }
    public bool RankS { get; set; }
    public bool RankSS { get; set; } = true;
    public bool StatusClosed { get; set; }
    public bool StatusOpen { get; set; }
    public bool StatusCapped { get; set; }
    public bool StatusUnmet { get; set; }
    public List<bool> ExpansionActive { get; set; } = new();
    public List<string> Worlds { get; set; } = new();
}
