using Newtonsoft.Json;

namespace Aetherphone.Core.Market;

[Serializable]
internal sealed class MarketAlert
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public uint IconId { get; set; }
    public MarketScopeKind ScopeKind { get; set; }
    public string ScopeName { get; set; } = string.Empty;
    public bool HqOnly { get; set; }
    public long Threshold { get; set; }
    public bool Below { get; set; } = true;
    public bool Enabled { get; set; } = true;
    [JsonIgnore] public bool Triggered { get; set; }
    [JsonIgnore] public bool Acknowledged { get; set; }
    [JsonIgnore] public long LastSeenPrice { get; set; }
}
