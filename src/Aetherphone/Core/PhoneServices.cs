using System.IO;
using Aetherphone.Core.Game;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
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

    private PhoneServices(Configuration configuration, ThemeProvider themes, GameData gameData, ITextureProvider textures, WeatherService weather, NotificationService notifications, IRingtone ringtone, MessageStore messages, ChatBridge chatBridge, MessageLauncher messageLauncher, HttpService http, MediaCache media, LodestoneService lodestone)
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
    }

    public static PhoneServices Build(Configuration configuration, INotificationManager notificationManager, IChatGui chatGui, IDataManager dataManager, IObjectTable objectTable, IClientState clientState, ITextureProvider textures, DirectoryInfo configDirectory)
    {
        var themes = new ThemeProvider(configuration);
        var gameData = new GameData(dataManager, objectTable);
        var weather = new WeatherService(dataManager, clientState);
        var toast = new DalamudToast(notificationManager);
        var ringtone = new GameSoundRingtone(configuration);
        var notifications = new NotificationService(toast, ringtone, configuration);
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

        return new PhoneServices(configuration, themes, gameData, textures, weather, notifications, ringtone, messages, chatBridge, messageLauncher, http, media, lodestone);
    }

    public void Dispose()
    {
        ChatBridge.Dispose();
        Lodestone.Dispose();
        Media.Dispose();
        Http.Dispose();
    }
}
