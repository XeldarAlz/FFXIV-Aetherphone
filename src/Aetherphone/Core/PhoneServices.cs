using Aetherphone.Core.Activity;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Announcements;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Health;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Market;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Net;
using Aetherphone.Core.News;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
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
    public required ITextureProvider Textures { get; init; }
    public required WeatherService Weather { get; init; }
    public required WeatherControl WeatherControl { get; init; }
    public required NotificationService Notifications { get; init; }
    public required SocialNotificationService SocialNotifications { get; init; }
    public required SoundService Sound { get; init; }
    public required MessageStore Messages { get; init; }
    public required ChatBridge ChatBridge { get; init; }
    public required LinkpearlLauncher LinkpearlLauncher { get; init; }
    public required VelvetLauncher VelvetLauncher { get; init; }
    public required DmLauncher DmLauncher { get; init; }
    public required GramDmLauncher GramDmLauncher { get; init; }
    public required SocialLauncher SocialLauncher { get; init; }
    public required LinkshellMuteStore LinkshellMutes { get; init; }
    public required LinkpearlNotificationGate LinkpearlNotificationGate { get; init; }
    public required LinkshellStore Linkshells { get; init; }
    public required LinkshellBridge LinkshellBridge { get; init; }
    public required HttpService Http { get; init; }
    public required MediaCache Media { get; init; }
    public required RemoteImageCache RemoteImages { get; init; }
    public required LodestoneService Lodestone { get; init; }
    public required LookupService Lookup { get; init; }
    public required AethernetSession AethernetSession { get; init; }
    public required CharacterSessionManager CharacterSwitcher { get; init; }
    public required AethernetApi Aethernet { get; init; }
    public required KeyVault KeyVault { get; init; }
    public required PeerKeyDirectory PeerKeys { get; init; }
    public required ConversationKeyStore ConversationKeys { get; init; }
    public required MarketItemIndex MarketIndex { get; init; }
    public required MarketboardService Market { get; init; }
    public required MarketLauncher MarketLauncher { get; init; }
    public required MarketAlertService MarketAlerts { get; init; }
    public required NewsService News { get; init; }
    public required RadioService Radio { get; init; }
    public required RadioPlayer RadioPlayer { get; init; }
    public required SongSearchService SongSearch { get; init; }
    public required SongPlayer SongPlayer { get; init; }
    public required SongHistory SongHistory { get; init; }
    public required PlaylistStore Playlists { get; init; }
    public required PlaybackHub Playback { get; init; }
    public required GameStatsStore GameStats { get; init; }
    public required VenuesService Venues { get; init; }
    public required MusterStore Musters { get; init; }
    public required MusterLauncher MusterLauncher { get; init; }
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
    public required PhoneVisibility Visibility { get; init; }
    public required RealtimeSignalBus RealtimeSignals { get; init; }
    public required LoadingScreen Loading { get; init; }
    public required ConfirmService Confirm { get; init; }
    public required ReportService Report { get; init; }
    public required ConductGateService Conduct { get; init; }
    public required WallpaperLibrary Wallpapers { get; init; }
    public required WallpaperImageCache WallpaperImages { get; init; }

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
        var gameData = new GameData(dataManager, objectTable);
        var maps = new MapData(dataManager, clientState);
        var weather = new WeatherService(dataManager, clientState);
        var weatherControl = new WeatherControl(weather, framework, clientState, condition,
            installer.Gate("skywatcher"));
        var soundBundledDirectory = new DirectoryInfo(
            Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Sounds"));
        var soundUserDirectory = new DirectoryInfo(Path.Combine(configDirectory.FullName, "Sounds"));
        var soundLibrary = new SoundLibrary(soundBundledDirectory, soundUserDirectory);
        var sound = new SoundService(configuration, soundLibrary, new SoundEffectPlayer(), framework);
        var notifications = new NotificationService(sound, configuration, installer, framework);
        var characterWatch = new CharacterWatch(framework);
        var messageArchive = new MessageArchive(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Messages")));
        var messages = new MessageStore(messageArchive, configuration, characterWatch);
        var linkpearlNotificationGate = new LinkpearlNotificationGate(configuration);
        var linkpearlGate = installer.Gate("messages");
        var chatBridge = new ChatBridge(messages, notifications, linkpearlNotificationGate, chatGui, gameData,
            linkpearlGate);
        var linkpearlLauncher = new LinkpearlLauncher();
        var velvetLauncher = new VelvetLauncher();
        var dmLauncher = new DmLauncher();
        var gramDmLauncher = new GramDmLauncher();
        var socialLauncher = new SocialLauncher();
        var linkshellMutes = new LinkshellMuteStore(configuration, characterWatch);
        var linkshells = new LinkshellStore(linkshellMutes, characterWatch);
        var linkshellBridge = new LinkshellBridge(linkshells, linkshellMutes, notifications, linkpearlNotificationGate,
            chatGui, gameData, linkpearlGate);
        var cacheRoot = new DirectoryInfo(Path.Combine(configDirectory.FullName, "cache"));
        cacheRoot.Create();
        var mediaRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "media"));
        var http = new HttpService();
        var disk = new DiskCache(mediaRoot, 64L * 1024 * 1024);
        var media = new MediaCache(textures, disk);
        var imageRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "images"));
        var imageDisk = new DiskCache(imageRoot, 128L * 1024 * 1024);
        var remoteImages = new RemoteImageCache(http, imageDisk);
        var lodestone = new LodestoneService(configuration, http, media, cacheRoot);
        var lookup = new LookupService(lodestone);
        var aethernetSession = new AethernetSession(configuration, framework);
        var aethernet = new AethernetApi(http, aethernetSession);
        var keyVault = new KeyVault(configuration, aethernetSession, aethernet.Keys);
        var peerKeys = new PeerKeyDirectory(configuration, aethernet.Keys);
        var conversationKeys = new ConversationKeyStore(aethernet.Keys, keyVault);
        var marketIndex = new MarketItemIndex(dataManager);
        var market = new MarketboardService(http);
        var marketLauncher = new MarketLauncher();
        var marketAlerts = new MarketAlertService(market, notifications, configuration, installer.Gate("market"));
        var news = new NewsService(http);
        var radio = new RadioService(http);
        var radioPlayer = new RadioPlayer();
        var youtube = new YoutubeClient();
        var songSearch = new SongSearchService(youtube);
        var audioRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "audio"));
        var audioCache = new DiskCache(audioRoot, 256L * 1024 * 1024);
        var songPlayer = new SongPlayer(youtube, audioCache);
        var songHistory = new SongHistory(configuration);
        var playlists = new PlaylistStore(configuration);
        var playback = new PlaybackHub(radioPlayer, songPlayer, configuration);
        var gameStats = new GameStatsStore(configuration);
        var venues = new VenuesService(http, notifications, configuration, gameData);
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
        var realtimeSignals = new RealtimeSignalBus();
        var visibility = new PhoneVisibility();
        var confirm = new ConfirmService();
        var calls = new CallHub(configuration, aethernetSession, notifications, sound, playback, realtimeSignals,
            confirm, installer.Gate("message"));
        var characterSwitcher = new CharacterSessionManager(framework, aethernetSession, aethernet.Account,
            gameData, configuration, confirm);
        var socialNotifications = new SocialNotificationService(aethernetSession, aethernet.Account, notifications, configuration, framework, visibility, realtimeSignals, confirm, installer);
        var musters = new MusterStore(aethernetSession, aethernet.Musters, notifications, configuration,
            visibility, realtimeSignals, installer.Gate(MusterStore.AppId));
        var yellowPages = new YellowPagesStore(aethernetSession, aethernet.Ads, aethernet.Media, configuration,
            visibility, realtimeSignals, installer.Gate(YellowPagesStore.AppId));
        var adInquiries = new AdInquiryStore(aethernetSession, aethernet.Ads, keyVault, conversationKeys,
            visibility, realtimeSignals, installer.Gate(YellowPagesStore.AppId));
        return new PhoneServices
        {
            Installer = installer,
            Configuration = configuration,
            Themes = themes,
            GameData = gameData,
            CharacterWatch = characterWatch,
            Maps = maps,
            Textures = textures,
            Weather = weather,
            WeatherControl = weatherControl,
            Notifications = notifications,
            SocialNotifications = socialNotifications,
            Sound = sound,
            Messages = messages,
            ChatBridge = chatBridge,
            LinkpearlLauncher = linkpearlLauncher,
            VelvetLauncher = velvetLauncher,
            DmLauncher = dmLauncher,
            GramDmLauncher = gramDmLauncher,
            SocialLauncher = socialLauncher,
            LinkshellMutes = linkshellMutes,
            LinkpearlNotificationGate = linkpearlNotificationGate,
            Linkshells = linkshells,
            LinkshellBridge = linkshellBridge,
            Http = http,
            Media = media,
            RemoteImages = remoteImages,
            Lodestone = lodestone,
            Lookup = lookup,
            AethernetSession = aethernetSession,
            CharacterSwitcher = characterSwitcher,
            Aethernet = aethernet,
            KeyVault = keyVault,
            PeerKeys = peerKeys,
            ConversationKeys = conversationKeys,
            MarketIndex = marketIndex,
            Market = market,
            MarketLauncher = marketLauncher,
            MarketAlerts = marketAlerts,
            News = news,
            Radio = radio,
            RadioPlayer = radioPlayer,
            SongSearch = songSearch,
            SongPlayer = songPlayer,
            SongHistory = songHistory,
            Playlists = playlists,
            Playback = playback,
            GameStats = gameStats,
            Venues = venues,
            Musters = musters,
            MusterLauncher = new MusterLauncher(),
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
            Visibility = visibility,
            RealtimeSignals = realtimeSignals,
            Loading = new LoadingScreen(configuration),
            Confirm = confirm,
            Report = new ReportService(),
            Conduct = new ConductGateService(configuration),
            Wallpapers = wallpapers,
            WallpaperImages = new WallpaperImageCache(),
        };
    }

    public void Dispose()
    {
        CharacterSwitcher.Dispose();
        CharacterWatch.Dispose();
        WeatherControl.Dispose();
        SocialNotifications.Dispose();
        KeyVault.Dispose();
        Calls.Dispose();
        Collections.Dispose();
        InventoryCapture.Dispose();
        RingNotifier.Dispose();
        Health.Dispose();
        Activity.Dispose();
        Venues.Dispose();
        Musters.Dispose();
        YellowPages.Dispose();
        AdInquiries.Dispose();
        SongPlayer.Dispose();
        SongSearch.Dispose();
        RadioPlayer.Dispose();
        Radio.Dispose();
        LinkshellBridge.Dispose();
        ChatBridge.Dispose();
        Lookup.Dispose();
        Lodestone.Dispose();
        MarketAlerts.Dispose();
        Market.Dispose();
        News.Dispose();
        Notifications.Dispose();
        Sound.Dispose();
        Media.Dispose();
        RemoteImages.Dispose();
        Http.Dispose();
        Wallpapers.Dispose();
        WallpaperImages.Dispose();
    }
}
