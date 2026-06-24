using Aetherphone.Core.Market;
using Aetherphone.Core.Songs;
using Dalamud.Configuration;

namespace Aetherphone;

[Serializable]
internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool OpenOnStartup { get; set; }

    public bool WelcomeShown { get; set; }

    public bool LockPosition { get; set; }

    public bool DoNotDisturb { get; set; }

    public bool ShowLodestonePortraits { get; set; } = true;

    public float TextZoom { get; set; } = 1.0f;

    public string AccentName { get; set; } = "Violet";

    public string WallpaperName { get; set; } = "Aurora";

    public uint RingtoneId { get; set; } = 7;

    public string AethernetBaseUrl { get; set; } = "http://127.0.0.1:5240";

    public string AethernetToken { get; set; } = string.Empty;

    public bool ChirperEnabled { get; set; }

    public MarketScopeKind MarketScope { get; set; } = MarketScopeKind.DataCenter;

    public bool MarketHqOnly { get; set; }

    public List<uint> MarketFavorites { get; set; } = new();

    public List<uint> MarketRecents { get; set; } = new();

    public List<MarketAlert> MarketAlerts { get; set; } = new();

    public List<SongRecord> SongRecents { get; set; } = new();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
