using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum PrismScreen : byte
{
    Home,
    Pick,
    Runway,
    Results,
}

internal sealed partial class PrismApp : IPhoneApp
{
    private const float RefreshSeconds = 120f;

    private readonly PrismStore store;
    private readonly RemoteImageCache images;
    private readonly PhotoLibrary library;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly AppSkin ui = new(AppPalettes.Prism);
    private readonly ViewRouter<PrismScreen> router = new(PrismScreen.Home);
    private readonly PhotoComposeSession pickSession;
    private readonly ImageIntake pickIntake = new();
    private readonly NavBarButton[] reportButton = new NavBarButton[1];
    private readonly RouterDraw<PrismScreen> drawView;
    private readonly Action back;
    private readonly Action<bool> ballotSettled;
    private readonly Action<bool> entrySettled;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private string? pendingSharedPhoto;
    private float sinceRefresh;

    public PrismApp(AethernetSession session, AethernetApi api, RemoteImageCache images, PhotoLibrary library,
        WallpaperImageCache wallpaperImages, ConfirmService confirm, ReportService report)
    {
        store = new PrismStore(session, api);
        this.images = images;
        this.library = library;
        this.confirm = confirm;
        this.report = report;
        pickSession = new PhotoComposeSession(library, wallpaperImages);
        drawView = DrawView;
        back = () => router.Pop();
        withdraw = store.Withdraw;
        ballotSettled = SettleBallot;
        entrySettled = SettleEntry;
    }

    public string Id => "prism";
    public string DisplayName => Loc.T(L.Apps.Prism);
    public string Glyph => "Pr";
    public int BadgeCount => 0;

    public ShareKindSet AcceptedShares => store.IsSignedIn ? ShareKindSet.Photo : ShareKindSet.None;

    public LocString? ShareLabel(ShareKind kind) => L.Prism.ShareLabel;

    public void OnShare(in ShareItem item)
    {
        if (item.Kind == ShareKind.Photo)
        {
            pendingSharedPhoto = item.LocalPath;
        }
    }

    public void OnOpened()
    {
        router.Reset();
        sinceRefresh = 0f;
        store.ClearBallot();
        store.Refresh();
    }

    public void OnClosed()
    {
        pickIntake.Discard();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current));
        TickRefresh();
        ConsumeSharedPhoto();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(PrismScreen screen, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, depth > 1);
        var title = DisplayName;
        switch (screen)
        {
            case PrismScreen.Pick:
                title = Loc.T(L.Prism.EnterLook);
                DrawPick(navBar.Body);
                break;
            case PrismScreen.Runway:
                title = Loc.T(store.Ballot?.Stage == PrismPhases.StageFinal ? L.Prism.JudgeFinal : L.Prism.JudgeHeat);
                DrawRunway(navBar.Body);
                break;
            case PrismScreen.Results:
                title = store.Results?.Title ?? DisplayName;
                DrawResults(navBar.Body);
                break;
            default:
                DrawHome(navBar.Body);
                break;
        }

        var lookId = screen == PrismScreen.Runway ? CurrentLookId() : null;
        reportButton[0] = new NavBarButton(PhoneIcons.Flag, Loc.T(L.Prism.ReportTitle));
        var buttons = lookId is null ? ReadOnlySpan<NavBarButton>.Empty : reportButton;
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "prism.nav", title, NavBarStyle.From(ui), buttons,
            depth > 1 ? DisplayName : string.Empty, depth > 1 ? back : null);
        if (pressed == 0 && lookId is not null)
        {
            OpenReport(lookId);
        }
    }

    private void TickRefresh()
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private void ConsumeSharedPhoto()
    {
        var path = pendingSharedPhoto;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        pendingSharedPhoto = null;
        if (store.Week?.Phase != PrismPhases.Entering)
        {
            return;
        }

        StartPick();
        pickSession.TakeImported(path);
    }

    private void OpenReport(string lookId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Prism.ReportTitle),
            Submit = (reason, done) => store.Report(lookId, reason, done),
        });
    }

    public void Dispose()
    {
        pickSession.Dispose();
        store.Dispose();
    }
}
