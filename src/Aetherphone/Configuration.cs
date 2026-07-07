using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Clock;
using Aetherphone.Apps.Notes;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Games;
using Aetherphone.Core.Home;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
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
    public bool OpenMinimizedOnStartup { get; set; }
    public bool WelcomeShown { get; set; }
    public bool TutorialsEnabled { get; set; } = true;
    public Dictionary<string, int> OnboardingCompleted { get; set; } = new();
    public bool LockPosition { get; set; }
    public bool DoNotDisturb { get; set; }
    public Dictionary<string, AppNotificationSetting> NotificationSettings { get; set; } = new();
    public bool NotifyDailyReset { get; set; }
    public bool NotifyWeeklyReset { get; set; }
    public bool NotifyGrandCompanyReset { get; set; }
    public bool NotifyRetainerVentures { get; set; }
    public bool NotifyDailiesReset { get; set; }
    public List<DailyCheckRecord> DailyChecks { get; set; } = new();
    public bool ScrollWhileIdle { get; set; } = true;
    public bool ShowLodestonePortraits { get; set; } = true;
    public float TextZoom { get; set; } = 1.15f;
    public float ScreenBrightness { get; set; } = 1f;
    public float PhoneScale { get; set; } = 1.25f;
    public string Language { get; set; } = string.Empty;
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;
    public string AccentName { get; set; } = "Violet";
    public string LightWallpaperId { get; set; } = "DuskLight";
    public string DarkWallpaperId { get; set; } = "DuskDark";
    public List<CustomWallpaper> CustomWallpapers { get; set; } = new();
    public uint RingtoneId { get; set; } = 7;
    public string RingtoneSound { get; set; } = SoundTokens.DefaultGame;
    public string NotificationSound { get; set; } = SoundTokens.DefaultGame;
    public float RingtoneVolume { get; set; } = 0.8f;
    public float NotificationVolume { get; set; } = 0.8f;
    public bool SoundSettingsMigrated { get; set; }
    public const string DefaultAethernetBaseUrl = "https://ffxiv-aethernet-production.up.railway.app";
    public string AethernetBaseUrl { get; set; } = DefaultAethernetBaseUrl;
    public string AethernetToken { get; set; } = string.Empty;
    public string AnalyticsInstallId { get; set; } = string.Empty;
    public bool AnalyticsEnabled { get; set; } = true;
    public bool AnalyticsConsentPrompted { get; set; }
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
    public int HomeGridRows { get; set; } = 6;
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
    public long DevChatLastSeenUnix { get; set; }
    public bool TimeZoneManual { get; set; }
    public int ManualUtcOffsetMinutes { get; set; }
    public bool RegionManual { get; set; }
    public string ManualRegion { get; set; } = string.Empty;
    public long LastFeedbackSentUnix { get; set; }
    public List<CalendarCustomEvent> CalendarCustomEvents { get; set; } = new();
    public List<PhoneNote> Notes { get; set; } = new();
    public List<ReminderItem> Reminders { get; set; } = new();
    public List<WorldClockEntry> WorldClocks { get; set; } = new();
    public List<AlarmEntry> Alarms { get; set; } = new();
    public DateTime? TimerEndsAtUtc { get; set; }
    public int TimerDurationSeconds { get; set; }
    public bool TimerNotified { get; set; }
    public string LastSeenChangelogVersion { get; set; } = string.Empty;
    public bool ChangelogSeenInitialized { get; set; }

    public bool HasUnseenChangelog => LastSeenChangelogVersion != ChangelogData.LatestVersion;

    public void MarkChangelogSeen()
    {
        if (LastSeenChangelogVersion == ChangelogData.LatestVersion)
        {
            return;
        }

        LastSeenChangelogVersion = ChangelogData.LatestVersion;
        Save();
    }

    public void MigrateChangelogSeen()
    {
        if (ChangelogSeenInitialized)
        {
            return;
        }

        LastSeenChangelogVersion = ChangelogData.LatestVersion;
        ChangelogSeenInitialized = true;
        Save();
    }

    public bool IsAppNotificationEnabled(string appId) =>
        !NotificationSettings.TryGetValue(appId, out var setting) || setting.Enabled;

    public string? AppSoundOverride(string appId) =>
        NotificationSettings.TryGetValue(appId, out var setting) && !string.IsNullOrEmpty(setting.Sound)
            ? setting.Sound
            : null;

    public string ResolveNotificationToken(string appId) => AppSoundOverride(appId) ?? NotificationSound;

    public void MigrateSoundSettings()
    {
        if (SoundSettingsMigrated)
        {
            return;
        }

        RingtoneSound = SoundTokens.Game(RingtoneId);
        NotificationSound = SoundTokens.Game(RingtoneId);
        foreach (var pair in NotificationSettings)
        {
            var setting = pair.Value;
            if (setting.SoundId.HasValue && string.IsNullOrEmpty(setting.Sound))
            {
                setting.Sound = SoundTokens.Game(setting.SoundId.Value);
            }
        }

        SoundSettingsMigrated = true;
        Save();
    }

    public AppNotificationSetting NotificationSettingFor(string appId)
    {
        if (!NotificationSettings.TryGetValue(appId, out var setting))
        {
            setting = new AppNotificationSetting();
            NotificationSettings[appId] = setting;
        }

        return setting;
    }

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
