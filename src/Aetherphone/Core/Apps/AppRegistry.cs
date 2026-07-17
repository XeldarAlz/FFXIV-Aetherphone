using Aetherphone.Apps.Aethergram;
using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Camera;
using Aetherphone.Apps.Chirper;
using Aetherphone.Apps.Message;
using Aetherphone.Apps.Clock;
using Aetherphone.Apps.Collections;
using Aetherphone.Apps.Dailies;
using Aetherphone.Apps.Fishing;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Inventory;
using Aetherphone.Apps.Calculator;
using Aetherphone.Apps.Maps;
using Aetherphone.Apps.Market;
using Aetherphone.Apps.Linkpearl;
using Aetherphone.Apps.Music;
using Aetherphone.Apps.Activity;
using Aetherphone.Apps.News;
using Aetherphone.Apps.Notes;
using Aetherphone.Apps.Notifications;
using Aetherphone.Apps.Photos;
using Aetherphone.Apps.Polls;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Timers;
using Aetherphone.Apps.Dev;
using Aetherphone.Apps.Feedback;
using Aetherphone.Apps.KupoAi;
using Aetherphone.Apps.Velvet;
using Aetherphone.Apps.Venues;
using Aetherphone.Apps.Wallet;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Core.Apps;

internal static class AppRegistry
{
    public static AppBundle BuildDefault(PhoneServices services, Action showAbout)
    {
        var contactBook = new ContactBook(services.Aethernet.Contacts, services.AethernetSession);
        var apps = new List<IPhoneApp>
        {
            new LinkpearlApp(services.Messages, services.Linkshells, services.LinkshellMutes, services.ChatBridge, services.LinkshellBridge, services.LinkpearlLauncher, services.Lodestone, services.Notifications, services.GameData, services.Lookup, services.Confirm),
            new ActivityApp(services.GameData, services.Activity, services.Configuration),
        };

        var photoLibrary = new PhotoLibrary(Plugin.PluginInterface.ConfigDirectory);
        var dmNet = new AethernetApi(services.Http, services.AethernetSession, "dm");
        apps.Insert(0, new MessageApp(new DirectMessagesStore(services.AethernetSession, dmNet.Chats, dmNet.Safety, dmNet.Media, services.Notifications, services.KeyVault, services.ConversationKeys, services.PeerKeys, services.Visibility, services.RealtimeSignals, services.Analytics), contactBook, services.Calls, services.AethernetSession, services.RemoteImages, services.Lodestone, services.DmLauncher, photoLibrary, services.Http, services.Configuration, services.Confirm, services.Report, services.WallpaperImages));
        apps.Add(new ChirperApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "chirper"), services.Lodestone, services.RemoteImages, photoLibrary, services.SocialLauncher, services.GameData, services.Configuration, services.SocialNotifications, services.WallpaperImages, services.Analytics, services.Confirm, services.Report));
        apps.Add(new AethergramApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "aethergram"), services.Lodestone, services.RemoteImages, photoLibrary, services.SocialLauncher, services.GameData, services.Configuration, services.SocialNotifications, services.WallpaperImages, services.Analytics, services.Confirm, services.Report));
        apps.Add(new VelvetShell(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "velvet"), services.Lodestone, services.Configuration, photoLibrary, services.Http, services.RemoteImages, services.Notifications, services.VelvetLauncher, services.SocialLauncher, services.GameData, services.SocialNotifications, services.KeyVault, services.ConversationKeys, services.Visibility, services.RealtimeSignals, services.WallpaperImages, services.Analytics, services.Confirm, services.Report));
        var feedbackNet = new AethernetApi(services.Http, services.AethernetSession, "feedback");
        apps.Add(new FeedbackApp(services.AethernetSession, feedbackNet.Feedback, feedbackNet.Media, photoLibrary, services.Configuration, services.Confirm, services.WallpaperImages));
        apps.Add(new KupoAiApp(new KupoAiStore(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "kupoai").Assistant, new KupoAiArchive(new DirectoryInfo(Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "KupoAI")))), services.Confirm));
        apps.Add(new DevApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "dev"), services.Lodestone, services.Configuration, photoLibrary, services.Http, services.RemoteImages, services.Confirm, services.WallpaperImages));
        apps.Add(new PollsApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "polls").Polls));
        apps.Add(new CameraApp(new PhotoCaptureService(), photoLibrary));
        apps.Add(new PhotosApp(photoLibrary, services.Confirm));
        apps.Add(new SkywatcherApp(services.Weather));
        apps.Add(new VenuesApp(services.Venues, services.Media, services.Http, services.Textures, services.GameData, services.Configuration));
        apps.Add(new MapsApp(services.Maps, services.Configuration));
        apps.Add(new NewsApp(services.News, services.Media, services.Http, services.GameData));
        apps.Add(new CollectionsApp(services.Collections, services.Lodestone, services.Media, services.Http, services.GameData));
        apps.Add(new MarketApp(services.Market, services.MarketIndex, services.MarketAlerts, services.MarketLauncher, services.GameData, services.Textures, services.Configuration, services.Analytics));
        apps.Add(new WalletApp(services.GameData, services.Textures));
        apps.Add(new InventoryApp(services.InventoryCapture, services.GameData, services.Textures));
        apps.Add(new MusicApp(services.Radio, services.SongSearch, services.Playback, services.SongHistory, services.Media, services.Http, services.Textures, new FeatureFlags(services.Http, services.AethernetSession), services.Analytics));
        apps.Add(new ClockApp(services.Configuration, services.Confirm));
        apps.Add(new NotesApp(services.Configuration, services.Confirm));
        apps.Add(new CalculatorApp());
        apps.Add(new TimersApp(services.Configuration));
        apps.Add(new DailiesApp(services.Configuration, services.GameData));
        apps.Add(new FishingApp());
        apps.Add(new GamesApp(services.GameStats, services.Analytics));
        apps.Add(new NotificationsApp(services.Notifications, services.LinkpearlLauncher, services.VelvetLauncher, services.DmLauncher, services.SocialLauncher, services.Analytics));
        apps.Add(new SettingsApp(services, photoLibrary, showAbout));
        var calendarEvents = new CalendarEvents(services.Http, services.AethernetSession);
        apps.Add(new CalendarApp(services.Configuration, calendarEvents, services.Confirm));

        return new AppBundle
        {
            Apps = apps,
            Widgets = WidgetCatalog.Build(services, photoLibrary, calendarEvents, apps),
            Photos = photoLibrary,
        };
    }
}
