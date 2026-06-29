using System.IO;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Character;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Market;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Net;
using Aetherphone.Core.News;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
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

    public ITextureProvider Textures { get; }

    public WeatherService Weather { get; }

    public NotificationService Notifications { get; }

    public IRingtone Ringtone { get; }

    public MessageStore Messages { get; }

    public ChatBridge ChatBridge { get; }

    public MessageLauncher MessageLauncher { get; }

    public HttpService Http { get; }

    public MediaCache Media { get; }

    public LodestoneService Lodestone { get; }

    public CollectService Collect { get; }

    public AethernetSession AethernetSession { get; }

    public AethernetClient AethernetClient { get; }

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

    private PhoneServices(Configuration configuration, ThemeProvider themes, GameData gameData, ITextureProvider textures, WeatherService weather, NotificationService notifications, IRingtone ringtone, MessageStore messages, ChatBridge chatBridge, MessageLauncher messageLauncher, HttpService http, MediaCache media, LodestoneService lodestone, CollectService collect, AethernetSession aethernetSession, AethernetClient aethernetClient, MarketItemIndex marketIndex, MarketboardService market, MarketLauncher marketLauncher, MarketAlertService marketAlerts, NewsService news, RadioService radio, RadioPlayer radioPlayer, SongSearchService songSearch, SongPlayer songPlayer, SongHistory songHistory, PlaybackHub playback, GameStatsStore gameStats, VenuesService venues)
    {
        Configuration = configuration;
        Themes = themes;
        GameData = gameData;
        Textures = textures;
        Weather = weather;
        Notifications = notifications;
        Ringtone = ringtone;
        Messages = messages;
        ChatBridge = chatBridge;
        MessageLauncher = messageLauncher;
        Http = http;
        Media = media;
        Lodestone = lodestone;
        Collect = collect;
        AethernetSession = aethernetSession;
        AethernetClient = aethernetClient;
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
    }

    public static PhoneServices Build(Configuration configuration, IChatGui chatGui, IDataManager dataManager, IObjectTable objectTable, IClientState clientState, ITextureProvider textures, DirectoryInfo configDirectory)
    {
        var themes = new ThemeProvider(configuration);
        var gameData = new GameData(dataManager, objectTable);
        var weather = new WeatherService(dataManager, clientState);
        var ringtone = new GameSoundRingtone(configuration);
        var notifications = new NotificationService(ringtone, configuration);
        var messages = new MessageStore();
        var chatBridge = new ChatBridge(messages, notifications, chatGui, gameData);
        var messageLauncher = new MessageLauncher();

        var cacheRoot = new DirectoryInfo(Path.Combine(configDirectory.FullName, "cache"));
        cacheRoot.Create();
        var mediaRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "media"));
        var http = new HttpService();
        var disk = new DiskCache(mediaRoot, 64L * 1024 * 1024);
        var media = new MediaCache(textures, disk);
        var lodestone = new LodestoneService(configuration, http, media, cacheRoot);
        var collectRoot = new DirectoryInfo(Path.Combine(cacheRoot.FullName, "collect"));
        var collectCache = new DiskCache(collectRoot, 8L * 1024 * 1024);
        var collect = new CollectService(http, collectCache);
        var aethernetSession = new AethernetSession(configuration);
        var aethernetClient = new AethernetClient(http, aethernetSession);
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

        return new PhoneServices(configuration, themes, gameData, textures, weather, notifications, ringtone, messages, chatBridge, messageLauncher, http, media, lodestone, collect, aethernetSession, aethernetClient, marketIndex, market, marketLauncher, marketAlerts, news, radio, radioPlayer, songSearch, songPlayer, songHistory, playback, gameStats, venues);
    }

    public void Dispose()
    {
        Venues.Dispose();
        SongPlayer.Dispose();
        SongSearch.Dispose();
        RadioPlayer.Dispose();
        Radio.Dispose();
        ChatBridge.Dispose();
        Collect.Dispose();
        Lodestone.Dispose();
        MarketAlerts.Dispose();
        Market.Dispose();
        News.Dispose();
        Media.Dispose();
        Http.Dispose();
    }
}
