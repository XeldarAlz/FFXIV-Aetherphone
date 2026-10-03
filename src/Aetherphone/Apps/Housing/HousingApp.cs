using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp : IPhoneApp
{
    private const int TabCount = 4;
    private const float BottomPad = 24f;
    private const float RefreshFeedbackSeconds = 1.6f;

    private readonly HousingService housing;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ViewRouter<HousingView> router;
    private readonly RouterDraw<HousingView> drawView;
    private readonly Action back;
    private readonly AppSkin ui = new(AppPalettes.Housing);
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly DropdownMenu menu = new();
    private readonly List<DropdownMenu.Item> menuItems = new();
    private readonly List<HousingPlot> visible = new();

    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private HousingTab activeTab;
    private MenuTarget menuTarget = MenuTarget.None;
    private float refreshFeedbackRemaining;
    private float deltaSeconds;
    private CachedText filtersLabel;

    private int cachedRevision = -1;
    private int cachedFilterRevision = -1;
    private int cachedWatchRevision = -1;
    private int cachedWard = -1;
    private uint cachedWorld;
    private uint cachedDistrict;
    private bool cachedSubdivision;
    private bool cachedDivisionSplit;

    private enum MenuTarget : byte
    {
        None,
        Sort,
    }

    public HousingApp(HousingService housing, Configuration configuration, ConfirmService confirm)
    {
        this.housing = housing;
        this.configuration = configuration;
        this.confirm = confirm;
        router = new ViewRouter<HousingView>(HousingView.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public string Id => HousingService.AppId;

    public string DisplayName => Loc.T(L.Apps.Housing);

    public string Glyph => "Ho";

    public int BadgeCount => housing.Watch.FiredReminderCount;

    public bool HasBadge => true;

    public bool BadgeAsDot => true;

    private bool Refreshing => housing.IsRefreshing || refreshFeedbackRemaining > 0f;

    public void OnOpened()
    {
        router.Reset();
        activeTab = HousingTab.Overview;
        CloseOverlays();
        worldSearch = string.Empty;
        housing.SetForeground(true);
        housing.EnsureStarted();
        InvalidateCache();
    }

    public void OnClosed()
    {
        CloseOverlays();
        menu.Close();
        housing.SetForeground(false);
        housing.PersistFilterDefaults();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        ui.Theme = frameTheme;
        deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, frameTheme, scale));
        menu.Gate();
        if (ModalOpen)
        {
            UiInteract.BlockThisFrame();
        }

        AdvanceAnimations(deltaSeconds);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawModalSheets(context.Content);
        DrawMenu(context.Content);
        UpdateTourHold();
    }

    private void DrawView(HousingView view, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        switch (view.Route)
        {
            case HousingRoute.Details:
                DrawDetailsRoute(context, view);
                break;
            case HousingRoute.Settings:
                DrawSettingsRoute(context, view);
                break;
            case HousingRoute.WorldPicker:
                DrawWorldPickerRoute(context, view);
                break;
            default:
                DrawRoot(context);
                break;
        }
    }

    private void DrawRoot(in PhoneContext context)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case HousingTab.Map:
                    DrawMapTab(context.Content);
                    break;
                case HousingTab.Plots:
                    DrawPlotsTab(context);
                    break;
                case HousingTab.Watchlist:
                    DrawWatchlistTab(context);
                    break;
                default:
                    DrawOverviewTab(context);
                    break;
            }
        }

        DrawTabBar(context.Content);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)HousingTab.Overview] = new TabItem(Loc.T(L.Housing.TabOverview), PhoneIcons.Home,
            PhoneIcons.HomeFilled, AnchorKey: "housing.tab.overview");
        tabItems[(int)HousingTab.Map] = new TabItem(Loc.T(L.Housing.Map), PhoneIcons.MapPin, PhoneIcons.PinFilled,
            AnchorKey: "housing.tab.map");
        tabItems[(int)HousingTab.Plots] = new TabItem(Loc.T(L.Housing.TabPlots), PhoneIcons.LayoutList,
            AnchorKey: "housing.tab.plots");
        tabItems[(int)HousingTab.Watchlist] = new TabItem(Loc.T(L.Housing.Watchlist), PhoneIcons.Bookmark,
            PhoneIcons.BookmarkFilled, housing.Watch.Watched.Count, "housing.tab.watchlist");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        SwitchTab((HousingTab)result.Tapped);
    }

    private void SwitchTab(HousingTab tab)
    {
        if (tab == activeTab)
        {
            return;
        }

        activeTab = tab;
        menu.Close();
        if (tab != HousingTab.Map)
        {
            ClosePlotCard();
        }

        UiFeedback.Play(UiSound.Tap);
    }

    private void AdvanceAnimations(float delta)
    {
        StepMap(delta);
        if (refreshFeedbackRemaining > 0f)
        {
            refreshFeedbackRemaining = MathF.Max(0f, refreshFeedbackRemaining - delta);
        }
    }

    private void PushDetails(HousingPlotKey key, string backTitle)
    {
        UiFeedback.Play(UiSound.Tap);
        menu.Close();
        router.Push(new HousingView(HousingRoute.Details, key, backTitle));
    }

    private void PushRoute(HousingRoute route, string backTitle)
    {
        UiFeedback.Play(UiSound.Tap);
        menu.Close();
        router.Push(new HousingView(route, default, backTitle));
    }

    private void CloseOverlays()
    {
        filterSheet.CloseImmediately();
        locationSheet.CloseImmediately();
        reminderSheet.CloseImmediately();
        legendOpen = false;
        ClosePlotCard(true);
    }

    private string FiltersLabel()
    {
        var count = housing.Filters.ActiveCount;
        if (count == 0)
        {
            return Loc.T(L.Housing.Filters);
        }

        return filtersLabel.IsCurrent(count)
            ? filtersLabel.Value
            : filtersLabel.Store(count, Loc.T(L.Housing.FiltersCount, count));
    }

    private string RootTitle()
    {
        return activeTab switch
        {
            HousingTab.Map => Loc.T(L.Housing.Map),
            HousingTab.Plots => Loc.T(L.Housing.TabPlots),
            HousingTab.Watchlist => Loc.T(L.Housing.Watchlist),
            _ => WorldTitle(),
        };
    }

    private string WorldTitle()
    {
        var name = housing.WorldName;
        return name.Length > 0 ? name : DisplayName;
    }

    private void DrawMenu(Rect area)
    {
        if (!menu.Open || menuItems.Count == 0)
        {
            return;
        }

        var picked = menu.Draw(SceneChrome.ScreenFrom(area, frameTheme, UiScale.Current), frameTheme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(menuItems));
        if (picked < 0)
        {
            return;
        }

        if (menuTarget == MenuTarget.Sort)
        {
            configuration.HousingListSort = picked;
            configuration.Save();
            plotsDirty = true;
            UiFeedback.Play(UiSound.Tap);
        }

        menuTarget = MenuTarget.None;
        menuItems.Clear();
    }

    private void OpenSortMenu(Rect anchor)
    {
        menuItems.Clear();
        var labels = SortLabels;
        for (var index = 0; index < labels.Length; index++)
        {
            menuItems.Add(new DropdownMenu.Item(Loc.T(labels[index]), string.Empty, false,
                index == configuration.HousingListSort));
        }

        menuTarget = MenuTarget.Sort;
        menu.Toggle("housing.sort", anchor);
    }

    private static readonly LocString[] SortLabels =
    {
        L.Housing.SortEntries, L.Housing.SortScanned, L.Housing.SortSize, L.Housing.SortPrice, L.Housing.SortWard,
    };

    private void TravelTo(HousingPlotKey key)
    {
        var outcome = HousingTravel.Go(key);
        if (outcome == LifestreamOutcome.Started)
        {
            UiFeedback.Play(UiSound.Success);
            ShellToast.Show(Loc.T(L.Housing.TravelStarted,
                HousingFormat.Place(HousingDistricts.DisplayName(key.DistrictId), key.Ward), key.Plot));
            return;
        }

        if (outcome == LifestreamOutcome.NotInstalled)
        {
            ImGui.SetClipboardText(HousingTravel.Command(key, housing.WorldNameOf(key.WorldId)));
            ShellToast.Show(Loc.T(L.Housing.TravelNeedsLifestream));
            return;
        }

        UiFeedback.Play(UiSound.Blocked);
        ShellToast.Show(Loc.T(HousingTravel.Message(outcome)));
    }

    private void ToggleWatch(HousingPlot plot)
    {
        var nowWatched = housing.Watch.ToggleWatch(plot, housing.WorldNameOf(plot.Key.WorldId));
        if (!nowWatched)
        {
            housing.Watch.CancelReminder(plot.Key);
        }

        UiFeedback.Play(nowWatched ? UiSound.ToggleOn : UiSound.ToggleOff);
        InvalidateCache();
    }

    private void RequestRefresh()
    {
        UiFeedback.Play(UiSound.Refresh);
        refreshFeedbackRemaining = RefreshFeedbackSeconds;
        housing.Refresh(true);
    }

    private void InvalidateCache()
    {
        cachedRevision = -1;
        plotsDirty = true;
    }

    private List<HousingPlot> VisiblePlots()
    {
        var ward = housing.Ward;
        var world = housing.WorldId;
        var district = housing.DistrictId;
        var split = housing.GameMap is { HasSubdivision: true };
        if (cachedRevision == housing.Revision && cachedFilterRevision == housing.Filters.Revision &&
            cachedWatchRevision == housing.Watch.Revision && cachedWard == ward && cachedWorld == world &&
            cachedDistrict == district && cachedSubdivision == showSubdivision && cachedDivisionSplit == split)
        {
            return visible;
        }

        cachedRevision = housing.Revision;
        cachedFilterRevision = housing.Filters.Revision;
        cachedWatchRevision = housing.Watch.Revision;
        cachedWard = ward;
        cachedWorld = world;
        cachedDistrict = district;
        cachedSubdivision = showSubdivision;
        cachedDivisionSplit = split;
        visible.Clear();
        if (housing.Snapshot is not { } snapshot)
        {
            return visible;
        }

        var now = DateTime.UtcNow;
        var thresholds = housing.Thresholds;
        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            if (plot.Key.Ward != ward || (split && plot.IsSubdivision != showSubdivision))
            {
                continue;
            }

            if (housing.Filters.Matches(plot, now, thresholds, housing.Watch.IsWatched(plot.Key)))
            {
                visible.Add(plot);
            }
        }

        return visible;
    }

    private HousingPlot? FindPlot(HousingPlotKey key)
    {
        if (!key.IsValid || housing.Lookup(key.WorldId, key.DistrictId) is not { } snapshot)
        {
            return null;
        }

        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            if (plots[index].Key == key)
            {
                return plots[index];
            }
        }

        return null;
    }

    private HousingDataFreshness FreshnessOf(HousingPlot plot) =>
        housing.Thresholds.Classify(plot.LastSeenUtc, DateTime.UtcNow, housing.ActiveSource);

    private HousingDataFreshness SnapshotFreshness()
    {
        if (housing.Snapshot is not { } snapshot)
        {
            return HousingDataFreshness.Unknown;
        }

        return housing.Thresholds.Classify(snapshot.FetchedUtc, DateTime.UtcNow, snapshot.Source);
    }

    private void OpenOnMap(HousingPlotKey key)
    {
        if (key.WorldId != housing.WorldId)
        {
            PushDetails(key, RootTitle());
            return;
        }

        if (key.DistrictId != housing.DistrictId)
        {
            housing.SelectDistrict(key.DistrictId);
        }

        housing.SelectWard(key.Ward);
        showSubdivision = HousingDistricts.IsSubdivision(key.Plot) && housing.GameMap is { HasSubdivision: true };
        InvalidateCache();
        activeTab = HousingTab.Map;
        UiFeedback.Play(UiSound.Tap);
        SelectPlot(key);
        CenterOnSelected();
    }

    private void UpdateTourHold()
    {
        if (router.Depth == 1 && activeTab == HousingTab.Overview && housing.Snapshot is not null)
        {
            TourHolds.Release(Id);
            return;
        }

        if (router.Depth == 1 && activeTab == HousingTab.Map && VisiblePlots().Count > 0)
        {
            TourHolds.Release(Id);
            return;
        }

        TourHolds.Hold(Id);
    }

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    private static string NoLongerReportedText(ref CachedText cache, HousingWatchRecord record, DateTime now)
    {
        var lastSeen = FromUnix(record.LastSeenUnix);
        var ageMinutes = lastSeen == default ? -1L : (long)Math.Max(0d, (now - lastSeen).TotalMinutes);
        var key = ((long)record.Key.GetHashCode() << 32) ^ ageMinutes;
        return cache.IsCurrent(key)
            ? cache.Value
            : cache.Store(key, Loc.T(L.Housing.NoLongerReported, HousingFormat.ScanAgeShort(lastSeen, now)));
    }

    private static DateTime FromUnix(long unixSeconds) =>
        unixSeconds <= 0L ? default : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

    private static DateTime? OptionalUnix(long unixSeconds) =>
        unixSeconds <= 0L ? null : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

    public void Dispose()
    {
    }
}
