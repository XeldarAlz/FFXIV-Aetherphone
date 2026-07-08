using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Character;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Market;
using Aetherphone.Core.Media;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Net;
using Aetherphone.Core.News;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Dalamud.Plugin.Services;
using YoutubeExplode;

namespace Aetherphone.Core;

internal sealed class PhoneServices : IDisposable
{
    public Configuration Configuration { get; }
    public ThemeProvider Themes { get; }
    public GameData GameData { get; }
    public MapData Maps { get; }
    public ITextureProvider Textures { get; }
    public WeatherService Weather { get; }
    public NotificationService Notifications { get; }
    public SocialNotificationService SocialNotifications { get; }
    public SoundService Sound { get; }
    public MessageStore Messages { get; }
    public ChatBridge ChatBridge { get; }
    public MessageLauncher MessageLauncher { get; }
    public VelvetLauncher VelvetLauncher { get; }
    public DmLauncher DmLauncher { get; }
    public SocialLauncher SocialLauncher { get; }
    public LinkshellMuteStore LinkshellMutes { get; }
    public LinkshellStore Linkshells { get; }
    public LinkshellBridge LinkshellBridge { get; }
    public HttpService Http { get; }
    public MediaCache Media { get; }
    public RemoteImageCache RemoteImages { get; }
    public LodestoneService Lodestone { get; }
    public CollectService Collect { get; }
    public LookupService Lookup { get; }
    public AethernetSession AethernetSession { get; }
    public AethernetClient AethernetClient { get; }
    public IAnalyticsService Analytics { get; }
    public MarketItemIndex MarketIndex { get; }
    public MarketboardService Market { get; }
    public MarketLauncher MarketLauncher { get; }
    public MarketAlertService MarketAlerts { get; }
    public NewsService News { get; }
    public RadioService Radio { get; }
    public RadioPlayer RadioPlayer { get; }
    public SongSearchService SongSearch { get; }
    public SongPlayer SongPlayer { get; }
    public SongHistory SongHistory { get; }
    public PlaybackHub Playback { get; }
    public GameStatsStore GameStats { get; }
    public VenuesService Venues { get; }
    public CollectionsCatalogService Collections { get; }
    public InventoryCaptureService InventoryCapture { get; }
    public CallHub Calls { get; }

    private PhoneServices(Configuration configuration, ThemeProvider themes, GameData gameData, MapData maps,
        ITextureProvider textures, WeatherService weather, NotificationService notifications, SoundService sound,
        MessageStore messages, ChatBridge chatBridge, MessageLauncher messageLauncher, VelvetLauncher velvetLauncher,
        DmLauncher dmLauncher, SocialLauncher socialLauncher, LinkshellMuteStore linkshellMutes,
        LinkshellStore linkshells, LinkshellBridge linkshellBridge, HttpService http,
        MediaCache media, RemoteImageCache remoteImages,
        LodestoneService lodestone, CollectService collect, LookupService lookup, AethernetSession aethernetSession,
        AethernetClient aethernetClient, IAnalyticsService analytics, MarketItemIndex marketIndex,
        MarketboardService market, MarketLauncher marketLauncher, MarketAlertService marketAlerts, NewsService news,
        RadioService radio, RadioPlayer radioPlayer, SongSearchService songSearch, SongPlayer songPlayer,
        SongHistory songHistory, PlaybackHub playback, GameStatsStore gameStats, VenuesService venues,
        CollectionsCatalogService collections, InventoryCaptureService inventoryCapture, CallHub calls,
        SocialNotificationService socialNotifications)
    {
        Configuration = configuration;
        Themes = themes;
        GameData = gameData;
        Maps = maps;
        Textures = textures;
        Weather = weather;
        Notifications = notifications;
        SocialNotifications = socialNotifications;
        Sound = sound;
        Messages = messages;
        ChatBridge = chatBridge;
        MessageLauncher = messageLauncher;
        VelvetLauncher = velvetLauncher;
        DmLauncher = dmLauncher;
        SocialLauncher = socialLauncher;
        LinkshellMutes = linkshellMutes;
        Linkshells = linkshells;
        LinkshellBridge = linkshellBridge;
        Http = http;
        Media = media;
        RemoteImages = remoteImages;
        Lodestone = lodestone;
        Collect = collect;
        Lookup = lookup;
        AethernetSession = aethernetSession;
        AethernetClient = aethernetClient;
        Analytics = analytics;
        MarketIndex = marketIndex;
        Market = market;
        MarketLauncher = marketLauncher;
        MarketAlerts = marketAlerts;
        News = news;
        Radio = radio;
        RadioPlayer = radioPlayer;
        SongSearch = songSearch;
        SongPlayer = songPlayer;
        SongHistory = songHistory;
        Playback = playback;
        GameStats = gameStats;
        Venues = venues;
        Collections = collections;
        InventoryCapture = inventoryCapture;
        Calls = calls;
    }

