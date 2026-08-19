using Aetherphone.Apps.Aethergram;
using Aetherphone.Apps.AetherStream;
using Aetherphone.Apps.Announcements;
using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Camera;
using Aetherphone.Apps.Chirper;
using Aetherphone.Apps.Message;
using Aetherphone.Apps.Clock;
using Aetherphone.Apps.Collections;
using Aetherphone.Apps.Dailies;
using Aetherphone.Apps.Fishing;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Health;
using Aetherphone.Apps.Housing;
using Aetherphone.Apps.Inventory;
using Aetherphone.Apps.Jobs;
using Aetherphone.Apps.Calculator;
using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Maps;
using Aetherphone.Apps.Market;
using Aetherphone.Apps.Linkpearl;
using Aetherphone.Apps.Music;
using Aetherphone.Apps.Muster;
using Aetherphone.Apps.Activity;
using Aetherphone.Apps.AppStore;
using Aetherphone.Apps.News;
using Aetherphone.Apps.Notes;
using Aetherphone.Apps.Notifications;
using Aetherphone.Apps.Photos;
using Aetherphone.Apps.Polls;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Shortcuts;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Timers;
using Aetherphone.Apps.Feedback;
using Aetherphone.Apps.Velvet;
using Aetherphone.Apps.Venues;
using Aetherphone.Apps.VenueSync;
using Aetherphone.Apps.Wallet;
using Aetherphone.Apps.YellowPages;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Video;
using Aetherphone.Windows;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Core.Apps;

