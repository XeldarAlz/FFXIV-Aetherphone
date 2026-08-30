using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings;

internal sealed class SettingsApp : IResumableApp, ISettingsNavigator, ISpotlightPages
{
    public string Id => "settings";
    public string DisplayName => Loc.T(L.Apps.Settings);
    public string Glyph => "S";
    public int BadgeCount => configuration.HasUnseenChangelog ? 1 : 0;
    public bool BadgeAsDot => true;
    public bool WantsSystemTheme => true;
    public ShareKindSet AcceptedShares => ShareKindSet.Photo;
    private readonly Configuration configuration;
    private readonly ViewRouter<ISettingsPage> router;
    private readonly ISettingsPage[] searchablePages;
    private ISettingsPage? pendingPage;
    private readonly RouterDraw<ISettingsPage> drawPage;
    private readonly Action popBack;
    private readonly SoundService sound;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private readonly AccountPage accountPage;
    private readonly SafetyPage safetyPage;
    private readonly SafetyLauncher safetyLauncher;
    private readonly EncryptionSetupLauncher encryptionSetupLauncher;
    private readonly NamePage namePage;
    private readonly ProfilePage profilePage;
    private readonly EncryptionPage encryptionPage;
    private readonly ChangelogPage changelogPage;
    private readonly PrivacyPage privacyPage;
    private readonly TagsMentionsPage tagsMentionsPage;
    private readonly ThemeProvider themes;
    private readonly WallpaperLibrary wallpapers;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly Action<string> assignWallpaper;
    private string? pendingSharedWallpaper;

    public SettingsApp(PhoneServices services, PhotoLibrary photoLibrary)
    {
        sound = services.Sound;
        configuration = services.Configuration;
        themes = services.Themes;
        var aethernetSession = services.AethernetSession;
        var aethernet = services.Aethernet;
        var keyVault = services.KeyVault;
        var gameData = services.GameData;
        var remoteImages = services.RemoteImages;
        var lodestone = services.Lodestone;
        var calls = services.Calls;
        var confirm = services.Confirm;
        wallpapers = services.Wallpapers;
        wallpaperImages = services.WallpaperImages;
        profilePage = new ProfilePage(configuration, aethernetSession, aethernet.Account, gameData);
        encryptionPage = new EncryptionPage(aethernetSession, keyVault, confirm);
        namePage = new NamePage(aethernetSession, aethernet.Account, this);
        var coinPage = new CoinPage(services.Coins);
        accountPage = new AccountPage(configuration, aethernetSession, aethernet.Auth, aethernet.Account,
            services.AccountState, aethernet.Media, gameData, remoteImages, lodestone, this, namePage, profilePage,
            encryptionPage, coinPage, photoLibrary, confirm, wallpaperImages);
        var appearance = new AppearancePage(configuration, themes, this, photoLibrary, confirm, wallpapers,
            wallpaperImages, services.MinimizedLayout);
        var language = new LanguagePage(configuration, services.Translation);
        var general = new GeneralPage(configuration, services.Translation, confirm);
        var tutorials = new TutorialsPage(configuration);
        var callsPage = new CallsPage(calls, configuration);
        var appNotifications = new AppNotificationPage(configuration, sound);
        var notificationSoundPage = new SoundSettingsPage(sound, SoundKind.Notification, L.Settings.NotificationSound,
            FontAwesomeIcon.Bell, new Vector4(0.98f, 0.27f, 0.25f, 1f), "settings.notificationVolume",
            () => configuration.NotificationSound, token =>
            {
                configuration.NotificationSound = token;
                configuration.Save();
            }, () => configuration.NotificationVolume, volume =>
            {
                configuration.NotificationVolume = volume;
                configuration.Save();
            });
        var notifications = new NotificationsPage(configuration, this, appNotifications, services.Installer);
        var ringtonePage = new SoundSettingsPage(sound, SoundKind.Ringtone, L.Settings.Ringtone, FontAwesomeIcon.Music,
            new Vector4(0.95f, 0.40f, 0.65f, 1f), "settings.ringtoneVolume",
            () => configuration.RingtoneSound, token =>
            {
                configuration.RingtoneSound = token;
                configuration.Save();
            }, () => configuration.RingtoneVolume, volume =>
            {
                configuration.RingtoneVolume = volume;
                configuration.Save();
            });
        var sounds = new SoundsPage(configuration, sound, this, ringtonePage, notificationSoundPage);
        safetyPage = new SafetyPage(aethernetSession, services.ModerationArchive, this);
        safetyLauncher = services.SafetyLauncher;
        encryptionSetupLauncher = services.EncryptionSetup;
        var commands = new CommandsPage();
        tagsMentionsPage = new TagsMentionsPage(aethernetSession, aethernet.Account, this);
        privacyPage = new PrivacyPage(configuration, aethernetSession, aethernet.Account, aethernet.Safety,
            confirm, this, tagsMentionsPage);
        var about = new AboutPage(configuration, gameData, aethernetSession);
        changelogPage = new ChangelogPage(configuration);
        var groups = new[]
        {
            new ISettingsPage[] { general, appearance, sounds, notifications, callsPage, language },
            new ISettingsPage[] { privacyPage, safetyPage },
            new ISettingsPage[] { tutorials, commands, changelogPage, about },
        };
        var searchableCount = 0;
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            searchableCount += groups[groupIndex].Length;
        }

