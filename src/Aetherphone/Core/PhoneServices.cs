using Aetherphone.Core.Activity;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Announcements;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Games;
using Aetherphone.Core.Health;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Market;
using Aetherphone.Core.Media;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Net;
using Aetherphone.Core.News;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Core.Video;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Core.YellowPages;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using YoutubeExplode;

namespace Aetherphone.Core;

internal sealed class PhoneServices : IDisposable
{
    public required Home.AppInstaller Installer { get; init; }
    public required Configuration Configuration { get; init; }
    public required ThemeProvider Themes { get; init; }
    public required GameData GameData { get; init; }
    public required CharacterWatch CharacterWatch { get; init; }
    public required MapData Maps { get; init; }
    public required HousingService Housing { get; init; }
    public required HousingReminderService HousingReminders { get; init; }
    public required ITextureProvider Textures { get; init; }
    public required Windows.Components.ArtworkCache Artwork { get; init; }
    public required WeatherService Weather { get; init; }
    public required WeatherControl WeatherControl { get; init; }
    public required GameUiVisibility GameUiVisibility { get; init; }
    public required NotificationService Notifications { get; init; }
    public required SocialNotificationService SocialNotifications { get; init; }
    public required ModerationNoticeService ModerationNotices { get; init; }

    public required AccountStateService AccountState { get; init; }
    public required ModerationNoticePresenter ModerationPresenter { get; init; }
    public required ModerationNoticeArchive ModerationArchive { get; init; }
    public required SafetyLauncher SafetyLauncher { get; init; }
    public required SoundService Sound { get; init; }
    public required UiSoundService UiSound { get; init; }
    public required FrameworkTicker UiSoundTicker { get; init; }
    public required LinkpearlLauncher LinkpearlLauncher { get; init; }
    public required VelvetLauncher VelvetLauncher { get; init; }
    public required DmLauncher DmLauncher { get; init; }
    public required GramDmLauncher GramDmLauncher { get; init; }
    public required SocialLauncher SocialLauncher { get; init; }
    public required LinkpearlNotificationGate LinkpearlNotificationGate { get; init; }
    public required GameChat.ChatLog ChatLog { get; init; }
    public required GameChat.ChatSend ChatSend { get; init; }
    public required GameChat.ChatCapture ChatCapture { get; init; }
    public required GameChat.ChatArchive ChatArchive { get; init; }
    public required GameChat.TabStore ChatTabs { get; init; }
    public required GameChat.TellPreferences TellPreferences { get; init; }
    public required GameChat.ChatInbox ChatInbox { get; init; }
    public required GameChat.ChatNotifier ChatNotifier { get; init; }
    public required HttpService Http { get; init; }
    public required MediaCache Media { get; init; }
    public required RemoteImageCache RemoteImages { get; init; }

    public required Social.BadgeCatalogStore BadgeCatalog { get; init; }

    public required Social.FrameCatalogStore FrameCatalog { get; init; }

    public required Social.LoadoutStore Loadout { get; init; }

    public required Coins.CoinStore Coins { get; init; }

    public required Coins.CoinCatalogStore CoinCatalog { get; init; }

    public required Coins.CoinGameSessionTracker CoinSessions { get; init; }

    public required Coins.CoinEarnNotifier CoinEarnNotifier { get; init; }

    public required Casino.CasinoStore Casino { get; init; }
    public required Casino.CasinoPlayStore CasinoPlay { get; init; }
    public required Casino.CasinoHistoryStore CasinoHistory { get; init; }
    public required Casino.CasinoRoomsStore CasinoRooms { get; init; }
    public required Casino.CasinoTablesStore CasinoTables { get; init; }
    public required Casino.CasinoSpinStore CasinoSpin { get; init; }
    public required Casino.CasinoTurnNotifier CasinoTurns { get; init; }
    public required Casino.CasinoLauncher CasinoLauncher { get; init; }
    public required Games.GameRoomsStore GameRooms { get; init; }
    public required Video.AetherStreamLauncher AetherStreamLauncher { get; init; }
    public required PluginCatalog PluginCatalog { get; init; }
    public required ShortcutStore Shortcuts { get; init; }
    public required ShortcutRunner ShortcutRunner { get; init; }
    public required LodestoneService Lodestone { get; init; }
    public required LookupService Lookup { get; init; }
    public required AethernetSession AethernetSession { get; init; }
    public required AppAvailability Availability { get; init; }
    public required CharacterSessionManager CharacterSwitcher { get; init; }
    public required AethernetApi Aethernet { get; init; }
    public required KeyVault KeyVault { get; init; }

