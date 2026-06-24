using System.Numerics;
using Aetherphone.Apps.Chirper;
using Aetherphone.Apps.Clock;
using Aetherphone.Apps.Contacts;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Market;
using Aetherphone.Apps.Messages;
using Aetherphone.Apps.Music;
using Aetherphone.Apps.MyCharacter;
using Aetherphone.Apps.Notifications;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Wallet;

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

        apps.Add(new PlaceholderApp("camera", "Camera", "O", new Vector4(0.34f, 0.35f, 0.41f, 1f)));
        apps.Add(new PlaceholderApp("photos", "Photos", "P", new Vector4(0.95f, 0.62f, 0.25f, 1f)));
        apps.Add(new SkywatcherApp(services.Weather));
        apps.Add(new MarketApp(services.Market, services.MarketIndex, services.MarketAlerts, services.MarketLauncher, services.GameData, services.Textures, services.Configuration));
        apps.Add(new WalletApp(services.GameData, services.Textures));
        apps.Add(new MusicApp(services.Radio, services.SongSearch, services.Playback, services.SongHistory, services.Media, services.Http, services.Textures));
        apps.Add(new ClockApp());
        apps.Add(new GamesApp());
        apps.Add(new NotificationsApp(services.Notifications));
        apps.Add(new SettingsApp(services.Configuration, services.Themes, services.Ringtone, services.AethernetSession, services.AethernetClient, services.GameData, showAbout));

        return apps;
    }
}
