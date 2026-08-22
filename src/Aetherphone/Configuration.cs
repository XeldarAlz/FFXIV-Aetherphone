using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Message;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.ControlCenter;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Games;
using Aetherphone.Core.Home;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Social;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.Video;
using Aetherphone.Core.Wallpapers;
using Dalamud.Configuration;

namespace Aetherphone;

[Serializable]
internal sealed class ScreenPositionPreset
{
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float Scale { get; set; } = 1.0f;
}

[Serializable]
internal sealed class VideoQueueRecord
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public double? DurationSeconds { get; set; }
    public string? ThumbnailUrl { get; set; }
}

[Serializable]
internal sealed class Configuration : IPluginConfiguration, IHomeConfiguration, IControlConfiguration
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
    public bool QuietWhileBusy { get; set; } = true;
    public bool Vibration { get; set; } = true;
    public bool ShowNotificationBanner { get; set; } = true;
    public bool ImportScreenshots { get; set; } = true;
    public bool? UseNativeFileDialog { get; set; }
    public bool ChirperShowMediaPosts { get; set; } = true;
    public bool ChirperShowPhotoPosts { get; set; } = true;
    public bool ChirperShowGifPosts { get; set; } = true;
    public bool ChirperShowCommentMedia { get; set; } = true;
    public bool AethergramShowGifPosts { get; set; } = true;
    public bool AethergramShowCommentMedia { get; set; } = true;
    public int ChirperFeedRegionMask { get; set; }
    public int AethergramFeedRegionMask { get; set; }
    public bool ShowSensitiveContent { get; set; }
    public Dictionary<string, AppNotificationSetting> NotificationSettings { get; set; } = new();
    public bool NotifyDailyReset { get; set; }
    public bool NotifyWeeklyReset { get; set; }
    public bool NotifyGrandCompanyReset { get; set; }
    public bool NotifyRetainerVentures { get; set; }
    public bool ShowWalletBadge { get; set; } = true;
    public bool ShowDailiesBadge { get; set; } = true;
    public bool ShowActivityBadge { get; set; } = true;
    public List<DailyCheckRecord> DailyChecks { get; set; } = new();
    public float ActivityGoalLevels { get; set; } = 1f;
    public int ActivityGoalDuties { get; set; } = 3;
    public long ActivityGoalGil { get; set; } = 50000;
    public bool ScrollWhileIdle { get; set; } = true;
    public bool ShowLodestonePortraits { get; set; } = true;
    public int LodestoneIdIndexVersion { get; set; }
    public float TextZoom { get; set; } = 1.0f;
    public string FontGlyphCache { get; set; } = string.Empty;
    public string IconGlyphCache { get; set; } = string.Empty;
    public float ScreenBrightness { get; set; } = 1f;
    public float PhoneScale { get; set; } = PhoneSizeCatalog.DefaultWidth / PhoneSizeCatalog.DesignWidth;
    public float PhoneWidth { get; set; }
    public bool CameraLandscape { get; set; }
    public bool CameraGrid { get; set; }
    public bool CameraFlash { get; set; } = true;
    public int PhotosSegment { get; set; }
    public string Language { get; set; } = string.Empty;
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;
    public string AccentName { get; set; } = "Violet";
    public string AccentCustomHex { get; set; } = string.Empty;
    public string PhoneCaseName { get; set; } = "Titanium";
    public string JobsAccentName { get; set; } = "Blue";
    public List<JobsCustomColor> JobsCustomColors { get; set; } = new();
    public Dictionary<ulong, List<JobsCategory>> JobsCategoriesByCharacter { get; set; } = new();
    public string LightWallpaperId { get; set; } = "DuskLight";
    public string DarkWallpaperId { get; set; } = "DuskDark";
    public List<CustomWallpaper> CustomWallpapers { get; set; } = new();
    public string RingtoneSound { get; set; } = SoundLibrary.BundledRingtoneToken;
    public string NotificationSound { get; set; } = SoundLibrary.BundledNotificationToken;
    public float RingtoneVolume { get; set; } = 0.8f;
    public float NotificationVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.6f;
    public int MusicRepeat { get; set; }
    public bool SoundSettingsMigrated { get; set; }
    public float VideoVolume { get; set; } = 0.6f;
    public int VideoMaxQualityHeight { get; set; } = 720;
    public bool VideoHideNameplates { get; set; } = true;
    public bool VideoShareWatchPresence { get; set; } = true;
    public bool VideoHardwareDecoding { get; set; }
    public bool VideoAllowInsecureDirectUrls { get; set; }
    public bool VideoStreamApprovalRequired { get; set; }
    public bool VideoStreamDiscoverable { get; set; } = true;
    public bool VideoScreenVisible { get; set; } = true;
    public List<ScreenPositionPreset> ScreenPresets { get; set; } = new();
    public List<VideoQueueRecord> VideoQueue { get; set; } = new();
    public bool GameSoundsCleared { get; set; }
    #if DEBUG
    public const string DefaultAethernetBaseUrl = "https://aethernet-dev-production.up.railway.app";
    #else
    public const string DefaultAethernetBaseUrl = "https://api.aetherphone.net";
    #endif
    private const string LegacyAethernetHost = "ffxiv-aethernet-production.up.railway.app";
    public string AethernetBaseUrl { get; set; } = DefaultAethernetBaseUrl;
    public string AethernetToken { get; set; } = string.Empty;
    public string EncryptionKeyCache { get; set; } = string.Empty;
    public string EncryptionKeyCacheUserId { get; set; } = string.Empty;
    public string HuntsSessionCache { get; set; } = string.Empty;
    public bool HuntsAuthenticated { get; set; }
    public bool HuntsAppOpened { get; set; }
    public HuntsFilterSnapshot? HuntsFilterSettings { get; set; }
    public HuntsNotificationSnapshot? HuntsNotificationSettings { get; set; }
    public bool EncryptionRecoveryNudgeDismissed { get; set; }
    public Dictionary<string, int> KnownPeerKeyVersions { get; set; } = new();
    public Dictionary<ulong, CharacterSession> CharacterSessions { get; set; } = new();
    public bool FollowCharacterAccount { get; set; } = true;
    public ulong PinnedAccountContentId { get; set; }
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
    public int DailyChallengeStreak { get; set; }
    public int DailyChallengeLastDay { get; set; }
    public string PendingCoinGameSession { get; set; } = string.Empty;
    public Dictionary<ulong, string> PendingCasinoSittings { get; set; } = new();
    public Dictionary<ulong, long> CasinoSittingSeenAtUnix { get; set; } = new();
    public Dictionary<ulong, PendingCasinoRound> PendingCasinoRounds { get; set; } = new();
    public HomeLayout? Home { get; set; }
    public Dictionary<string, bool> AppFlags { get; set; } = new();
    public int HomeGridRows { get; set; } = 6;
    public bool ShowAppNames { get; set; } = true;
    public ControlLayout? ControlPanel { get; set; }
    public bool ControlPanelRepacked { get; set; }
    public VenueTimeFilter VenueTimeFilter { get; set; } = VenueTimeFilter.LiveNow;
    public int VenueSourceFilter { get; set; }
    public bool VenueAllDataCenters { get; set; }
    public bool VenueNotifyNewEvents { get; set; } = true;
    public List<string> VenueFavorites { get; set; } = new();
    public int MusterCategoryFilter { get; set; }
    public int MusterScope { get; set; }
    public int MusterDataCenterId { get; set; }
    public int YellowPagesCategoryFilter { get; set; }
    public int YellowPagesScope { get; set; }
    public bool YellowPagesAfterDark { get; set; }
    public List<uint> MapFavorites { get; set; } = new();
    public uint HousingWorldId { get; set; }
    public uint HousingDistrictId { get; set; } = 339u;
    public int HousingWard { get; set; } = HousingDefaults.DefaultWard;
    public bool HousingFollowCurrentWorld { get; set; }
    public bool HousingAutoRefresh { get; set; } = true;
    public int HousingRefreshMinutes { get; set; } = HousingDefaults.RefreshMinutes;
    public bool HousingRefreshFloorApplied { get; set; }
    public int HousingLiveMinutes { get; set; } = 15;
    public int HousingRecentMinutes { get; set; } = 60;
    public bool HousingNotifyEntry { get; set; } = true;
    public bool HousingNotifyResults { get; set; } = true;
    public int HousingReminderMinutes { get; set; } = HousingDefaults.ReminderMinutes;
    public bool HousingFilterSmall { get; set; } = true;
    public bool HousingFilterMedium { get; set; } = true;
    public bool HousingFilterLarge { get; set; } = true;
    public bool HousingShowAllPlots { get; set; }
    public int HousingListSort { get; set; }
    public bool HousingMapHintDismissed { get; set; }
    public List<HousingWatchRecord> HousingWatched { get; set; } = new();
    public List<HousingReminderRecord> HousingReminders { get; set; } = new();
    public List<RadioStationRecord> RadioFavorites { get; set; } = new();
    public List<string> CustomAlbumOrder { get; set; } = new();
    public Dictionary<string, List<string>> CustomAlbumPhotos { get; set; } = new();
    public const int VelvetGateVersion = 1;
    public const int VelvetOnboardVersion = 2;
    public bool VelvetAcknowledgedGate { get; set; }
    public bool VelvetOnboarded { get; set; }
    public int VelvetOnboardedVersion { get; set; }
    public int VelvetAcknowledgedGateVersion { get; set; }

    public bool IsVelvetOnboarded() => VelvetOnboarded && VelvetOnboardedVersion >= VelvetOnboardVersion;
    public bool VelvetBlurByDefault { get; set; } = true;
    public VelvetMutePreferences VelvetMutes { get; set; } = new();
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
    public Dictionary<string, long> PendingNotificationAcks { get; set; } = new();
    public Dictionary<string, int> ConductAcknowledged { get; set; } = new();
    public List<string> MutedLinkshells { get; set; } = new();
    public Dictionary<ulong, List<string>> MutedLinkshellsByCharacter { get; set; } = new();
    public bool LinkshellMutesPerCharacterMigrated { get; set; }
    public List<ChatTab> LinkpearlTabs { get; set; } = new();
    public int LinkpearlHistory { get; set; } = (int)HistoryPolicy.Days30;
    public Dictionary<string, int> LinkpearlHistoryByChannel { get; set; } = new();
    public List<ulong> LinkpearlMigratedCharacters { get; set; } = new();
    public Dictionary<string, long> LinkpearlSeen { get; set; } = new();
    public long DevChatLastSeenUnix { get; set; }
    public long AnnouncementsSeenUnix { get; set; }
    public long AnnouncementsNotifiedUnix { get; set; }
    public bool AnnouncementsInitialized { get; set; }
    public bool? Use24HourClock { get; set; }
    public bool TimeZoneManual { get; set; }
    public int ManualUtcOffsetMinutes { get; set; }
    public bool RegionManual { get; set; }
    public string ManualRegion { get; set; } = string.Empty;
    public long LastFeedbackSentUnix { get; set; }
    public List<CalendarCustomEvent> CalendarCustomEvents { get; set; } = new();
    public List<PhoneNote> Notes { get; set; } = new();
    public List<ShortcutEntry> Shortcuts { get; set; } = new();
    public List<ReminderItem> Reminders { get; set; } = new();
    public List<WorldClockEntry> WorldClocks { get; set; } = new();
    public List<AlarmEntry> Alarms { get; set; } = new();
    public DateTime? TimerEndsAtUtc { get; set; }
    public int TimerDurationSeconds { get; set; }
    public bool TimerNotified { get; set; }
    public string LastSeenChangelogVersion { get; set; } = string.Empty;
    public bool ChangelogSeenInitialized { get; set; }
    
    public bool MarketContextMenu { get; set; } = true;

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

    public void MigrateChirperMediaFilters()
    {
        if (ChirperShowMediaPosts)
        {
            return;
        }

        ChirperShowPhotoPosts = false;
        ChirperShowGifPosts = false;
        ChirperShowCommentMedia = false;
        ChirperShowMediaPosts = true;
        Save();
    }

    public void MigratePhoneWidth()
    {
        if (PhoneWidth > 0f)
        {
            return;
        }

        PhoneWidth = PhoneSizeCatalog.WidthForScale(PhoneScale);
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

    public void MigrateHousingRefreshFloor()
    {
        if (HousingRefreshFloorApplied)
        {
            return;
        }

        HousingRefreshFloorApplied = true;
        if (HousingRefreshMinutes < HousingDefaults.RefreshMinutes)
        {
            HousingRefreshMinutes = HousingDefaults.RefreshMinutes;
        }

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

    public bool ShouldShowNotificationBanner(string appId) =>
        !NotificationSettings.TryGetValue(appId, out var setting) || setting.ShowNotificationBanner;

    public string? AppSoundOverride(string appId) =>
        NotificationSettings.TryGetValue(appId, out var setting) && !string.IsNullOrEmpty(setting.Sound)
            ? setting.Sound
            : null;

    public string ResolveNotificationToken(string appId) => AppSoundOverride(appId) ?? NotificationSound;

    public void MigrateSoundSettings()
    {
        if (GameSoundsCleared)
        {
            return;
        }

        if (SoundTokens.TryUpgradeLegacy(RingtoneSound, out var ringtone))
        {
            RingtoneSound = ringtone.Length == 0 ? SoundLibrary.BundledRingtoneToken : ringtone;
        }

        if (SoundTokens.TryUpgradeLegacy(NotificationSound, out var notification))
        {
            NotificationSound = notification.Length == 0 ? SoundLibrary.BundledNotificationToken : notification;
        }

        foreach (var pair in NotificationSettings)
        {
            var setting = pair.Value;
            if (SoundTokens.TryUpgradeLegacy(setting.Sound, out var appSound))
            {
                setting.Sound = appSound.Length == 0 ? null : appSound;
            }
        }

        GameSoundsCleared = true;
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

#if DEBUG
        return false;
#else
        return parsed.IsLoopback;
#endif
    }

    public void Save()
    {
        if (Plugin.Framework.IsInFrameworkUpdateThread)
        {
            SaveNow();
            return;
        }

        _ = Plugin.Framework.RunOnFrameworkThread(SaveNow);
    }

    public void SaveNow()
    {
        try
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "Configuration save failed; settings changed this session may be lost");
        }
    }
}
