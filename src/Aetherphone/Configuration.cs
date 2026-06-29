using Aetherphone.Core.Dailies;
using Aetherphone.Core.Games;
using Aetherphone.Core.Home;
using Aetherphone.Core.Market;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.Wallpapers;
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

    public bool NotifyDailyReset { get; set; }

    public bool NotifyWeeklyReset { get; set; }

    public bool NotifyGrandCompanyReset { get; set; }

    public bool NotifyRetainerVentures { get; set; }

    public bool NotifyDailiesReset { get; set; }

    public List<DailyCheckRecord> DailyChecks { get; set; } = new();

    public bool ScrollWhileIdle { get; set; } = true;

    public bool ShowLodestonePortraits { get; set; } = true;

    public float TextZoom { get; set; } = 1.0f;

    public string Language { get; set; } = string.Empty;

    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;

    public string AccentName { get; set; } = "Violet";

    public string LightWallpaperId { get; set; } = "DuskLight";

    public string DarkWallpaperId { get; set; } = "DuskDark";

    public List<CustomWallpaper> CustomWallpapers { get; set; } = new();

    public uint RingtoneId { get; set; } = 7;

    public string AethernetBaseUrl { get; set; } = "https://ffxiv-aethernet-production.up.railway.app";

    public string AethernetToken { get; set; } = string.Empty;

    public bool ChirperEnabled { get; set; }

    public MarketScopeKind MarketScope { get; set; } = MarketScopeKind.DataCenter;

    public bool MarketHqOnly { get; set; }

    public List<uint> MarketFavorites { get; set; } = new();

    public List<uint> MarketRecents { get; set; } = new();

    public List<MarketAlert> MarketAlerts { get; set; } = new();

    public List<SongRecord> SongRecents { get; set; } = new();

    public List<GameStatRecord> GameStats { get; set; } = new();

    public HomeLayout? Home { get; set; }

    public VenueTimeFilter VenueTimeFilter { get; set; } = VenueTimeFilter.LiveNow;

    public int VenueSourceFilter { get; set; }

    public bool VenueAllDataCenters { get; set; }

    public bool VenueNotifyNewEvents { get; set; } = true;

    public List<string> VenueFavorites { get; set; } = new();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