    public static PhoneServices Build(Configuration configuration, IChatGui chatGui, IDataManager dataManager,
        IObjectTable objectTable, IClientState clientState, IFramework framework, ITextureProvider textures,
        DirectoryInfo configDirectory)
    {
        var themes = new ThemeProvider(configuration);
        var gameData = new GameData(dataManager, objectTable);
        var maps = new MapData(dataManager, clientState);
        var weather = new WeatherService(dataManager, clientState);
        var soundBundledDirectory = new DirectoryInfo(
            Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Sounds"));
        var soundUserDirectory = new DirectoryInfo(Path.Combine(configDirectory.FullName, "Sounds"));
        var soundLibrary = new SoundLibrary(soundBundledDirectory, soundUserDirectory);
        var sound = new SoundService(configuration, soundLibrary, new SoundEffectPlayer(), framework);
        var notifications = new NotificationService(sound, configuration, framework);
        var messageArchive = new MessageArchive(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Messages")));
        var messages = new MessageStore(messageArchive);
        var chatBridge = new ChatBridge(messages, notifications, chatGui, gameData);
        var messageLauncher = new MessageLauncher();
        var velvetLauncher = new VelvetLauncher();
        var dmLauncher = new DmLauncher();
        var socialLauncher = new SocialLauncher();
        var linkshellMutes = new LinkshellMuteStore(configuration);
        var linkshells = new LinkshellStore(linkshellMutes);
        var linkshellBridge = new LinkshellBridge(linkshells, linkshellMutes, notifications, chatGui, gameData);
        var cacheRoot = new DirectoryInfo(Path.Combine(configDirectory.FullName, "cache"));
        cacheRoot.Create();
        var mediaRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "media"));
        var http = new HttpService();
        var disk = new DiskCache(mediaRoot, 64L * 1024 * 1024);
        var media = new MediaCache(textures, disk);
        var remoteImages = new RemoteImageCache(http);
        var lodestone = new LodestoneService(configuration, http, media, cacheRoot);
        var collectRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "collect"));
        var collectCache = new DiskCache(collectRoot, 8L * 1024 * 1024);
        var collect = new CollectService(http, collectCache);
        var lookup = new LookupService(lodestone);
        var aethernetSession = new AethernetSession(configuration, framework);
        var aethernetClient = new AethernetClient(http, aethernetSession);
        var gameRegion = clientState.ClientLanguage switch
        {
            Dalamud.Game.ClientLanguage.German => "de",
            Dalamud.Game.ClientLanguage.French => "fr",
            Dalamud.Game.ClientLanguage.Japanese => "ja",
            _ => "en",
        };
        var analytics = new AnalyticsService(new AnalyticsClient(http, aethernetSession), configuration, gameRegion);
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
        var playback = new PlaybackHub(radioPlayer, songPlayer);
        var gameStats = new GameStatsStore(configuration);
        var venues = new VenuesService(http, notifications, configuration, gameData);
        var collectionsRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "collections"));
        var collectionsDisk = new DiskCache(collectionsRoot, 32L * 1024 * 1024);
        var collections = new CollectionsCatalogService(http, collectionsDisk);
        var inventoryRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "inventory"));
        var inventoryStore = new InventoryStore(inventoryRoot);
        var inventoryCapture = new InventoryCaptureService(framework, inventoryStore);
        var calls = new CallHub(configuration, aethernetSession, notifications, sound, playback);
        var socialNotifications = new SocialNotificationService(aethernetSession, aethernetClient, notifications, configuration, framework);
        return new PhoneServices(configuration, themes, gameData, maps, textures, weather, notifications, sound,
            messages, chatBridge, messageLauncher, velvetLauncher, dmLauncher, socialLauncher, linkshellMutes, linkshells,
            linkshellBridge, http, media, remoteImages, lodestone,
            collect, lookup, aethernetSession, aethernetClient, analytics, marketIndex, market, marketLauncher,
            marketAlerts, news, radio, radioPlayer, songSearch, songPlayer, songHistory, playback, gameStats, venues,
            collections, inventoryCapture, calls, socialNotifications);
    }

    public void Dispose()
    {
        SocialNotifications.Dispose();
        Calls.Dispose();
        Collections.Dispose();
        InventoryCapture.Dispose();
        Venues.Dispose();
        SongPlayer.Dispose();
        SongSearch.Dispose();
        RadioPlayer.Dispose();
        Radio.Dispose();
        LinkshellBridge.Dispose();
        ChatBridge.Dispose();
        Collect.Dispose();
        Lookup.Dispose();
        Lodestone.Dispose();
        MarketAlerts.Dispose();
        Market.Dispose();
        News.Dispose();
        Notifications.Dispose();
        Sound.Dispose();
        Media.Dispose();
        RemoteImages.Dispose();
        Analytics.Dispose();
        Http.Dispose();
    }
}