        searchablePages = new ISettingsPage[searchableCount];
        var searchableIndex = 0;
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            for (var pageIndex = 0; pageIndex < groups[groupIndex].Length; pageIndex++)
            {
                searchablePages[searchableIndex++] = groups[groupIndex][pageIndex];
            }
        }

        router = new ViewRouter<ISettingsPage>(
            new RootSettingsPage(this, groups, configuration, aethernetSession, remoteImages, lodestone,
                accountPage));
        drawPage = DrawPage;
        popBack = PopBack;
        assignWallpaper = AssignWallpaper;
    }

    public LocString? ShareLabel(ShareKind kind) =>
        kind == ShareKind.Photo ? L.Share.SetAsWallpaper : null;

    public void OnShare(in ShareItem item)
    {
        if (item.Kind != ShareKind.Photo)
        {
            return;
        }

        pendingSharedWallpaper = item.LocalPath;
    }

    private void AssignWallpaper(string id)
    {
        if (wallpapers.ThemeDarkness >= 0.5f)
        {
            configuration.DarkWallpaperId = id;
        }
        else
        {
            configuration.LightWallpaperId = id;
        }

        themes.Apply(configuration);
        configuration.Save();
    }

    private void ConsumeSharedWallpaper()
    {
        var path = pendingSharedWallpaper;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        pendingSharedWallpaper = null;
        router.Reset();
        router.Push(new WallpaperCropPage(path, this, assignWallpaper, wallpapers, wallpaperImages));
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
        router.Reset();
        ConsumePendingPage();
    }

    public void OnResumed()
    {
        ConsumePendingPage();
    }

    public IReadOnlyList<ISettingsPage> SearchablePages => searchablePages;

    public void RequestPage(ISettingsPage page) => pendingPage = page;

    public int SpotlightPageCount => searchablePages.Length;

    public string SpotlightPageTitle(int pageIndex) => searchablePages[pageIndex].Title;

    public void RequestSpotlightPage(int pageIndex) => pendingPage = searchablePages[pageIndex];

    private void ConsumePendingPage()
    {
        if (pendingPage is not { } page)
        {
            return;
        }

        pendingPage = null;
        Open(page);
    }

    public void OnClosed()
    {
        sound.StopPreview();
    }

    public void Draw(in PhoneContext context)
    {
        ConsumeSharedWallpaper();
        if (safetyLauncher.TryConsume())
        {
            router.Reset();
            router.Push(safetyPage);
        }

        if (encryptionSetupLauncher.TryConsume())
        {
            router.Reset();
            router.Push(encryptionPage);
        }

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
        var scale = UiScale.Current;
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
