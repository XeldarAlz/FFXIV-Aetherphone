using Aetherphone.Apps.Camera;
using Aetherphone.Apps.Chirper;
using Aetherphone.Apps.Clock;
using Aetherphone.Apps.Contacts;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Market;
using Aetherphone.Apps.Messages;
using Aetherphone.Apps.Music;
using Aetherphone.Apps.MyCharacter;
using Aetherphone.Apps.Notifications;
using Aetherphone.Apps.Photos;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Wallet;
using Aetherphone.Core.Photos;

namespace Aetherphone.Core.Apps;

internal static class AppRegistry
{
    public static IReadOnlyList<IPhoneApp> BuildDefault(PhoneServices services, Action showAbout)
    {
        var apps = new List<IPhoneApp>
        {
            new MessagesApp(services.Messages, services.ChatBridge, services.MessageLauncher, services.Lodestone),
            new ContactsApp(services.GameData, services.MessageLauncher, services.Lodestone),
            new MyCharacterApp(services.GameData, services.Textures, services.Lodestone),
        };

        if (services.Configuration.ChirperEnabled)
        {
            apps.Add(new ChirperApp(services.AethernetSession, services.AethernetClient, services.Lodestone));
        }

        var photoLibrary = new PhotoLibrary(Plugin.PluginInterface.ConfigDirectory);
        apps.Add(new CameraApp(new PhotoCaptureService(), photoLibrary));
        apps.Add(new PhotosApp(photoLibrary));
        apps.Add(new SkywatcherApp(services.Weather));
        apps.Add(new MarketApp(services.Market, services.MarketIndex, services.MarketAlerts, services.MarketLauncher, services.GameData, services.Textures, services.Configuration));
        apps.Add(new WalletApp(services.GameData, services.Textures));
        apps.Add(new MusicApp(services.Radio, services.SongSearch, services.Playback, services.SongHistory, services.Media, services.Http, services.Textures));
        apps.Add(new ClockApp());
        apps.Add(new GamesApp(services.GameStats));
        apps.Add(new NotificationsApp(services.Notifications));
        apps.Add(new SettingsApp(services.Configuration, services.Themes, services.Ringtone, services.AethernetSession, services.AethernetClient, services.GameData, photoLibrary, showAbout));

        return apps;
    }
}
