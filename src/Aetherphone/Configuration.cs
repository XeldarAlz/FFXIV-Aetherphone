using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Message;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.ControlCenter;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Games;
using Aetherphone.Core.Home;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.Wallpapers;
using Dalamud.Configuration;

namespace Aetherphone;

[Serializable]
internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool OpenOnStartup { get; set; } = true;
    public bool OpenMinimizedOnStartup { get; set; }
    public bool WelcomeShown { get; set; }
    public bool SetupCompleted { get; set; }
    public bool TutorialsEnabled { get; set; } = true;
    public Dictionary<string, int> OnboardingCompleted { get; set; } = new();
    public bool LockPosition { get; set; }
    public bool ShowInGpose { get; set; } = true;
    public Vector2? MaximizedPosition { get; set; }
    public Vector2? MinimizedPosition { get; set; }
    public bool DoNotDisturb { get; set; }
    public bool Vibration { get; set; } = true;
    public Dictionary<string, AppNotificationSetting> NotificationSettings { get; set; } = new();
    public bool NotifyDailyReset { get; set; }
    public bool NotifyWeeklyReset { get; set; }
    public bool NotifyGrandCompanyReset { get; set; }
    public bool NotifyRetainerVentures { get; set; }
    public bool ShowDailiesBadge { get; set; } = true;
    public List<DailyCheckRecord> DailyChecks { get; set; } = new();
    public float ActivityGoalLevels { get; set; } = 1f;
    public int ActivityGoalDuties { get; set; } = 3;
    public long ActivityGoalGil { get; set; } = 50000;
    public bool ScrollWhileIdle { get; set; } = true;
    public bool ShowLodestonePortraits { get; set; } = true;
    public float TextZoom { get; set; } = 1.15f;
    public List<string> FontGlyphLedger { get; set; } = new();
    public float ScreenBrightness { get; set; } = 1f;
    public float PhoneScale { get; set; } = 1.25f;
    public string Language { get; set; } = string.Empty;
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;
    public string AccentName { get; set; } = "Violet";
    public string JobsAccentName { get; set; } = "Blue";
    public List<JobsCustomColor> JobsCustomColors { get; set; } = new();
    public string LightWallpaperId { get; set; } = "DuskLight";
    public string DarkWallpaperId { get; set; } = "DuskDark";
    public List<CustomWallpaper> CustomWallpapers { get; set; } = new();
    public uint RingtoneId { get; set; } = 7;
    public string RingtoneSound { get; set; } = SoundTokens.DefaultGame;
    public string NotificationSound { get; set; } = SoundTokens.DefaultGame;
    public float RingtoneVolume { get; set; } = 0.8f;
    public float NotificationVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.6f;
    public int MusicRepeat { get; set; }
    public bool SoundSettingsMigrated { get; set; }
    public const string DefaultAethernetBaseUrl = "https://api.aetherphone.net";
    private const string LegacyAethernetHost = "ffxiv-aethernet-production.up.railway.app";
    public string AethernetBaseUrl { get; set; } = DefaultAethernetBaseUrl;
    public string AethernetToken { get; set; } = string.Empty;
    public string EncryptionKeyCache { get; set; } = string.Empty;
    public string EncryptionKeyCacheUserId { get; set; } = string.Empty;
    public Dictionary<string, int> KnownPeerKeyVersions { get; set; } = new();
    public Dictionary<ulong, CharacterSession> CharacterSessions { get; set; } = new();
    public string LegacyUnclaimedToken { get; set; } = string.Empty;
    public string LegacyUnclaimedEncryptionKey { get; set; } = string.Empty;
    public string LegacyUnclaimedEncryptionUserId { get; set; } = string.Empty;
    public bool CharacterSessionsMigrated { get; set; }
    public bool CallsEnabled { get; set; }
    public string CallInputDevice { get; set; } = string.Empty;
    public string CallOutputDevice { get; set; } = string.Empty;
    public List<CallLogEntry> CallLog { get; set; } = new();
    public long CallLogSeenUnix { get; set; }
    public MarketScopeKind MarketScope { get; set; } = MarketScopeKind.DataCenter;
    public bool MarketHqOnly { get; set; }
    public List<uint> MarketFavorites { get; set; } = new();
    public List<uint> MarketRecents { get; set; } = new();
    public List<MarketAlert> MarketAlerts { get; set; } = new();
    public List<SongRecord> SongRecents { get; set; } = new();
    public List<PlaylistRecord> Playlists { get; set; } = new();
    public List<GameStatRecord> GameStats { get; set; } = new();
    public HomeLayout? Home { get; set; }
    public int HomeGridRows { get; set; } = 6;
    public ControlLayout? ControlPanel { get; set; }
    public bool ControlPanelRepacked { get; set; }
    public VenueTimeFilter VenueTimeFilter { get; set; } = VenueTimeFilter.LiveNow;
    public int VenueSourceFilter { get; set; }
    public bool VenueAllDataCenters { get; set; }
    public bool VenueNotifyNewEvents { get; set; } = true;
    public List<string> VenueFavorites { get; set; } = new();
    public List<uint> MapFavorites { get; set; } = new();
    public List<RadioStationRecord> RadioFavorites { get; set; } = new();
    public const int VelvetGateVersion = 1;
    public const int VelvetOnboardVersion = 2;
    public bool VelvetAcknowledgedGate { get; set; }
    public bool VelvetOnboarded { get; set; }
    public int VelvetOnboardedVersion { get; set; }
    public int VelvetAcknowledgedGateVersion { get; set; }

    public bool IsVelvetOnboarded() => VelvetOnboarded && VelvetOnboardedVersion >= VelvetOnboardVersion;
    public bool VelvetBlurByDefault { get; set; } = true;
    public List<string> VelvetPinnedThreads { get; set; } = new();
    public List<string> MessagePinnedChats { get; set; } = new();
    public List<string> MessageArchivedChats { get; set; } = new();
    public List<string> MessageFavoriteContacts { get; set; } = new();
    public Dictionary<string, string> MessageContactNotes { get; set; } = new();
    public Dictionary<string, string> MessageDrafts { get; set; } = new();
    public List<StarredMessage> MessageStarredMessages { get; set; } = new();
    public bool ArchiveTellsToDisk { get; set; } = true;
    public bool LinkpearlNotificationsPaused { get; set; }
    public bool MessageMigrated { get; set; }
    public bool MessagesMergeMigrated { get; set; }
    public bool MessagesPerCharacterMigrated { get; set; }
    public Dictionary<string, long> SocialActivitySeenUnix { get; set; } = new();
    public long ModerationNoticeSeenUnix { get; set; }
    public Dictionary<string, int> ConductAcknowledged { get; set; } = new();
    public List<string> MutedLinkshells { get; set; } = new();
    public Dictionary<ulong, List<string>> MutedLinkshellsByCharacter { get; set; } = new();
    public bool LinkshellMutesPerCharacterMigrated { get; set; }
    public long DevChatLastSeenUnix { get; set; }
    public bool? Use24HourClock { get; set; }
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

    public void MigrateSetupCompleted()
    {
        if (SetupCompleted || !WelcomeShown)
        {
            return;
        }

        SetupCompleted = true;
        Save();
    }

    public void MigrateControlPanelRepack()
    {
        if (ControlPanelRepacked)
        {
            return;
        }

        ControlPanel = null;
        ControlPanelRepacked = true;
        Save();
    }

    public void MigrateCharacterSessions()
    {
        if (CharacterSessionsMigrated)
        {
            return;
        }

        CharacterSessionsMigrated = true;
        if (AethernetToken.Length > 0)
        {
            LegacyUnclaimedToken = AethernetToken;
            LegacyUnclaimedEncryptionKey = EncryptionKeyCache;
            LegacyUnclaimedEncryptionUserId = EncryptionKeyCacheUserId;
            AethernetToken = string.Empty;
            EncryptionKeyCache = string.Empty;
            EncryptionKeyCacheUserId = string.Empty;
        }

        Save();
    }

    public void MigrateMessage()
    {
        if (MessageMigrated)
        {
            return;
        }

        if (NotificationSettings.TryGetValue("dm", out var dmSetting) &&
            !NotificationSettings.ContainsKey("message"))
        {
            NotificationSettings["message"] = dmSetting;
        }

        if (Home is not null)
        {
            var placed = false;
            if (Home.Dock is { } dock)
            {
                MigrateMessageIds(dock, ref placed);
            }

            for (var pageIndex = 0; pageIndex < Home.Pages.Count; pageIndex++)
            {
                var items = Home.Pages[pageIndex].Items;
                for (var itemIndex = items.Count - 1; itemIndex >= 0; itemIndex--)
                {
                    var item = items[itemIndex];
                    MigrateMessageIds(item.AppIds, ref placed);
                    if (!IsLegacyMessageId(item.AppId))
                    {
                        continue;
                    }

                    if (placed)
                    {
                        items.RemoveAt(itemIndex);
                    }
                    else
                    {
                        item.AppId = "message";
                        placed = true;
                    }
                }
            }
        }

        MessageMigrated = true;
        Save();
    }

    public void MigrateMessagesMerge()
    {
        if (MessagesMergeMigrated)
        {
            return;
        }

        if (Home is not null)
        {
            var placed = HomeContains("messages");
            if (Home.Dock is { } dock)
            {
                MigrateMessagesMergeIds(dock, ref placed);
            }

            for (var pageIndex = 0; pageIndex < Home.Pages.Count; pageIndex++)
            {
                var items = Home.Pages[pageIndex].Items;
                for (var itemIndex = items.Count - 1; itemIndex >= 0; itemIndex--)
                {
                    var item = items[itemIndex];
                    MigrateMessagesMergeIds(item.AppIds, ref placed);
                    if (!IsLegacyMessagesId(item.AppId))
                    {
                        continue;
                    }

                    if (placed)
                    {
                        items.RemoveAt(itemIndex);
                    }
                    else
                    {
                        item.AppId = "messages";
                        placed = true;
                    }
                }
            }
        }

        MessagesMergeMigrated = true;
        Save();
    }

    private bool HomeContains(string appId)
    {
        if (Home is null)
        {
            return false;
        }

        if (Home.Dock is { } dock && dock.Contains(appId))
        {
            return true;
        }

        for (var pageIndex = 0; pageIndex < Home.Pages.Count; pageIndex++)
        {
            var items = Home.Pages[pageIndex].Items;
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                if (items[itemIndex].AppId == appId || items[itemIndex].AppIds.Contains(appId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsLegacyMessagesId(string appId) => appId is "contacts" or "findpeople";

    private static void MigrateMessagesMergeIds(List<string> ids, ref bool placed)
    {
        for (var index = ids.Count - 1; index >= 0; index--)
        {
            if (!IsLegacyMessagesId(ids[index]))
            {
                continue;
            }

            if (placed)
            {
                ids.RemoveAt(index);
            }
            else
            {
                ids[index] = "messages";
                placed = true;
            }
        }
    }

    private static bool IsLegacyMessageId(string appId) => appId is "dm" or "friends" or "phone" or "chocochat";

    private static void MigrateMessageIds(List<string> ids, ref bool placed)
    {
        for (var index = ids.Count - 1; index >= 0; index--)
        {
            if (!IsLegacyMessageId(ids[index]))
            {
                continue;
            }

            if (placed)
            {
                ids.RemoveAt(index);
            }
            else
            {
                ids[index] = "message";
                placed = true;
            }
        }
    }

    public bool HasAcknowledgedConduct(string appId, int version) =>
        ConductAcknowledged.TryGetValue(appId, out var seen) && seen >= version;

    public void AcknowledgeConduct(string appId, int version)
    {
        if (HasAcknowledgedConduct(appId, version))
        {
            return;
        }

        ConductAcknowledged[appId] = version;
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

        if (string.Equals(parsed.Host, LegacyAethernetHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return parsed.IsLoopback;
    }

    public void Save()
    {
        if (Plugin.Framework.IsInFrameworkUpdateThread)
        {
            Plugin.PluginInterface.SavePluginConfig(this);
            return;
        }

        _ = Plugin.Framework.RunOnFrameworkThread(() => Plugin.PluginInterface.SavePluginConfig(this));
    }
}
