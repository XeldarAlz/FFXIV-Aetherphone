using System.Numerics;
using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings;

internal sealed class SettingsApp : IPhoneApp, ISettingsNavigator
{
    public string Id => "settings";
    public string DisplayName => Loc.T(L.Apps.Settings);
    public string Glyph => "S";
    public int BadgeCount => configuration.HasUnseenChangelog ? 1 : 0;
    public bool BadgeAsDot => true;
    private readonly Configuration configuration;
    private readonly ViewRouter<ISettingsPage> router;
    private readonly RouterDraw<ISettingsPage> drawPage;
    private readonly Action popBack;
    private readonly SoundService sound;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private readonly AccountPage accountPage;
    private readonly ProfilePage profilePage;
    private readonly ChangelogPage changelogPage;

    public SettingsApp(Configuration configuration, ThemeProvider themes, SoundService sound,
        AethernetSession aethernetSession, AethernetClient aethernetClient, GameData gameData,
        PhotoLibrary photoLibrary, CallHub calls, Action showAbout)
    {
        this.sound = sound;
        this.configuration = configuration;
        accountPage = new AccountPage(aethernetSession, aethernetClient, gameData);
        profilePage = new ProfilePage(configuration, aethernetSession, aethernetClient, gameData);
        var appearance = new AppearancePage(configuration, themes, this, photoLibrary);
        var language = new LanguagePage(configuration);
        var immersion = new ImmersionPage(configuration);
        var tutorials = new TutorialsPage(configuration);
        var callsPage = new CallsPage(calls, configuration);
        var appNotifications = new AppNotificationPage(configuration, sound);
        var notificationSoundPage = new SoundSettingsPage(sound, Loc.T(L.Settings.NotificationSound),
            FontAwesomeIcon.Bell, new Vector4(0.98f, 0.27f, 0.25f, 1f), "settings.notificationVolume",
            "notification_sound",
            () => configuration.NotificationSound, token =>
            {
                configuration.NotificationSound = token;
                configuration.Save();
            }, () => configuration.NotificationVolume, volume =>
            {
                configuration.NotificationVolume = volume;
                configuration.Save();
            });
        var notifications = new NotificationsPage(configuration, this, appNotifications, sound, notificationSoundPage);
        var ringtonePage = new SoundSettingsPage(sound, Loc.T(L.Settings.Ringtone), FontAwesomeIcon.Music,
            new Vector4(0.95f, 0.40f, 0.65f, 1f), "settings.ringtoneVolume", "ringtone",
            () => configuration.RingtoneSound, token =>
            {
                configuration.RingtoneSound = token;
                configuration.Save();
            }, () => configuration.RingtoneVolume, volume =>
            {
                configuration.RingtoneVolume = volume;
                configuration.Save();
            });
        var commands = new CommandsPage();
        var privacy = new PrivacyPage(configuration);
        var about = new AboutPage(showAbout);
        changelogPage = new ChangelogPage(configuration);
        var groups = new[]
        {
            new SettingsGroup(new ISettingsPage[] { profilePage }),
            new SettingsGroup(new ISettingsPage[] { appearance, language, immersion, tutorials },
                L.Settings.GeneralFooter),
            new SettingsGroup(new ISettingsPage[] { callsPage, notifications, ringtonePage }, L.Settings.AlertsFooter),
            new SettingsGroup(new ISettingsPage[] { commands, privacy, about, changelogPage }),
        };
        router = new ViewRouter<ISettingsPage>(new RootSettingsPage(this, groups, aethernetSession, accountPage), Id);
        drawPage = DrawPage;
        popBack = PopBack;
    }

    public void Open(ISettingsPage page)
    {
        if (page == changelogPage)
        {
            configuration.MarkChangelogSeen();
        }

        router.Push(page);
    }

    public void Back()
    {
        sound.StopPreview();
        router.Pop();
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
        sound.StopPreview();
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawPage);
    }

    private void DrawPage(ISettingsPage page, Rect area, int depth)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        var onBack = depth > 1 ? popBack : null;
        AppHeader.Draw(context, page.Title, onBack);
        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        page.Draw(context, body);
    }

    private void PopBack()
    {
        sound.StopPreview();
        router.Pop();
    }

    public void Dispose()
    {
        accountPage.Dispose();
        profilePage.Dispose();
    }
}
