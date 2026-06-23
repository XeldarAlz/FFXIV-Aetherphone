using System.IO;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Game;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Market;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Theme;
using Dalamud.Plugin.Services;

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

    public AethernetSession AethernetSession { get; }

    public AethernetClient AethernetClient { get; }

    public MarketItemIndex MarketIndex { get; }

    public MarketboardService Market { get; }

    public MarketLauncher MarketLauncher { get; }

    public MarketAlertService MarketAlerts { get; }

    public RadioService Radio { get; }

    public RadioPlayer RadioPlayer { get; }

    private PhoneServices(Configuration configuration, ThemeProvider themes, GameData gameData, ITextureProvider textures, WeatherService weather, NotificationService notifications, IRingtone ringtone, MessageStore messages, ChatBridge chatBridge, MessageLauncher messageLauncher, HttpService http, MediaCache media, LodestoneService lodestone, AethernetSession aethernetSession, AethernetClient aethernetClient, MarketItemIndex marketIndex, MarketboardService market, MarketLauncher marketLauncher, MarketAlertService marketAlerts, RadioService radio, RadioPlayer radioPlayer)
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
        AethernetSession = aethernetSession;
        AethernetClient = aethernetClient;
        MarketIndex = marketIndex;
        Market = market;
        MarketLauncher = marketLauncher;
        MarketAlerts = marketAlerts;
        Radio = radio;
        RadioPlayer = radioPlayer;
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
        var aethernetSession = new AethernetSession(configuration);
        var aethernetClient = new AethernetClient(http, aethernetSession);
        var marketIndex = new MarketItemIndex(dataManager);
        var market = new MarketboardService(http);
        var marketLauncher = new MarketLauncher();
        var marketAlerts = new MarketAlertService(market, notifications, configuration);
        var radio = new RadioService(http);
        var radioPlayer = new RadioPlayer();

        return new PhoneServices(configuration, themes, gameData, textures, weather, notifications, ringtone, messages, chatBridge, messageLauncher, http, media, lodestone, aethernetSession, aethernetClient, marketIndex, market, marketLauncher, marketAlerts, radio, radioPlayer);
    }

    public void Dispose()
    {
        RadioPlayer.Dispose();
        Radio.Dispose();
        ChatBridge.Dispose();
        Lodestone.Dispose();
        MarketAlerts.Dispose();
        Market.Dispose();
        Media.Dispose();
        Http.Dispose();
    }
}
