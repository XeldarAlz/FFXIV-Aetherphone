using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal enum MusterTab : byte
{
    Discover,
    Plans,
}

internal sealed partial class MusterApp : IResumableApp
{
    private const string StartAnchor = "muster.start";
    private const string PlansAnchor = "muster.tab.plans";
    private const float SignedOutTop = 72f;

    public string Id => "muster";
    public string DisplayName => Loc.T(L.Apps.Muster);
    public string Glyph => "Mu";
    public int BadgeCount => 0;

    private readonly MusterStore store;
    private readonly MusterLauncher launcher;
    private readonly AethernetApi api;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly ReportService report;
    private readonly ConductGateService conduct;
    private readonly AppSkin ui = new(AppPalettes.Muster);
    private readonly ViewRouter<MusterRoute> router;
    private readonly RouterDraw<MusterRoute> drawView;
    private readonly Action back;
    private readonly MusterLabels labels = new();
    private readonly MusterSections sections = new();
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[2];
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private MusterTab activeTab;

    public MusterApp(MusterStore store, MusterLauncher launcher, AethernetApi api, GameData gameData,
        RemoteImageCache images, LodestoneService lodestone, Configuration configuration, ConfirmService confirm,
        TranslationService translation, ReportService report, ConductGateService conduct)
    {
        this.store = store;
        this.launcher = launcher;
        this.api = api;
        this.gameData = gameData;
        this.images = images;
        this.lodestone = lodestone;
        this.configuration = configuration;
        this.confirm = confirm;
        this.translation = translation;
        this.report = report;
        this.conduct = conduct;
        router = new ViewRouter<MusterRoute>(MusterRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        ResetDetailState();
        ResetManageState();
        activeTab = MusterTab.Discover;
        OnResumed();
    }

    public void OnResumed()
    {
        sections.Invalidate();
        store.SyncNow();
        store.RefreshDirectory();
    }

    public void OnClosed()
    {
    }

    private void ConsumeLaunch()
    {
        if (!launcher.TryConsumeDetail(out var musterId))
        {
            return;
        }

        var current = router.Current;
        if (store.Mine is { } mine && mine.Id == musterId)
        {
            if (current.Screen == MusterScreen.Manage)
            {
                return;
            }

            router.Reset();
            activeTab = MusterTab.Plans;
            OpenManage(false);
            return;
        }

        if (current.Screen == MusterScreen.Detail
            && string.Equals(current.MusterId, musterId, StringComparison.Ordinal))
        {
            return;
        }

        router.Reset();
        ResetDetailState();
        router.Push(MusterRoute.Detail(musterId, RootTitle()), false);
    }

    public void Draw(in PhoneContext context)
    {
        store.NoteWatched();
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, scale));
        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            DrawSignedOut(context);
            return;
        }

        TourHolds.Release(Id);
        ConsumeLaunch();
        TickTimers();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawSignedOut(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var bottom = MusterArt.StateScreen(drawList, ui, origin.X + width * 0.5f, origin.Y + SignedOutTop * scale,
                width, FontAwesomeIcon.UserFriends, Loc.T(L.Muster.SignedOutTitle), Loc.T(L.Muster.SetUpAccount),
                scale);
            MusterArt.Reserve(origin, width, bottom + MusterArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "muster.signedout.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void TickTimers()
    {
        var delta = ImGui.GetIO().DeltaTime;
        if (travelNoticeTimer > 0f)
        {
            travelNoticeTimer -= delta;
        }

        if (invitedTimer > 0f)
        {
            invitedTimer -= delta;
        }
    }

    private void DrawView(MusterRoute route, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        switch (route.Screen)
        {
            case MusterScreen.Detail:
                DrawDetail(context, route);
                return;
            case MusterScreen.Create:
                DrawCreate(context, route);
                return;
            case MusterScreen.Manage:
                DrawManage(context, route);
                return;
            case MusterScreen.DataCenter:
                DrawDataCenters(context, route);
                return;
            default:
                DrawRoot(context, area);
                return;
        }
    }

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        sections.Sync(store.ContactMusters, store.Directory, store.GoingMusters, store.Mine, NowUnix());
        using (TabBar.ReserveContent(scale))
        {
            if (activeTab == MusterTab.Plans)
            {
                DrawPlans(context);
            }
            else
            {
                DrawDiscover(context);
            }
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)MusterTab.Discover] = new TabItem(Loc.T(L.Muster.TabDiscover), PhoneIcons.Compass,
            PhoneIcons.CompassFilled, AnchorKey: "muster.tab.discover");
        tabItems[(int)MusterTab.Plans] = new TabItem(Loc.T(L.Muster.TabPlans), PhoneIcons.Calendar,
            PhoneIcons.CalendarFilled, AnchorKey: PlansAnchor);
        var hosting = store.Mine is not null;
        var action = hosting
            ? new TabBarAction(PhoneIcons.Flag, Loc.T(L.Muster.YourMuster), AnchorKey: StartAnchor)
            : new TabBarAction(PhoneIcons.Plus, Loc.T(L.Muster.StartMuster), AnchorKey: StartAnchor);
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab, action);
        if (result.ActionTapped)
        {
            UiFeedback.Play(UiSound.Tap);
            if (hosting)
            {
                OpenManage();
            }
            else
            {
                OpenCreate();
            }

            return;
        }

        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        activeTab = (MusterTab)result.Tapped;
        if (activeTab == MusterTab.Plans)
        {
            store.SyncNow();
        }
    }

    private string RootTitle() => activeTab == MusterTab.Plans ? Loc.T(L.Muster.TabPlans) : DisplayName;

    private void OpenDetail(string musterId)
    {
        if (store.Mine is { } mine && mine.Id == musterId)
        {
            OpenManage();
            return;
        }

        ResetDetailState();
        router.Push(MusterRoute.Detail(musterId, RootTitle()));
    }

    private bool IsGoing(MusterDto muster) =>
        store.IsGoing(muster.Id) || (!store.Primed && muster.Going);

    private bool IsMine(MusterDto muster) => store.Mine is { } mine && mine.Id == muster.Id;

    private void RefreshEverything()
    {
        UiFeedback.Play(UiSound.Refresh);
        store.SyncNow();
        store.RefreshDirectory();
    }

    private static uint KeyFor(string scope, string musterId) => ImGui.GetID($"muster.{scope}.{musterId}");

    private static uint KeyFor(string scope, int slot, string musterId) =>
        ImGui.GetID($"muster.{scope}.{slot}.{musterId}");

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void Dispose()
    {
    }
}