internal static class AppRegistry
{
    public static AppBundle BuildDefault(PhoneServices services, VideoPlayer video, ScreenController screen,
        AetherStreamQueue videoQueue, WatchAlongSession watchAlong, StreamSuggestionNotifier streamSuggestions,
        AetherStreamScreenWindow screenWindow)
    {
        var contactBook = new ContactBook(services.Aethernet.Contacts, services.AethernetSession);
        var apps = new List<IPhoneApp>
        {
            new LinkpearlApp(services.ChatInbox, services.ChatTabs, services.ChatArchive, services.LinkpearlNotificationGate, services.LinkpearlLauncher, services.Lodestone, services.MarketLauncher, services.Notifications, services.GameData, services.Lookup, services.Confirm, services.ChatLog, services.ChatSend),
            new ActivityApp(services.GameData, services.Activity, services.Configuration),
            new HealthApp(services.Health, services.GameData, services.Confirm),
        };

        var photoLibrary = new PhotoLibrary(Plugin.PluginInterface.ConfigDirectory);
        var dmNet = new AethernetApi(services.Http, services.AethernetSession, "dm");
        apps.Insert(0, new MessageApp(new DirectMessagesStore(services.AethernetSession, dmNet.Chats, dmNet.Safety, dmNet.Media, services.Notifications, services.KeyVault, services.ConversationKeys, services.PeerKeys, services.Visibility, services.RealtimeSignals, services.Installer), contactBook, services.Calls, services.AethernetSession, services.RemoteImages, services.Lodestone, services.DmLauncher, photoLibrary, services.Http, services.Configuration, services.Confirm, services.Report, services.WallpaperImages, services.Musters, services.MusterLauncher, services.SocialNotifications, services.EncryptionSetup));
        apps.Add(new ChirperApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "chirper"), services.Lodestone, services.RemoteImages, photoLibrary, services.SocialLauncher, services.GameData, services.Configuration, services.SocialNotifications, services.WallpaperImages, services.Confirm, services.Report, services.Conduct, services.RealtimeSignals));
        apps.Add(new AethergramApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "aethergram"), services.Lodestone, services.RemoteImages, photoLibrary, services.SocialLauncher, services.GramDmLauncher, services.GameData, services.Configuration, services.SocialNotifications, services.Notifications, services.Http, services.KeyVault, services.ConversationKeys, services.Visibility, services.RealtimeSignals, services.WallpaperImages, services.Confirm, services.Report, services.Conduct, services.Installer));
        apps.Add(new VelvetShell(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "velvet"), services.Lodestone, services.Configuration, photoLibrary, services.Http, services.RemoteImages, services.Notifications, services.VelvetLauncher, services.SocialLauncher, services.GameData, services.SocialNotifications, services.KeyVault, services.ConversationKeys, services.Visibility, services.RealtimeSignals, services.WallpaperImages, services.Confirm, services.Report, services.Conduct, services.Installer));
        var feedbackNet = new AethernetApi(services.Http, services.AethernetSession, "feedback");
        apps.Add(new FeedbackApp(services.AethernetSession, feedbackNet.Feedback, feedbackNet.Media, photoLibrary, services.Configuration, services.Confirm, services.WallpaperImages));
        apps.Add(new PollsApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "polls").Polls, services.Installer));
        apps.Add(new AnnouncementsApp(services.AethernetSession, new AethernetApi(services.Http, services.AethernetSession, "announcements").Announcements, services.Notifications, services.Configuration, services.AnnouncementsLauncher, services.RealtimeSignals));
        apps.Add(new CameraApp(new PhotoCaptureService(), photoLibrary, services.Configuration));
        apps.Add(new PhotosApp(photoLibrary, services.Confirm, services.Share, services.Configuration));
        apps.Add(new SkywatcherApp(services.Weather, services.WeatherControl));
        apps.Add(new VenuesApp(services.Venues, services.Media, services.Http, services.Textures, services.GameData, services.Configuration));
        apps.Add(new VenueSyncApp(services.VenueSync, services.VenueSyncState, services.Configuration, services.GameData));
        apps.Add(new MusterApp(services.Musters, services.MusterLauncher, services.Aethernet, services.GameData, services.RemoteImages, services.Lodestone, services.Configuration, services.Confirm, services.Report, services.Conduct));
        apps.Add(new YellowPagesApp(services.YellowPages, services.AdInquiries, services.YellowPagesLauncher, services.SocialNotifications, services.GramDmLauncher, services.Musters, new AethernetApi(services.Http, services.AethernetSession, "yellowpages"), services.GameData, services.RemoteImages, services.Lodestone, photoLibrary, services.WallpaperImages, services.Configuration, services.Confirm, services.Report, services.Conduct));
        apps.Add(new MapsApp(services.Maps, services.Configuration));
        apps.Add(new NewsApp(services.News, services.Media, services.Http, services.GameData));
        apps.Add(new CollectionsApp(services.Collections, services.Lodestone, services.Media, services.Http, services.GameData));
        apps.Add(new MarketApp(services.Market, services.MarketIndex, services.MarketAlerts, services.MarketLauncher, services.GameData, services.Textures, services.Configuration));
        apps.Add(new WalletApp(services.GameData, services.Textures, services.Configuration));
        apps.Add(new InventoryApp(services.InventoryCapture, services.GameData, services.Textures));
        apps.Add(new JobsApp(services.GameData, services.Textures, services.Configuration, services.Confirm, services.CharacterWatch));
        apps.Add(new MusicApp(services.Radio, services.SongSearch, services.SongResolver, services.Playback, services.SongHistory, services.Playlists, services.Media, services.Http, services.Textures, services.Aethernet, services.AethernetSession, services.Report, photoLibrary, services.WallpaperImages, services.Confirm, services.Configuration, services.RadioLauncher));
        apps.Add(new ClockApp(services.Configuration, services.Confirm));
        apps.Add(new NotesApp(services.Configuration, services.Confirm));
        apps.Add(new CalculatorApp());
        apps.Add(new AetherStreamApp(video, screen, videoQueue, services.Configuration, services.Confirm,
            services.RemoteImages, services.Http, services.AethernetSession, services.Lodestone, watchAlong,
            streamSuggestions, services.AetherStreamLauncher, screenWindow));
        apps.Add(new ShortcutsApp(services.Shortcuts, services.ShortcutRunner, services.Confirm));
        apps.Add(new TimersApp(services.Configuration));
        apps.Add(new DailiesApp(services.Configuration, services.GameData));
        apps.Add(new FishingApp());
        apps.Add(new GamesApp(services.GameStats, services.GameData, services.Textures, services.Coins,
            services.CoinSessions));
        apps.Add(new NotificationsApp(services.Notifications, services.SocialNotifications, services.LinkpearlLauncher, services.VelvetLauncher, services.DmLauncher, services.GramDmLauncher, services.SocialLauncher, services.MusterLauncher, services.YellowPagesLauncher, services.AnnouncementsLauncher, services.SafetyLauncher, services.RadioLauncher, services.CasinoLauncher, services.AetherStreamLauncher));
        apps.Add(new SettingsApp(services, photoLibrary));
        var calendarEvents = new CalendarEvents(services.Http, services.AethernetSession);
        apps.Add(new CalendarApp(services.Configuration, calendarEvents, services.Confirm));
        apps.Add(new Aetherphone.Apps.Coin.CoinApp(services.AethernetSession, services.Coins, services.CoinCatalog,
            services.Confirm, services.Conduct, services.BadgeCatalog, services.RemoteImages, services.Casino,
            services.FrameCatalog, services.Loadout, services.Lodestone));
        apps.Add(new CasinoApp(services.AethernetSession, services.Coins, services.Casino, services.CasinoPlay,
            services.CasinoHistory, services.CasinoRooms, services.CasinoTables, services.CasinoSpin,
            services.CasinoTurns, services.CasinoLauncher, services.GameStats, services.Confirm,
            services.Conduct, services.RemoteImages, services.Lodestone));
        apps.Add(new AppStoreApp(services.Installer, apps));
        apps.Add(new HousingApp(services.Housing, services.Configuration, services.Confirm));

        return new AppBundle
        {
            Apps = apps,
            Widgets = WidgetCatalog.Build(services, photoLibrary, calendarEvents, apps),
            Photos = photoLibrary,
        };
    }
}
