using System.Numerics;
using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings;

internal sealed class SettingsApp : IPhoneApp, ISettingsNavigator
{
    public string Id => "settings";

    public string DisplayName => Loc.T(L.Apps.Settings);

    public string Glyph => "S";

    public Vector4 Accent => new(0.56f, 0.57f, 0.63f, 1f);

    public int BadgeCount => 0;

    private readonly ViewRouter<ISettingsPage> router;
    private readonly RouterDraw<ISettingsPage> drawPage;
    private readonly Action popBack;

    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    private readonly AccountPage accountPage;

    public SettingsApp(Configuration configuration, ThemeProvider themes, IRingtone ringtone, AethernetSession aethernetSession, AethernetClient aethernetClient, GameData gameData, PhotoLibrary photoLibrary, Action showAbout)
    {
        accountPage = new AccountPage(aethernetSession, aethernetClient, gameData);
        var appearance = new AppearancePage(configuration, themes, this, photoLibrary);
        var language = new LanguagePage(configuration);
        var notifications = new NotificationsPage(configuration);
        var ringtonePage = new RingtonePage(configuration, ringtone);
        var about = new AboutPage(showAbout);

        var groups = new IReadOnlyList<ISettingsPage>[]
        {
            new ISettingsPage[] { accountPage },
            new ISettingsPage[] { appearance, language },
            new ISettingsPage[] { notifications, ringtonePage },
            new ISettingsPage[] { about },
        };

        router = new ViewRouter<ISettingsPage>(new RootSettingsPage(this, groups));
        drawPage = DrawPage;
        popBack = PopBack;
    }

    public void Open(ISettingsPage page) => router.Push(page);

    public void Back() => router.Pop();

    public void OnOpened()
    {
    }

    public void OnClosed() => router.Reset();

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

    private void PopBack() => router.Pop();

    public void Dispose()
    {
        accountPage.Dispose();
    }
}