    public required DeviceLinkWatcher DeviceLinks { get; init; }

    public required EncryptionGuide EncryptionGuide { get; init; }

    public required DecryptedHistoryStore ChatHistory { get; init; }

    public required EncryptionHelpService EncryptionHelp { get; init; }
    public required PeerKeyDirectory PeerKeys { get; init; }
    public required ConversationKeyStore ConversationKeys { get; init; }
    public required EncryptionSetupLauncher EncryptionSetup { get; init; }
    public required MarketItemIndex MarketIndex { get; init; }
    public required MarketboardService Market { get; init; }
    public required TranslationService Translation { get; init; }
    public required MarketLauncher MarketLauncher { get; init; }
    public required MarketAlertService MarketAlerts { get; init; }
    public required NewsService News { get; init; }
    public required RadioService Radio { get; init; }
    public required RadioPlayer RadioPlayer { get; init; }
    public required SongSearchService SongSearch { get; init; }
    public required VideoUrlResolver VideoMetadata { get; init; }
    public required SongPlayer SongPlayer { get; init; }
    public required SongLinkResolver SongResolver { get; init; }
    public required SongHistory SongHistory { get; init; }
    public required PlaylistStore Playlists { get; init; }
    public required PlaybackHub Playback { get; init; }
    public required GameStatsStore GameStats { get; init; }
    public required VenuesService Venues { get; init; }
    public required StratsManifestStore StratsManifest { get; init; }
    public required StratsGuideStore StratsGuides { get; init; }
    public required MusterStore Musters { get; init; }
    public required MusterLauncher MusterLauncher { get; init; }

    public required RadioLauncher RadioLauncher { get; init; }
    public required YellowPagesStore YellowPages { get; init; }

    public required AdInquiryStore AdInquiries { get; init; }
    public required YellowPagesLauncher YellowPagesLauncher { get; init; }
    public required AnnouncementsLauncher AnnouncementsLauncher { get; init; }
    public required CollectionsCatalogService Collections { get; init; }
    public required InventoryCaptureService InventoryCapture { get; init; }
    public required ActivityTracker Activity { get; init; }
    public required ActivityRingNotifier RingNotifier { get; init; }
    public required HealthTracker Health { get; init; }
    public required CallHub Calls { get; init; }
    public required StreamSignalRouter StreamSignals { get; init; }
    public required PhoneVisibility Visibility { get; init; }
    public required RealtimeSignalBus RealtimeSignals { get; init; }
    public required LoadingScreen Loading { get; init; }
    public required ConfirmService Confirm { get; init; }
    public required ReportService Report { get; init; }
    public required ShareService Share { get; init; }
    public required ConductGateService Conduct { get; init; }
    public required WallpaperLibrary Wallpapers { get; init; }
    public required WallpaperImageCache WallpaperImages { get; init; }
    public required Hunts.HuntsService Hunts { get; init; }
    public required Hunts.HuntMobCatalog HuntMobCatalog { get; init; }
    public required Hunts.HuntZoneCatalog HuntZoneCatalog { get; init; }
    public required Hunts.HuntZoneMapTextures HuntZoneMapTextures { get; init; }
    public required Hunts.HuntMobRewardCatalog HuntMobRewardCatalog { get; init; }
    public required Hunts.HuntsLauncher HuntsLauncher { get; init; }
    public required Shell.MinimizedLayoutService MinimizedLayout { get; init; }

