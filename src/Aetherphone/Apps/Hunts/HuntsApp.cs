using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp : IPhoneApp
{
    private enum HuntsRoute : byte
    {
        List,
        Filters,
        Detail,
        Signup,
        History,
        NotificationSettings,
        MarkNotifications,
        Guide,
    }

    private readonly record struct HuntsView(HuntsRoute Route, string MobId = "", string WorldId = "",
        int ZoneInstance = 0);

    public string Id => "hunts";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Hunts);
    public string Glyph => "Hu";
    public int BadgeCount => hunts.ActiveSpawnCount;

    private readonly HuntsService hunts;
    private readonly HuntMobCatalog mobCatalog;
    private readonly HuntZoneCatalog zoneCatalog;
    private readonly HuntMobRewardCatalog rewardCatalog;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly HuntsLauncher launcher;
    private const float ToolbarHeight = 44f;
    private const float ToolbarButtonSize = 34f;

    private readonly HuntsFilterState filter = new();
    private readonly SettingsSnapshotStore<HuntsFilterSnapshot> filterStore;
    private readonly AppSkin ui = new(AppPalettes.Hunts);
    private readonly ViewRouter<HuntsView> router;
    private readonly RouterDraw<HuntsView> drawView;
    private readonly BottomTabBar footer = new();
    private readonly NavTab[] footerTabs = new NavTab[4];
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private Rect frameScreen;
    private string searchQuery = string.Empty;
    private INavigator navigation = null!;

    public HuntsApp(HuntsService hunts, HuntMobCatalog mobCatalog, HuntZoneCatalog zoneCatalog,
        HuntMobRewardCatalog rewardCatalog, Configuration configuration, ConfirmService confirm,
        HuntsLauncher launcher)
    {
        this.hunts = hunts;
        this.mobCatalog = mobCatalog;
        this.zoneCatalog = zoneCatalog;
        this.rewardCatalog = rewardCatalog;
        this.configuration = configuration;
        this.confirm = confirm;
        this.launcher = launcher;
        compareByPercentageDescending = CompareByPercentageDescending;
        router = new ViewRouter<HuntsView>(new HuntsView(HuntsRoute.List));
        drawView = DrawView;

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

    public void OnOpened()
    {
        hunts.EnsureActive();
        if (launcher.TryConsumeDetail(out var mobId, out var worldId, out var zoneInstance))
        {
            router.Reset();
            OpenDetailFor(mobId, worldId, zoneInstance);
        }
    }

    public void OnClosed()
    {
        SaveNotificationSettingsIfDirty();
        SaveMarkNotificationsIfDirty();
        router.Reset();
        router.Replace(new HuntsView(HuntsRoute.List));
        menu.Close();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        ui.Theme = context.Theme;
        navigation = context.Navigation;
        var scale = UiScale.Current;
        var content = context.Content;
        frameScreen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(frameScreen);
        menu.Gate();
        router.Draw(content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawMenu();
    }

    private void DrawView(HuntsView view, Rect area, int depth)
    {
        var scale = UiScale.Current;
        ui.Body(area);
        switch (view.Route)
        {
            case HuntsRoute.Filters:
                DrawFiltersHeader(area, scale);
                var filtersBody = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
                DrawFiltersBody(filtersBody, scale);
                return;
            case HuntsRoute.Detail:
                DrawDetailBackground(area, scale, view.MobId);
                DrawDetailHeader(area, scale, view.MobId, view.WorldId);
                var detailBody = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
                DrawDetailBody(detailBody, scale, view);
                return;
            case HuntsRoute.Signup:
                DrawSignupHeader(area, scale);
                var signupBody = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
                DrawSignupBody(signupBody, scale);
                return;
            case HuntsRoute.MarkNotifications:
                DrawMarkNotificationsHeader(area, scale);
                var markNotificationsBody =
                    new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
                DrawMarkNotificationsBody(markNotificationsBody, scale);
                return;
            default:
                DrawRoot(view.Route, area, scale);
                return;
        }
    }

    private void DrawRoot(HuntsRoute route, Rect area, float scale)
    {
        DrawHeader(area, scale);

        var footerTop = area.Max.Y - BottomTabBar.Height * scale;
        var footerRect = new Rect(new Vector2(area.Min.X, footerTop), area.Max);
        var contentTop = area.Min.Y + AppHeader.Height * scale;
        var content = new Rect(new Vector2(area.Min.X, contentTop), new Vector2(area.Max.X, footerRect.Min.Y));

        switch (route)
        {
            case HuntsRoute.History:
                var historyHeaderTop = content.Min.Y;
                var historyHeader = new Rect(new Vector2(content.Min.X, historyHeaderTop),
                    new Vector2(content.Max.X, historyHeaderTop + ToolbarHeight * scale));
                DrawHistoryHeader(historyHeader, scale);
                var historyBody = new Rect(new Vector2(content.Min.X, historyHeader.Max.Y), content.Max);
                DrawHistory(historyBody, scale);
                break;
            case HuntsRoute.NotificationSettings:
                var notifyHeaderTop = content.Min.Y;
                var notifyHeader = new Rect(new Vector2(content.Min.X, notifyHeaderTop),
                    new Vector2(content.Max.X, notifyHeaderTop + ToolbarHeight * scale));
                DrawNotificationSettingsHeader(notifyHeader, scale);
                var notifyBody = new Rect(new Vector2(content.Min.X, notifyHeader.Max.Y), content.Max);
                DrawNotificationSettings(notifyBody, scale);
                break;
            case HuntsRoute.Guide:
                var guideHeaderTop = content.Min.Y;
                var guideHeader = new Rect(new Vector2(content.Min.X, guideHeaderTop),
                    new Vector2(content.Max.X, guideHeaderTop + ToolbarHeight * scale));
                DrawGuideHeader(guideHeader, scale);
                var guideBody = new Rect(new Vector2(content.Min.X, guideHeader.Max.Y), content.Max);
                DrawGuideBody(guideBody, scale);
                break;
            default:
                var toolbarTop = content.Min.Y;
                var toolbar = new Rect(new Vector2(content.Min.X, toolbarTop),
                    new Vector2(content.Max.X, toolbarTop + ToolbarHeight * scale));
                DrawToolbar(toolbar, scale);
                var listBody = new Rect(new Vector2(content.Min.X, toolbar.Max.Y), content.Max);
                DrawList(listBody, scale);
                break;
        }

        DrawFooter(footerRect, route, scale);
    }

    private void DrawFooter(Rect bar, HuntsRoute route, float scale)
    {
        var authLocked = !hunts.IsAuthenticated;
        footerTabs[0] = new NavTab(FontAwesomeIcon.Clock,
            authLocked ? Loc.T(L.Hunts.HistoryRequiresLoginTooltip) : Loc.T(L.Hunts.HistoryTab),
            Disabled: authLocked);
        footerTabs[1] = new NavTab(FontAwesomeIcon.Book, Loc.T(L.Hunts.ListTab));
        footerTabs[2] = new NavTab(FontAwesomeIcon.Bell,
            authLocked ? Loc.T(L.Hunts.NotificationSettingsRequiresLoginTooltip) : Loc.T(L.Hunts.NotificationSettingsTab),
            Disabled: authLocked);
        footerTabs[3] = new NavTab(FontAwesomeIcon.Question, Loc.T(L.Hunts.GuideTab), AnchorKey: "hunts.guide");

        var active = route switch
        {
            HuntsRoute.History => 0,
            HuntsRoute.NotificationSettings => 2,
            HuntsRoute.Guide => 3,
            _ => 1,
        };

        var tappedTab = footer.Draw(bar, ui, frameTheme, footerTabs, active);
        if (tappedTab < 0)
        {
            return;
        }

        SaveNotificationSettingsIfDirty();
        switch (tappedTab)
        {
            case 0:
                router.Replace(new HuntsView(HuntsRoute.History));
                break;
            case 1:
                router.Replace(new HuntsView(HuntsRoute.List));
                break;
            case 2:
                router.Replace(new HuntsView(HuntsRoute.NotificationSettings));
                break;
            case 3:
                router.Replace(new HuntsView(HuntsRoute.Guide));
                break;
        }
    }

    private void DrawHeader(Rect content, float scale)
    {
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), DisplayName, ui.TitleInk, 1.15f,
            FontWeight.SemiBold);
        DrawAuthStatusIcon(content, scale, rowCenterY);
    }

    private void DrawAuthStatusIcon(Rect content, float scale, float rowCenterY)
    {
        var authenticated = hunts.IsAuthenticated;
        var radius = 15f * scale;
        var center = new Vector2(content.Min.X + radius, rowCenterY);
        var hitMin = center - new Vector2(radius, radius);
        var hitMax = center + new Vector2(radius, radius);
        UiAnchors.Report("hunts.auth", new Rect(hitMin, hitMax));
        var hovered = UiInteract.Hover(hitMin, hitMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = authenticated ? OpenBarColor : frameTheme.Danger;
        AppSkin.Icon(center, FontAwesomeIcon.Wifi.ToIconString(), ink, 1.24f);
        var tooltip = authenticated ? Loc.T(L.Hunts.AuthenticatedTooltip) : Loc.T(L.Hunts.NotAuthenticatedTooltip);
        HoverTooltip.Show(new Rect(hitMin, hitMax), tooltip);
        if (UiInteract.Click(hitMin, hitMax, hovered))
        {
            OpenSignup();
        }

        DrawRealtimeFailureBadge(center, radius, scale, authenticated);
    }

    private void DrawRealtimeFailureBadge(Vector2 iconCenter, float iconRadius, float scale, bool authenticated)
    {
        if (hunts.RealtimeConnected)
        {
            return;
        }

        var badgeRadius = 6f * scale;
        var badgeCenter = iconCenter + new Vector2(iconRadius, iconRadius) * 0.62f;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(badgeCenter, badgeRadius + 1.5f * scale, ImGui.GetColorU32(frameTheme.AppBackground),
            16);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(frameTheme.Danger), 16);
        AppSkin.Icon(badgeCenter, FontAwesomeIcon.Times.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.46f);

        var badgeHitMin = badgeCenter - new Vector2(badgeRadius, badgeRadius) * 1.5f;
        var badgeHitMax = badgeCenter + new Vector2(badgeRadius, badgeRadius) * 1.5f;
        var badgeTooltip = authenticated
            ? Loc.T(L.Hunts.RealtimeReconnectingTooltip)
            : Loc.T(L.Hunts.NotAuthenticatedTooltip);
        HoverTooltip.Show("hunts.realtimeBadge", new Rect(badgeHitMin, badgeHitMax), badgeTooltip);
    }

    private void DrawToolbar(Rect content, float scale)
    {
        var margin = Metrics.Space.Lg * scale;
        var buttonRadius = ToolbarButtonSize * 0.5f * scale;
        var searchBar = new Rect(new Vector2(content.Min.X + margin, content.Min.Y),
            new Vector2(content.Max.X - margin - ToolbarButtonSize * scale - Metrics.Space.Sm * scale, content.Max.Y));
        SearchField.Draw(searchBar, "##hunts.search", Loc.T(L.Hunts.SearchHint), ref searchQuery, ui.Palette);

        var buttonCenter = new Vector2(content.Max.X - margin - buttonRadius, searchBar.Center.Y);
        if (ui.IconButton(buttonCenter, buttonRadius, FontAwesomeIcon.Bars.ToIconString(),
                filter.HasNarrowingFilters ? ui.Accent : ui.MutedInk, AppSkin.Transparent, 1.24f,
                Loc.T(L.Hunts.FiltersTitle)))
        {
            OpenFilters();
        }
    }

    public void Dispose()
    {
        pendingFlagAction.Disarm();
        pendingWorldHopAction.Disarm();
        pendingInstanceAction.Disarm();
    }
}
