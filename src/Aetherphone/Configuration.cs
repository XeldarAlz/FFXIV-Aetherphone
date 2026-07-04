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

    public bool TutorialsEnabled { get; set; } = true;

    public Dictionary<string, int> OnboardingCompleted { get; set; } = new();

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

    public float TextZoom { get; set; } = 1.15f;

    public float PhoneScale { get; set; } = 1.25f;

    public string Language { get; set; } = string.Empty;

    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;

    public string AccentName { get; set; } = "Violet";

    public string LightWallpaperId { get; set; } = "ShadowDark";

    public string DarkWallpaperId { get; set; } = "ShadowDark";

    public List<CustomWallpaper> CustomWallpapers { get; set; } = new();

    public uint RingtoneId { get; set; } = 7;

    public const string DefaultAethernetBaseUrl = "https://ffxiv-aethernet-production.up.railway.app";

    public string AethernetBaseUrl { get; set; } = DefaultAethernetBaseUrl;

    public string AethernetToken { get; set; } = string.Empty;

    public string AnalyticsInstallId { get; set; } = string.Empty;

    public bool AnalyticsEnabled { get; set; } = true;

    public bool CallsEnabled { get; set; }

    public string CallInputDevice { get; set; } = string.Empty;

    public string CallOutputDevice { get; set; } = string.Empty;

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

    public List<uint> MapFavorites { get; set; } = new();

    public const int VelvetGateVersion = 1;

    public bool VelvetAcknowledgedGate { get; set; }

    public bool VelvetOnboarded { get; set; }

    public int VelvetAcknowledgedGateVersion { get; set; }

    public bool VelvetBlurByDefault { get; set; } = true;

    public void NormalizeAethernetBaseUrl()
    {
        if (!ShouldResetBaseUrl(AethernetBaseUrl))
        {
            return;
        }

        AethernetBaseUrl = DefaultAethernetBaseUrl;
        Save();
    }

    private static bool ShouldResetBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return true;
        }

        return parsed.IsLoopback;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
