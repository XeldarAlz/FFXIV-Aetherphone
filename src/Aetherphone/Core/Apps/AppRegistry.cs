using Aetherphone.Apps.Aethergram;
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
using Aetherphone.Apps.Maps;
using Aetherphone.Apps.Market;
using Aetherphone.Apps.Messages;
using Aetherphone.Apps.Music;
using Aetherphone.Apps.MyCharacter;
using Aetherphone.Apps.News;
using Aetherphone.Apps.Notifications;
using Aetherphone.Apps.Phone;
using Aetherphone.Apps.Photos;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Timers;
using Aetherphone.Apps.Velvet;
using Aetherphone.Apps.Venues;
using Aetherphone.Apps.Wallet;
using Aetherphone.Core.Photos;

namespace Aetherphone.Core.Apps;

internal static class AppRegistry
{
    public static IReadOnlyList<IPhoneApp> BuildDefault(PhoneServices services, Action showAbout)
    {
        var apps = new List<IPhoneApp>
        {
            new PhoneApp(services.Calls, services.AethernetSession, services.AethernetClient, services.Lodestone),
            new MessagesApp(services.Messages, services.ChatBridge, services.MessageLauncher, services.Lodestone),
            new ContactsApp(services.GameData, services.MessageLauncher, services.Lodestone),
            new MyCharacterApp(services.GameData, services.Textures, services.Lodestone, services.Collect),
        };

        apps.Add(new ChirperApp(services.AethernetSession, services.AethernetClient, services.Lodestone));

        var photoLibrary = new PhotoLibrary(Plugin.PluginInterface.ConfigDirectory);
        apps.Add(new AethergramApp(services.AethernetSession, services.AethernetClient, services.Lodestone, services.Http, photoLibrary));
        apps.Add(new VelvetApp(services.AethernetSession, services.AethernetClient, services.Lodestone, services.Configuration, photoLibrary));
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
        apps.Add(new ClockApp());
        apps.Add(new TimersApp(services.Configuration));
        apps.Add(new DailiesApp(services.Configuration));
        apps.Add(new FishingApp());
        apps.Add(new GamesApp(services.GameStats));
        apps.Add(new NotificationsApp(services.Notifications));
        apps.Add(new SettingsApp(services.Configuration, services.Themes, services.Ringtone, services.AethernetSession, services.AethernetClient, services.GameData, photoLibrary, services.Calls, showAbout));

        return apps;
    }
}
