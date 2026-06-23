using System.Numerics;
using Aetherphone.Apps.Clock;
using Aetherphone.Apps.Contacts;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Messages;
using Aetherphone.Apps.MyCharacter;
using Aetherphone.Apps.Notifications;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Skywatcher;

namespace Aetherphone.Core.Apps;

internal static class AppRegistry
{
    public static IReadOnlyList<IPhoneApp> BuildDefault(PhoneServices services, Action showAbout)
    {
        return new IPhoneApp[]
        {
            new MessagesApp(services.Messages, services.ChatBridge, services.MessageLauncher),
            new ContactsApp(services.GameData, services.MessageLauncher, services.Lodestone),
            new MyCharacterApp(services.GameData, services.Textures, services.Lodestone),
            new PlaceholderApp("camera", "Camera", "O", new Vector4(0.34f, 0.35f, 0.41f, 1f)),
            new PlaceholderApp("photos", "Photos", "P", new Vector4(0.95f, 0.62f, 0.25f, 1f)),
            new SkywatcherApp(services.Weather),
            new ClockApp(),
            new GamesApp(),
            new NotificationsApp(services.Notifications),
            new SettingsApp(services.Configuration, services.Themes, services.Ringtone, showAbout),
        };
    }
}