    public static PhoneServices Build(Configuration configuration, IChatGui chatGui, IDataManager dataManager,
        IObjectTable objectTable, IClientState clientState, IFramework framework, IDutyState dutyState,
        ITextureProvider textures, DirectoryInfo configDirectory, IUnlockState unlockState, ICondition condition)
    {
        var installer = new Home.AppInstaller();
        var builtInWallpaperDirectory = new DirectoryInfo(
            Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Wallpapers"));
        var customWallpaperDirectory = new DirectoryInfo(Path.Combine(configDirectory.FullName, "Wallpapers"));
        var wallpapers = new WallpaperLibrary(textures, builtInWallpaperDirectory, customWallpaperDirectory,
            configuration);
        var themes = new ThemeProvider(configuration, wallpapers);
        var gameData = new GameData(dataManager, objectTable, framework);
        var maps = new MapData(dataManager, clientState);
        var weather = new WeatherService(dataManager, clientState);
        var weatherControl = new WeatherControl(weather, framework, clientState, condition,
            installer.Gate("skywatcher"));
        var gameUiVisibility = new GameUiVisibility(framework, Plugin.PluginInterface);
        var soundBundledRoot = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
            "Sounds");
        var soundUserRoot = Path.Combine(configDirectory.FullName, "Sounds");
        var ringtoneLibrary = new SoundLibrary(new DirectoryInfo(Path.Combine(soundBundledRoot, "Ringtones")),
            new DirectoryInfo(Path.Combine(soundUserRoot, "Ringtones")));
        var notificationLibrary = new SoundLibrary(new DirectoryInfo(Path.Combine(soundBundledRoot, "Notifications")),
            new DirectoryInfo(Path.Combine(soundUserRoot, "Notifications")));
        var sound = new SoundService(configuration, ringtoneLibrary, notificationLibrary, new SoundEffectPlayer());
        var uiSound = new UiSoundService(configuration, new UiSoundPlayer(new DirectoryInfo(soundBundledRoot)));
        var uiSoundTicker = new FrameworkTicker(framework, 1000, uiSound.Maintain);
        UiFeedback.Bind(uiSound);
        var notifications = new NotificationService(sound, configuration, installer, framework);
        var characterWatch = new CharacterWatch(framework);
        var messageArchive = new MessageArchive(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Messages")));
        var linkpearlNotificationGate = new LinkpearlNotificationGate(configuration);
        var linkpearlGate = installer.Gate("messages");
        var linkpearlLauncher = new LinkpearlLauncher();
        var velvetLauncher = new VelvetLauncher();
        var dmLauncher = new DmLauncher();
        var gramDmLauncher = new GramDmLauncher();
        var socialLauncher = new SocialLauncher();
        var chatLog = new GameChat.ChatLog();
        var chatSend = new GameChat.ChatSend();
        var chatCapture = new GameChat.ChatCapture(chatLog, chatSend, chatGui, gameData, linkpearlGate);
        var chatArchive = new GameChat.ChatArchive(
            new DirectoryInfo(Path.Combine(configDirectory.FullName, "GameChat")), configuration, chatLog,
            messageArchive, characterWatch);
        var chatTabs = new GameChat.TabStore(configuration, characterWatch);
        var tellPreferences = new GameChat.TellPreferences(configuration);
        var chatInbox = new GameChat.ChatInbox(chatLog, chatTabs, tellPreferences, configuration);
        var chatNotifier = new GameChat.ChatNotifier(chatLog, chatTabs, chatInbox, tellPreferences,
            linkpearlNotificationGate, notifications, linkpearlGate);
        var cacheRoot = new DirectoryInfo(Path.Combine(configDirectory.FullName, "cache"));
        cacheRoot.Create();
        var mediaRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "media"));
        var aethernetSession = new AethernetSession(configuration, framework);
        var http = new HttpService(new AethernetClientIdentity(aethernetSession.BaseUrl, aethernetSession.ReportSourceStatus));
        var disk = new DiskCache(mediaRoot, 64L * 1024 * 1024);
        var media = new MediaCache(textures, disk);
        var imageRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "images"));
        var imageDisk = new DiskCache(imageRoot, 128L * 1024 * 1024);
        var remoteImages = new RemoteImageCache(http, imageDisk);
        var pluginCatalog = new PluginCatalog(remoteImages, http, imageDisk);
        var wallpaperImages = new WallpaperImageCache();
        var shortcutIconDirectory = new DirectoryInfo(Path.Combine(configDirectory.FullName, "ShortcutIcons"));
        var shortcutIcons = new ShortcutIconLibrary(shortcutIconDirectory, configuration, wallpaperImages);
        var lodestone = new LodestoneService(configuration, gameData, http, media, cacheRoot);
        var lookup = new LookupService(lodestone);
        var availability = new AppAvailability(http, aethernetSession, configuration, gameData);
        var aethernet = new AethernetApi(http, aethernetSession);
        var keyVault = new KeyVault(configuration, aethernetSession, aethernet.Keys);
        var chatHistory = new DecryptedHistoryStore(configDirectory, aethernetSession);
        var encryptionHelp = new EncryptionHelpService();
        var badgeCatalog = new Social.BadgeCatalogStore(aethernetSession, aethernet.Account);
        var frameCatalog = new Social.FrameCatalogStore(aethernetSession, aethernet.Account);
        var loadoutStore = new Social.LoadoutStore(aethernetSession, aethernet.Account);
        Social.Frames.Use(frameCatalog);
        Windows.Components.UserName.Configure(badgeCatalog, remoteImages);
        Moderation.ModerationNoticeText.Configure(badgeCatalog, frameCatalog);
        var coinApi = new AethernetApi(http, aethernetSession, "coin");
        var coins = new Coins.CoinStore(aethernetSession, coinApi.Coins);
        var coinCatalog = new Coins.CoinCatalogStore(aethernetSession, coinApi.Coins);
        var coinSessions = new Coins.CoinGameSessionTracker(configuration, aethernetSession, coinApi.Coins);
        var coinEarnNotifier = new Coins.CoinEarnNotifier(coins, notifications);
        var casinoApi = new AethernetApi(http, aethernetSession, "casino");
        var casino = new Casino.CasinoStore(configuration, aethernetSession, casinoApi.Casino, coins);
        var casinoPlay = new Casino.CasinoPlayStore(configuration, aethernetSession, casinoApi.Casino, casino);
        var casinoHistory = new Casino.CasinoHistoryStore(aethernetSession, casinoApi.Casino);
        var casinoSpin = new Casino.CasinoSpinStore(aethernetSession, casinoApi.Casino, coins);
        var realtimeSignals = new RealtimeSignalBus();
        var peerKeys = new PeerKeyDirectory(configuration, aethernet.Keys);
        var conversationKeys = new ConversationKeyStore(aethernet.Keys, keyVault, realtimeSignals);
        var translation = new TranslationService(aethernetSession, aethernet.Translation, configuration);
        var marketIndex = new MarketItemIndex(dataManager);
        var market = new MarketboardService(http);
        var marketLauncher = new MarketLauncher();
        var marketAlerts = new MarketAlertService(market, notifications, configuration, installer.Gate("market"));
        var news = new NewsService(http, aethernetSession);
        var radio = new RadioService(http);
        var radioPlayer = new RadioPlayer();
        var youtube = new YoutubeClient();
        var audioRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "audio"));
        var audioCache = new DiskCache(audioRoot, 256L * 1024 * 1024);
        var songResolver = new SongLinkResolver(Path.Combine(audioRoot.FullName, "resolver"));
        var songSearch = new SongSearchService(youtube, songResolver);
        var videoMetadata = new VideoUrlResolver(youtube);
        var songPlayer = new SongPlayer(youtube, audioCache, songResolver);
        var songHistory = new SongHistory(configuration);
        var playlists = new PlaylistStore(configuration);
        var playback = new PlaybackHub(radioPlayer, songPlayer, configuration);
        var gameStats = new GameStatsStore(configuration);
        var venues = new VenuesService(http, notifications, configuration, gameData);
        var stratsRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "strats"));
        var stratsDisk = new DiskCache(stratsRoot, 24L * 1024 * 1024);
        var stratsManifest = new StratsManifestStore(http, stratsDisk);
        var stratsGuides = new StratsGuideStore(http, stratsDisk);
        var collectionsRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "collections"));
        var collectionsDisk = new DiskCache(collectionsRoot, 32L * 1024 * 1024);
        var collections = new CollectionsCatalogService(http, collectionsDisk, dataManager, unlockState, framework);
        var inventoryRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "inventory"));
        var inventoryStore = new InventoryStore(inventoryRoot);
        var inventoryCapture = new InventoryCaptureService(framework, inventoryStore, installer.Gate("inventory"));
        var characterGate = installer.Gate("character");
        var activity = new ActivityTracker(framework, clientState, dutyState, gameData, configDirectory, characterGate);
        var ringNotifier = new ActivityRingNotifier(framework, activity, configuration, notifications, characterGate);
        var health = new HealthTracker(framework, characterWatch, notifications, configDirectory);
        var visibility = new PhoneVisibility();
        var housingCacheRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "housing"));
        var housingGate = installer.Gate(HousingService.AppId);
        var housingGameMaps = new HousingGameMaps(dataManager, textures);
        var housing = new HousingService(http, configuration, gameData, framework, housingGameMaps, visibility,
            housingCacheRoot, housingGate);
        var housingReminders = new HousingReminderService(configuration, framework, notifications, housing.Watch,
            housingGate);
        var confirm = new ConfirmService();
        var deviceLinks = new DeviceLinkWatcher(keyVault, aethernetSession, confirm);
        var encryptionGuide = new EncryptionGuide(keyVault, aethernetSession, notifications);
        Windows.UrlActions.Configure(confirm);
        var calls = new CallHub(configuration, aethernetSession, notifications, sound, playback, realtimeSignals,
            confirm, installer.Gate("message"));
        var streamSignals = new StreamSignalRouter(calls.Router);
        var characterSwitcher = new CharacterSessionManager(framework, aethernetSession, aethernet.Account,
            gameData, configuration, confirm);
        var socialNotifications = new SocialNotificationService(aethernetSession, aethernet.Account, notifications, configuration, framework, visibility, realtimeSignals, installer);
        var moderationNotices = new ModerationNoticeService(aethernetSession, aethernet.Account, framework,
            visibility, realtimeSignals);
        var accountState = new AccountStateService(aethernetSession, aethernet.Account, framework, visibility);
        var moderationPresenter = new ModerationNoticePresenter(moderationNotices, confirm, notifications,
            accountState, framework);
        var moderationArchive = new ModerationNoticeArchive(aethernetSession, aethernet.Account);
        var safetyLauncher = new SafetyLauncher();
        var casinoRooms = new Casino.CasinoRoomsStore(aethernetSession, casinoApi.Casino, casino, visibility,
            realtimeSignals);
        var casinoTables = new Casino.CasinoTablesStore(aethernetSession, casinoApi.Casino, casino, visibility,
            realtimeSignals);
        var casinoTurns = new Casino.CasinoTurnNotifier(aethernetSession, casinoRooms, notifications,
            Apps.AppAccents.For("casino"));
        var gameRooms = new Games.GameRoomsStore(aethernetSession, aethernet.Games, visibility,
            realtimeSignals);
        var musters = new MusterStore(aethernetSession, aethernet.Musters, notifications, configuration,
            visibility, realtimeSignals, installer.Gate(MusterStore.AppId));
        var yellowPages = new YellowPagesStore(aethernetSession, aethernet.Ads, aethernet.Media, configuration,
            visibility, realtimeSignals, installer.Gate(YellowPagesStore.AppId));
        var adInquiries = new AdInquiryStore(aethernetSession, aethernet.Ads, aethernet.Safety, keyVault, conversationKeys,
            chatHistory, visibility, realtimeSignals, installer.Gate(YellowPagesStore.AppId));
        var huntMobsFile = new FileInfo(Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Hunts", "HuntMob.json"));
        var huntMobCatalog = new HuntMobCatalog(huntMobsFile);
        var huntZonesFile = new FileInfo(Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Hunts", "HuntPOI.json"));
        var huntZoneCatalog = new HuntZoneCatalog(huntZonesFile);
        var huntZoneMapTextures = new HuntZoneMapTextures(dataManager, textures);
        var huntMobDescriptionsFile = new FileInfo(Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Hunts", "HuntMobDescriptions.json"));
        var huntMobTipsFile = new FileInfo(Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Hunts", "HuntMobTips.json"));
        HuntMobLore.Initialize(new HuntMobTextCatalog(huntMobDescriptionsFile), new HuntMobTextCatalog(huntMobTipsFile));
        var huntMobRewardsFile = new FileInfo(Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Hunts", "HuntMobRewards.json"));
        var huntMobRewardCatalog = new HuntMobRewardCatalog(huntMobRewardsFile);
        var huntsAuthTokens = new HuntsAuthTokenStore(http, configuration);
        var huntsClient = new HuntsClient(http, huntsAuthTokens);
        var hunts = new HuntsService(huntsClient, huntsAuthTokens, huntMobCatalog, gameData, characterWatch,
            notifications, configuration);


        return new PhoneServices
        {
            Installer = installer,
            Configuration = configuration,
            MinimizedLayout = new Shell.MinimizedLayoutService(configuration),
            Themes = themes,
            GameData = gameData,
            CharacterWatch = characterWatch,
            Maps = maps,
            Housing = housing,
            HousingReminders = housingReminders,
            Textures = textures,
            Artwork = new Windows.Components.ArtworkCache(textures),
            Weather = weather,
            WeatherControl = weatherControl,
            GameUiVisibility = gameUiVisibility,
            Notifications = notifications,
            SocialNotifications = socialNotifications,
            ModerationNotices = moderationNotices,
            AccountState = accountState,
            ModerationPresenter = moderationPresenter,
            ModerationArchive = moderationArchive,
            SafetyLauncher = safetyLauncher,
            Sound = sound,
            UiSound = uiSound,
            UiSoundTicker = uiSoundTicker,
            LinkpearlLauncher = linkpearlLauncher,
            VelvetLauncher = velvetLauncher,
            DmLauncher = dmLauncher,
            GramDmLauncher = gramDmLauncher,
            SocialLauncher = socialLauncher,
            LinkpearlNotificationGate = linkpearlNotificationGate,
            ChatLog = chatLog,
            ChatSend = chatSend,
            ChatCapture = chatCapture,
            ChatArchive = chatArchive,
            ChatTabs = chatTabs,
            TellPreferences = tellPreferences,
            ChatInbox = chatInbox,
            ChatNotifier = chatNotifier,
            Http = http,
            Media = media,
            RemoteImages = remoteImages,
            BadgeCatalog = badgeCatalog,
            FrameCatalog = frameCatalog,
            Loadout = loadoutStore,
            Coins = coins,
            CoinCatalog = coinCatalog,
            CoinSessions = coinSessions,
            CoinEarnNotifier = coinEarnNotifier,
            Casino = casino,
            CasinoPlay = casinoPlay,
            CasinoHistory = casinoHistory,
            CasinoRooms = casinoRooms,
            CasinoTables = casinoTables,
            CasinoSpin = casinoSpin,
            CasinoTurns = casinoTurns,
            CasinoLauncher = new Casino.CasinoLauncher(),
            GameRooms = gameRooms,
            AetherStreamLauncher = new Video.AetherStreamLauncher(),
            PluginCatalog = pluginCatalog,
            Shortcuts = new ShortcutStore(configuration, pluginCatalog, shortcutIcons),
            ShortcutRunner = new ShortcutRunner(clientState, condition),
            Lodestone = lodestone,
            Lookup = lookup,
            AethernetSession = aethernetSession,
            Availability = availability,
            CharacterSwitcher = characterSwitcher,
            Aethernet = aethernet,
            KeyVault = keyVault,
            DeviceLinks = deviceLinks,
            EncryptionGuide = encryptionGuide,
            ChatHistory = chatHistory,
            EncryptionHelp = encryptionHelp,
            PeerKeys = peerKeys,
            ConversationKeys = conversationKeys,
            EncryptionSetup = new EncryptionSetupLauncher(),
            MarketIndex = marketIndex,
            Market = market,
            Translation = translation,
            MarketLauncher = marketLauncher,
            MarketAlerts = marketAlerts,
            News = news,
            Radio = radio,
            RadioPlayer = radioPlayer,
            SongSearch = songSearch,
            VideoMetadata = videoMetadata,
            SongPlayer = songPlayer,
            SongResolver = songResolver,
            SongHistory = songHistory,
            Playlists = playlists,
            Playback = playback,
            GameStats = gameStats,
            Venues = venues,
            StratsManifest = stratsManifest,
            StratsGuides = stratsGuides,
            Musters = musters,
            MusterLauncher = new MusterLauncher(),
            RadioLauncher = new RadioLauncher(),
            YellowPages = yellowPages,
            AdInquiries = adInquiries,
            YellowPagesLauncher = new YellowPagesLauncher(),
            AnnouncementsLauncher = new AnnouncementsLauncher(),
            Collections = collections,
            InventoryCapture = inventoryCapture,
            Activity = activity,
            RingNotifier = ringNotifier,
            Health = health,
            Calls = calls,
            StreamSignals = streamSignals,
            Visibility = visibility,
            RealtimeSignals = realtimeSignals,
            Loading = new LoadingScreen(configuration),
            Confirm = confirm,
            Report = new ReportService(),
            Share = new ShareService(installer),
            Conduct = new ConductGateService(configuration),
            Wallpapers = wallpapers,
            WallpaperImages = wallpaperImages,
            Hunts = hunts,
            HuntMobCatalog = huntMobCatalog,
            HuntZoneCatalog = huntZoneCatalog,
            HuntZoneMapTextures = huntZoneMapTextures,
            HuntMobRewardCatalog = huntMobRewardCatalog,
            HuntsLauncher = new Hunts.HuntsLauncher(),
        };
    }

    public void Dispose()
    {
        CharacterSwitcher.Dispose();
        CharacterWatch.Dispose();
        WeatherControl.Dispose();
        GameUiVisibility.Dispose();
        SocialNotifications.Dispose();
        ModerationPresenter.Dispose();
        ModerationNotices.Dispose();
        AccountState.Dispose();
        ChatHistory.Dispose();
        DeviceLinks.Dispose();
        KeyVault.Dispose();
        StreamSignals.Dispose();
        Calls.Dispose();
        Collections.Dispose();
        InventoryCapture.Dispose();
        RingNotifier.Dispose();
        Health.Dispose();
        Activity.Dispose();
        HousingReminders.Dispose();
        Housing.Dispose();
        Venues.Dispose();
        Hunts.Dispose();
        StratsManifest.Dispose();
        StratsGuides.Dispose();
        Musters.Dispose();
        YellowPages.Dispose();
        AdInquiries.Dispose();
        SongPlayer.Dispose();
        SongSearch.Dispose();
        VideoMetadata.Dispose();
        RadioPlayer.Dispose();
        Radio.Dispose();
        ChatNotifier.Dispose();
        ChatCapture.Dispose();
        ChatInbox.Dispose();
        ChatArchive.Dispose();
        Lookup.Dispose();
        Lodestone.Dispose();
        MarketAlerts.Dispose();
        Market.Dispose();
        Translation.Dispose();
        News.Dispose();
        Notifications.Dispose();
        Sound.Dispose();
        UiFeedback.Unbind();
        UiSoundTicker.Dispose();
        UiSound.Dispose();
        Media.Dispose();
        Artwork.Dispose();
        ShortcutRunner.Dispose();
        RemoteImages.Dispose();
        Windows.Components.UserName.Reset();
        Moderation.ModerationNoticeText.Reset();
        GameRooms.Dispose();
        CasinoTurns.Dispose();
        CasinoTables.Dispose();
        CasinoRooms.Dispose();
        CasinoSpin.Dispose();
        CasinoHistory.Dispose();
        CasinoPlay.Dispose();
        Casino.Dispose();
        CoinEarnNotifier.Dispose();
        CoinSessions.Dispose();
        CoinCatalog.Dispose();
        Coins.Dispose();
        BadgeCatalog.Dispose();
        FrameCatalog.Dispose();
        Loadout.Dispose();
        Availability.Dispose();
        Http.Dispose();
        Wallpapers.Dispose();
        WallpaperImages.Dispose();
    }
}
