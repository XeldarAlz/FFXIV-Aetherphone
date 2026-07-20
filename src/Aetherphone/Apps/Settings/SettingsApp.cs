using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
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
    private readonly NamePage namePage;
    private readonly ProfilePage profilePage;
    private readonly EncryptionPage encryptionPage;
    private readonly ChangelogPage changelogPage;
    private readonly PrivacyPage privacyPage;
    private readonly TagsMentionsPage tagsMentionsPage;

    public SettingsApp(PhoneServices services, PhotoLibrary photoLibrary, Action showAbout)
    {
        sound = services.Sound;
        configuration = services.Configuration;
        var themes = services.Themes;
        var aethernetSession = services.AethernetSession;
        var aethernet = services.Aethernet;
        var keyVault = services.KeyVault;
        var gameData = services.GameData;
        var remoteImages = services.RemoteImages;
        var lodestone = services.Lodestone;
        var calls = services.Calls;
        var analytics = services.Analytics;
        var confirm = services.Confirm;
        var wallpapers = services.Wallpapers;
        var wallpaperImages = services.WallpaperImages;
        profilePage = new ProfilePage(configuration, aethernetSession, aethernet.Account, gameData);
        encryptionPage = new EncryptionPage(aethernetSession, keyVault, confirm);
        namePage = new NamePage(aethernetSession, aethernet.Account, this);
        accountPage = new AccountPage(aethernetSession, aethernet.Auth, aethernet.Account, aethernet.Media, gameData,
            remoteImages, lodestone, this, namePage, profilePage, encryptionPage, photoLibrary, confirm,
            wallpaperImages, analytics);
        var appearance = new AppearancePage(configuration, themes, this, photoLibrary, analytics, confirm, wallpapers,
            wallpaperImages);
        var language = new LanguagePage(configuration, analytics);
        var immersion = new ImmersionPage(configuration, analytics);
        var tutorials = new TutorialsPage(configuration);
        var callsPage = new CallsPage(calls, configuration);
        var appNotifications = new AppNotificationPage(configuration, sound, analytics);
        var notificationSoundPage = new SoundSettingsPage(sound, analytics, L.Settings.NotificationSound,
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
        var notifications = new NotificationsPage(configuration, this, appNotifications, sound, notificationSoundPage,
            analytics);
        var ringtonePage = new SoundSettingsPage(sound, analytics, L.Settings.Ringtone, FontAwesomeIcon.Music,
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
        privacyPage = new PrivacyPage(configuration, aethernetSession, aethernet.Account, aethernet.Safety, analytics,
            confirm);
        tagsMentionsPage = new TagsMentionsPage(aethernetSession, aethernet.Account, this);
        var about = new AboutPage(showAbout);
        changelogPage = new ChangelogPage(configuration);
        var groups = new[]
        {
            new SettingsGroup(new ISettingsPage[] { appearance, language, immersion, tutorials },
                L.Settings.GeneralFooter),
            new SettingsGroup(new ISettingsPage[] { callsPage, notifications, ringtonePage }, L.Settings.AlertsFooter),
            new SettingsGroup(new ISettingsPage[] { commands, privacyPage, tagsMentionsPage, changelogPage, about }),
        };
        router = new ViewRouter<ISettingsPage>(
            new RootSettingsPage(this, groups, aethernetSession, remoteImages, lodestone, accountPage), Id);
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
        if (page.OwnsChrome)
        {
            page.Draw(context, area);
            return;
        }

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
        namePage.Dispose();
        profilePage.Dispose();
        encryptionPage.Dispose();
        privacyPage.Dispose();
        tagsMentionsPage.Dispose();
    }
}
