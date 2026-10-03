using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Config;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp : IPhoneApp, ITabRouteTarget
{
    private const string SetupIntent = "hunts.tab.settings";
    private const string AlertsIntent = "hunts.tab.alerts";
    private const float BottomPad = 24f;
    private const int TabCount = 5;

    private enum HuntsRoute : byte
    {
        Root,
        Filters,
        Detail,
        Account,
    }

    private enum HuntsTab : byte
    {
        Now,
        Trains,
        History,
        Alerts,
        Guide,
    }

    private readonly record struct HuntsView(HuntsRoute Route, string MobId = "", string WorldId = "",
        int ZoneInstance = 0, string BackTitle = "");

    public string Id => "hunts";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Hunts);
    public string Glyph => "Hu";
    public int BadgeCount => hunts.ActiveSpawnCount;
    public bool HasBadge => true;

    private readonly HuntsService hunts;
    private readonly HuntMobCatalog mobCatalog;
    private readonly HuntZoneCatalog zoneCatalog;
    private readonly ZoneMapTextures zoneMapTextures;
    private readonly HuntMobRewardCatalog rewardCatalog;
    private readonly HuntCandidateCache candidateCache;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly HuntsLauncher launcher;
    private readonly HuntsMapMarkers huntsMapMarkers;
    private readonly HuntsFilterState filter = new();
    private readonly SettingsSnapshotStore<HuntsFilterSnapshot> filterStore;
    private readonly AppSkin ui = new(AppPalettes.Hunts);
    private readonly ViewRouter<HuntsView> router;
    private readonly RouterDraw<HuntsView> drawView;
    private readonly Action back;
    private readonly Action alertOverridesChanged;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly NavBarButton[] navButtons = new NavBarButton[2];
    private readonly PendingFrameworkAction pendingFlagAction;
    private readonly PendingFrameworkAction pendingWorldHopAction;
    private readonly PendingFrameworkAction pendingInstanceAction;
    private PendingTab pendingTab;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private HuntsTab activeTab;

    public HuntsApp(HuntsService hunts, HuntMobCatalog mobCatalog, HuntZoneCatalog zoneCatalog,
        ZoneMapTextures zoneMapTextures, HuntMobRewardCatalog rewardCatalog, HuntCandidateCache candidateCache,
        Configuration configuration, ConfirmService confirm, HuntsLauncher launcher, HuntsMapMarkers huntsMapMarkers)
    {
        this.hunts = hunts;
        this.mobCatalog = mobCatalog;
        this.zoneCatalog = zoneCatalog;
        this.zoneMapTextures = zoneMapTextures;
        this.rewardCatalog = rewardCatalog;
        this.candidateCache = candidateCache;
        this.configuration = configuration;
        this.confirm = confirm;
        this.launcher = launcher;
        this.huntsMapMarkers = huntsMapMarkers;
        router = new ViewRouter<HuntsView>(new HuntsView(HuntsRoute.Root));
        drawView = DrawView;
        back = PopView;
        alertOverridesChanged = MarkAlertOverridesDirty;
        hunts.NotificationSettings.Changed += alertOverridesChanged;

        filterStore = new SettingsSnapshotStore<HuntsFilterSnapshot>(configuration,
            static config => config.HuntsFilterSettings,
            static (config, snapshot) => config.HuntsFilterSettings = snapshot);
        if (filterStore.Load() is { } savedFilters)
        {
            filter.ApplySnapshot(savedFilters);
        }

        pendingFlagAction = new PendingFrameworkAction(Plugin.Framework, PendingFlagTimeout, FlagRetryDelay,
            IsPendingFlagReady, TryDropPendingFlag, FlagRetryAttempts);
        pendingWorldHopAction = new PendingFrameworkAction(Plugin.Framework, PendingFlagTimeout, WorldHopRetryDelay,
            IsPendingWorldHopReady, TryContinuePendingWorldHop);
        pendingInstanceAction = new PendingFrameworkAction(Plugin.Framework, PendingFlagTimeout, WorldHopRetryDelay,
            IsPendingInstanceSyncReady, TryAdvancePendingInstanceSync);
    }

    public void OpenTab(string tab) => pendingTab.Request(tab);

    public void OnOpened()
    {
        hunts.EnsureActive();
        boardDirty = true;
        activeTab = HuntsTab.Now;
        if (launcher.TryConsumeDetail(out var mobId, out var worldId, out var zoneInstance))
        {
            router.Reset();
            OpenDetailFor(mobId, worldId, zoneInstance, DisplayName);
        }
    }

    public void OnClosed()
    {
        SaveAlertsIfDirty();
        SaveFiltersIfDirty();
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        if (pendingTab.Take(SetupIntent))
        {
            router.Reset();
            activeTab = HuntsTab.Now;
            if (hunts.CurrentDataCenter is null)
            {
                OpenFilters();
            }
        }

        if (pendingTab.Take(AlertsIntent))
        {
            router.Reset();
            activeTab = HuntsTab.Alerts;
        }

        SyncFilterDataCenter();
        ConsumeLoginResult();
        frameTheme = context.Theme;
        ui.Theme = context.Theme;
        navigation = context.Navigation;
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, context.Theme, scale));
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        UpdateTourHold();
    }

    private void DrawView(HuntsView view, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, frameTheme, navigation);
        switch (view.Route)
        {
            case HuntsRoute.Filters:
                DrawFilters(context, view);
                return;
            case HuntsRoute.Detail:
                DrawDetail(context, view);
                return;
            case HuntsRoute.Account:
                DrawAccount(context, view);
                return;
            default:
                DrawRoot(context, area);
                return;
        }
    }

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case HuntsTab.Trains:
                    DrawTrains(context);
                    break;
                case HuntsTab.History:
                    DrawHistory(context);
                    break;
                case HuntsTab.Alerts:
                    DrawAlerts(context);
                    break;
                case HuntsTab.Guide:
                    DrawGuide(context);
                    break;
                default:
                    DrawNow(context);
                    break;
            }
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)HuntsTab.Now] = new TabItem(Loc.T(L.Hunts.NowTab), PhoneIcons.Compass,
            PhoneIcons.CompassFilled, AnchorKey: "hunts.tab.now");
        tabItems[(int)HuntsTab.Trains] = new TabItem(Loc.T(L.Hunts.TrainsTab), PhoneIcons.Navigation,
            PhoneIcons.NavigationFilled, AnchorKey: "hunts.tab.trains");
        tabItems[(int)HuntsTab.History] = new TabItem(Loc.T(L.Hunts.HistoryTab), PhoneIcons.Clock,
            AnchorKey: "hunts.tab.history");
        tabItems[(int)HuntsTab.Alerts] = new TabItem(Loc.T(L.Hunts.AlertsTab), PhoneIcons.Bell, PhoneIcons.BellFilled,
            AnchorKey: "hunts.tab.alerts");
        tabItems[(int)HuntsTab.Guide] = new TabItem(Loc.T(L.Hunts.GuideTab), PhoneIcons.HelpCircle,
            AnchorKey: "hunts.guide");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        SaveAlertsIfDirty();
        activeTab = (HuntsTab)result.Tapped;
        UiFeedback.Play(UiSound.Tap);
        if (activeTab == HuntsTab.History)
        {
            hunts.EnsureHistoryLoaded();
        }
    }

    private void PopView()
    {
        if (router.Current.Route == HuntsRoute.Filters)
        {
            SaveFiltersIfDirty();
        }

        router.Pop();
    }

    private void Push(in HuntsView view)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(view);
    }

    private void UpdateTourHold()
    {
        if (listReadyForTour && router.Depth == 1 && activeTab == HuntsTab.Now)
        {
            TourHolds.Release(Id);
            return;
        }

        TourHolds.Hold(Id);
    }

    private string RootTitle() => activeTab switch
    {
        HuntsTab.Trains => Loc.T(L.Hunts.TrainsTab),
        HuntsTab.History => Loc.T(L.Hunts.HistoryTab),
        HuntsTab.Alerts => Loc.T(L.Hunts.AlertsTab),
        HuntsTab.Guide => Loc.T(L.Hunts.GuideTab),
        _ => DisplayName,
    };

    private static void BottomSpacer(float scale) => ImGui.Dummy(new Vector2(0f, BottomPad * scale));

    private static void Gap(float units) => ImGui.Dummy(new Vector2(0f, units * UiScale.Current));

    public void Dispose()
    {
        hunts.NotificationSettings.Changed -= alertOverridesChanged;
        pendingFlagAction.Disarm();
        pendingWorldHopAction.Disarm();
        pendingInstanceAction.Disarm();
    }
}
