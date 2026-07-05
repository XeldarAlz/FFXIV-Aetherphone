using Aetherphone.Apps.Aethergram;
using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Camera;
using Aetherphone.Apps.Chirper;
using Aetherphone.Apps.Clock;
using Aetherphone.Apps.Collections;
using Aetherphone.Apps.Contacts;
using Aetherphone.Apps.Dailies;
using Aetherphone.Apps.FindPeople;
using Aetherphone.Apps.Fishing;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Inventory;
using Aetherphone.Apps.Calculator;
using Aetherphone.Apps.Maps;
using Aetherphone.Apps.Market;
using Aetherphone.Apps.Messages;
using Aetherphone.Apps.Music;
using Aetherphone.Apps.Activity;
using Aetherphone.Apps.News;
using Aetherphone.Apps.Notes;
using Aetherphone.Apps.Notifications;
using Aetherphone.Apps.Phone;
using Aetherphone.Apps.Photos;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Timers;
using Aetherphone.Apps.Feedback;
using Aetherphone.Apps.Velvet;
using Aetherphone.Apps.Venues;
using Aetherphone.Apps.Wallet;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Photos;

namespace Aetherphone.Core.Apps;

internal static class AppRegistry
{
    public static IReadOnlyList<IPhoneApp> BuildDefault(PhoneServices services, Action showAbout)
    {
        var apps = new List<IPhoneApp>
        {
            new PhoneApp(services.Calls, services.AethernetSession, services.AethernetClient, services.Lodestone),
            new MessagesApp(services.Messages, services.Linkshells, services.ChatBridge, services.LinkshellBridge, services.MessageLauncher, services.Lodestone),
            new ContactsApp(services.GameData, services.MessageLauncher, services.Lodestone),
            new ActivityApp(services.GameData, services.Textures, services.Lodestone, services.Collect),
        };

        var photoLibrary = new PhotoLibrary(Plugin.PluginInterface.ConfigDirectory);
        apps.Add(new ChirperApp(services.AethernetSession, new AethernetClient(services.Http, services.AethernetSession, "chirper"), services.Lodestone, services.Http, photoLibrary));
        apps.Add(new AethergramApp(services.AethernetSession, new AethernetClient(services.Http, services.AethernetSession, "aethergram"), services.Lodestone, services.Http, photoLibrary));
        apps.Add(new VelvetApp(services.AethernetSession, new AethernetClient(services.Http, services.AethernetSession, "velvet"), services.Lodestone, services.Configuration, photoLibrary, services.Http, services.Notifications, services.VelvetLauncher));
        apps.Add(new FeedbackApp(services.AethernetSession, new AethernetClient(services.Http, services.AethernetSession, "feedback"), photoLibrary));
        apps.Add(new CameraApp(new PhotoCaptureService(), photoLibrary));
        apps.Add(new PhotosApp(photoLibrary));
        apps.Add(new SkywatcherApp(services.Weather));
        apps.Add(new VenuesApp(services.Venues, services.Media, services.Http, services.Textures, services.GameData, services.Configuration));
        apps.Add(new MapsApp(services.Maps, services.Configuration));
        apps.Add(new FindPeopleApp(services.Lookup, services.Lodestone, services.MessageLauncher, services.GameData));
        apps.Add(new NewsApp(services.News, services.Media, services.Http, services.GameData));
        apps.Add(new CollectionsApp(services.Collections, services.Lodestone, services.Media, services.Http, services.GameData));
        apps.Add(new MarketApp(services.Market, services.MarketIndex, services.MarketAlerts, services.MarketLauncher, services.GameData, services.Textures, services.Configuration));
        apps.Add(new WalletApp(services.GameData, services.Textures));
        apps.Add(new InventoryApp(services.InventoryCapture, services.GameData, services.Textures));
        apps.Add(new MusicApp(services.Radio, services.SongSearch, services.Playback, services.SongHistory, services.Media, services.Http, services.Textures));
        apps.Add(new ClockApp(services.Configuration));
        apps.Add(new NotesApp(services.Configuration));
        apps.Add(new CalculatorApp());
        apps.Add(new TimersApp(services.Configuration));
        apps.Add(new DailiesApp(services.Configuration));
        apps.Add(new FishingApp());
        apps.Add(new GamesApp(services.GameStats));
        apps.Add(new NotificationsApp(services.Notifications, services.MessageLauncher, services.VelvetLauncher));
        apps.Add(new SettingsApp(services.Configuration, services.Themes, services.Sound, services.AethernetSession, services.AethernetClient, services.GameData, photoLibrary, services.Calls, showAbout));
        apps.Add(new CalendarApp(services.Configuration));

        return apps;
    }
}
