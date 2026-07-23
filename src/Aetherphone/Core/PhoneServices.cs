using Aetherphone.Core.Activity;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Market;
using Aetherphone.Core.Media;
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
using Dalamud.Plugin.Services;
using YoutubeExplode;

namespace Aetherphone.Core;

internal sealed class PhoneServices : IDisposable
{
    public required Configuration Configuration { get; init; }
    public required ThemeProvider Themes { get; init; }
    public required GameData GameData { get; init; }
    public required CharacterWatch CharacterWatch { get; init; }
    public required MapData Maps { get; init; }
    public required ITextureProvider Textures { get; init; }
    public required WeatherService Weather { get; init; }
    public required NotificationService Notifications { get; init; }
    public required SocialNotificationService SocialNotifications { get; init; }
    public required SoundService Sound { get; init; }
    public required MessageStore Messages { get; init; }
    public required ChatBridge ChatBridge { get; init; }
    public required LinkpearlLauncher LinkpearlLauncher { get; init; }
    public required VelvetLauncher VelvetLauncher { get; init; }
    public required DmLauncher DmLauncher { get; init; }
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
    public required CollectionsCatalogService Collections { get; init; }
    public required InventoryCaptureService InventoryCapture { get; init; }
    public required ActivityTracker Activity { get; init; }
    public required ActivityRingNotifier RingNotifier { get; init; }
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
        ITextureProvider textures, DirectoryInfo configDirectory, IUnlockState unlockState)
    {
        var builtInWallpaperDirectory = new DirectoryInfo(
            Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Wallpapers"));
        var customWallpaperDirectory = new DirectoryInfo(Path.Combine(configDirectory.FullName, "Wallpapers"));
        var wallpapers = new WallpaperLibrary(textures, builtInWallpaperDirectory, customWallpaperDirectory,
            configuration);
        var themes = new ThemeProvider(configuration, wallpapers);
        var gameData = new GameData(dataManager, objectTable);
        var maps = new MapData(dataManager, clientState);
        var weather = new WeatherService(dataManager, clientState);
        var soundBundledDirectory = new DirectoryInfo(
            Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Sounds"));
        var soundUserDirectory = new DirectoryInfo(Path.Combine(configDirectory.FullName, "Sounds"));
        var soundLibrary = new SoundLibrary(soundBundledDirectory, soundUserDirectory);
        var sound = new SoundService(configuration, soundLibrary, new SoundEffectPlayer(), framework);
        var notifications = new NotificationService(sound, configuration, framework);
        var characterWatch = new CharacterWatch(framework);
        var messageArchive = new MessageArchive(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Messages")));
        var messages = new MessageStore(messageArchive, configuration, characterWatch);
        var linkpearlNotificationGate = new LinkpearlNotificationGate(configuration);
        var chatBridge = new ChatBridge(messages, notifications, linkpearlNotificationGate, chatGui, gameData);
        var linkpearlLauncher = new LinkpearlLauncher();
        var velvetLauncher = new VelvetLauncher();
        var dmLauncher = new DmLauncher();
        var socialLauncher = new SocialLauncher();
        var linkshellMutes = new LinkshellMuteStore(configuration, characterWatch);
        var linkshells = new LinkshellStore(linkshellMutes, characterWatch);
        var linkshellBridge = new LinkshellBridge(linkshells, linkshellMutes, notifications, linkpearlNotificationGate,
            chatGui, gameData);
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
        var marketAlerts = new MarketAlertService(market, notifications, configuration);
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
        var collections = new CollectionsCatalogService(http, collectionsDisk, dataManager, unlockState);
        var inventoryRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "inventory"));
        var inventoryStore = new InventoryStore(inventoryRoot);
        var inventoryCapture = new InventoryCaptureService(framework, inventoryStore);
        var activity = new ActivityTracker(framework, clientState, dutyState, gameData, configDirectory);
        var ringNotifier = new ActivityRingNotifier(framework, activity, configuration, notifications);
        var realtimeSignals = new RealtimeSignalBus();
        var visibility = new PhoneVisibility();
        var confirm = new ConfirmService();
        var calls = new CallHub(configuration, aethernetSession, notifications, sound, playback, realtimeSignals, confirm);
        var characterSwitcher = new CharacterSessionManager(framework, aethernetSession, aethernet.Account,
            gameData, configuration, confirm);
        var socialNotifications = new SocialNotificationService(aethernetSession, aethernet.Account, notifications, configuration, framework, visibility, realtimeSignals, confirm);
        return new PhoneServices
        {
            Configuration = configuration,
            Themes = themes,
            GameData = gameData,
            CharacterWatch = characterWatch,
            Maps = maps,
            Textures = textures,
            Weather = weather,
            Notifications = notifications,
            SocialNotifications = socialNotifications,
            Sound = sound,
            Messages = messages,
            ChatBridge = chatBridge,
            LinkpearlLauncher = linkpearlLauncher,
            VelvetLauncher = velvetLauncher,
            DmLauncher = dmLauncher,
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
            Collections = collections,
            InventoryCapture = inventoryCapture,
            Activity = activity,
            RingNotifier = ringNotifier,
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
        SocialNotifications.Dispose();
        KeyVault.Dispose();
        Calls.Dispose();
        Collections.Dispose();
        InventoryCapture.Dispose();
        RingNotifier.Dispose();
        Activity.Dispose();
        Venues.Dispose();
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
