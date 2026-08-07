using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp : IPhoneApp
{
    private const float TopBarHeight = AppHeader.Height;
    private const float ContextBarHeight = 40f;
    private const float PhaseBarHeight = 26f;
    private const float FooterHeight = 34f;
    private const float SheetSmoothTime = 0.16f;
    private const float MapSmoothTime = 0.11f;
    private const float ToastSeconds = 3.4f;

    private readonly HousingService housing;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ViewRouter<HousingView> router;
    private readonly RouterDraw<HousingView> drawView;
    private readonly AppSkin ui = new(AppPalettes.Housing);
    private readonly DropdownMenu menu = new();
    private readonly List<HousingPlot> visible = new();
    private readonly List<HousingPlot> sorted = new();
    private readonly List<DropdownMenu.Item> menuItems = new();

    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private Rect frameScreen;
    private MenuTarget menuTarget = MenuTarget.None;

    private Spring zoomSpring = new(1f);
    private Spring panXSpring;
    private Spring panYSpring;
    private float zoomTarget = 1f;
    private Vector2 panTarget;
    private HousingPlotKey selectedPlot;
    private bool dragging;
    private float dragTravel;

    private bool showSubdivision;

    private Spring sheetSpring;
    private Spring filterSpring;
    private bool sheetOpen;
    private bool filtersOpen;
    private bool wardPickerOpen;
    private bool legendOpen;
    private bool reminderPickerOpen;
    private int reminderChoice = 2;
    private string toast = string.Empty;
    private float toastRemaining;
    private string worldSearch = string.Empty;
    private bool refreshFeedback;
    private float refreshFeedbackRemaining;

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
        District,
        Sort,
    }

    private enum HousingOverlay : byte
    {
        None,
        WardPicker,
        Filters,
        ReminderPicker,
    }

    private bool OverlayActive => wardPickerOpen || filtersOpen || reminderPickerOpen;

    private void ShowOverlay(HousingOverlay overlay)
    {
        wardPickerOpen = overlay == HousingOverlay.WardPicker;
        filtersOpen = overlay == HousingOverlay.Filters;
        reminderPickerOpen = overlay == HousingOverlay.ReminderPicker;
        if (filtersOpen)
        {
            ResetFilterRails();
        }
        else if (reminderPickerOpen)
        {
            reminderRail.Reset();
        }
    }

    public HousingApp(HousingService housing, Configuration configuration, ConfirmService confirm)
    {
        this.housing = housing;
        this.configuration = configuration;
        this.confirm = confirm;
        router = new ViewRouter<HousingView>(HousingView.Root);
        drawView = DrawView;
    }

    public string Id => HousingService.AppId;

    public string DisplayName => Loc.T(L.Apps.Housing);

    public string Glyph => "Ho";

    public int BadgeCount => housing.Watch.FiredReminderCount;

    public bool BadgeAsDot => true;

    public void OnOpened()
    {
        router.Reset();
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
        var scale = UiScale.Current;
        frameScreen = SceneChrome.ScreenFrom(context.Content, frameTheme, scale);
        ui.Backdrop(frameScreen);
        menu.Gate();
        if (OverlayActive)
        {
            UiInteract.BlockThisFrame();
        }

        AdvanceAnimations(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawMenu();
        DrawToast(context.Content, scale);
    }

    private void DrawView(HousingView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Route)
        {
            case HousingRoute.List:
                DrawListRoute(area);
                break;
            case HousingRoute.Watchlist:
                DrawWatchlistRoute(area);
                break;
            case HousingRoute.Details:
                DrawDetailsRoute(area, view.Plot);
                break;
            case HousingRoute.Settings:
                DrawSettingsRoute(area);
                break;
            case HousingRoute.WorldPicker:
                DrawWorldPickerRoute(area);
                break;
            default:
                DrawMapRoute(area);
                break;
        }
    }

    private void AdvanceAnimations(float deltaSeconds)
    {
        zoomSpring.Step(zoomTarget, MapSmoothTime, deltaSeconds);
        panXSpring.Step(panTarget.X, MapSmoothTime, deltaSeconds);
        panYSpring.Step(panTarget.Y, MapSmoothTime, deltaSeconds);
        sheetSpring.Step(sheetOpen ? 1f : 0f, SheetSmoothTime, deltaSeconds);
        filterSpring.Step(filtersOpen ? 1f : 0f, SheetSmoothTime, deltaSeconds);
        if (toastRemaining > 0f)
        {
            toastRemaining = MathF.Max(0f, toastRemaining - deltaSeconds);
        }

        if (refreshFeedbackRemaining > 0f)
        {
            refreshFeedbackRemaining = MathF.Max(0f, refreshFeedbackRemaining - deltaSeconds);
            if (refreshFeedbackRemaining <= 0f)
            {
                refreshFeedback = false;
            }
        }
    }

    private void Push(HousingRoute route, HousingPlotKey plot = default)
    {
        CloseTransientOverlays();
        router.Push(new HousingView(route, plot));
    }

    private void Back()
    {
        if (menu.Open)
        {
            menu.Close();
            return;
        }

        if (reminderPickerOpen)
        {
            reminderPickerOpen = false;
            return;
        }

        if (filtersOpen)
        {
            filtersOpen = false;
            return;
        }

        if (wardPickerOpen)
        {
            wardPickerOpen = false;
            return;
        }

        if (router.Depth > 1)
        {
            router.Pop();
            return;
        }

        if (sheetOpen)
        {
            sheetOpen = false;
            return;
        }

        frameNavigation.Back();
    }

    private void CloseOverlays()
    {
        CloseTransientOverlays();
        sheetOpen = false;
        selectedPlot = default;
        sheetSpring.SnapTo(0f);
    }

    private void CloseTransientOverlays()
    {
        filtersOpen = false;
        wardPickerOpen = false;
        legendOpen = false;
        reminderPickerOpen = false;
        filterSpring.SnapTo(0f);
    }

    private Rect DrawSubHeader(Rect area, string headerId, string title, string? actionLabel = null,
        Action? onAction = null)
    {
        var scale = UiScale.Current;
        var reserve = 12f * scale;
        if (actionLabel is not null)
        {
            reserve += Typography.Measure(actionLabel, TextStyles.SubheadlineEmphasized).X + 30f * scale;
        }

        AppHeader.DrawTitleWithReserve(area, headerId, title, reserve, ui.TitleInk, scale);
        var rowCenterY = area.Min.Y + TopBarHeight * scale * 0.5f;
        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 44f * scale, area.Min.Y + TopBarHeight * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        if (BackButton.Draw("housing.back", new Vector2(area.Min.X + 15f * scale, rowCenterY), 15f * scale, ui.Accent,
                hovered, scale))
        {
            Back();
        }

        if (actionLabel is not null && onAction is not null)
        {
            var height = 26f * scale;
            var width = HousingChrome.MeasurePill(actionLabel, height);
            var max = new Vector2(area.Max.X - 12f * scale, rowCenterY + height * 0.5f);
            var rect = new Rect(new Vector2(max.X - width, max.Y - height), max);
            if (HousingChrome.PillButton(rect, actionLabel, false, ui, false))
            {
                onAction();
            }
        }

        return new Rect(new Vector2(area.Min.X, area.Min.Y + TopBarHeight * scale), area.Max);
    }

    private void DrawMenu()
    {
        if (!menu.Open || menuItems.Count == 0)
        {
            return;
        }

        var picked = menu.Draw(frameScreen, frameTheme, System.Runtime.InteropServices.CollectionsMarshal
            .AsSpan(menuItems));
        if (picked < 0)
        {
            return;
        }

        switch (menuTarget)
        {
            case MenuTarget.District:
                if (picked < HousingDistricts.All.Count)
                {
                    housing.SelectDistrict(HousingDistricts.All[picked].Id);
                    ResetMapView();
                    sheetOpen = false;
                    selectedPlot = default;
                    InvalidateCache();
                }

                break;
            case MenuTarget.Sort:
                configuration.HousingListSort = picked;
                configuration.Save();
                break;
        }

        menuTarget = MenuTarget.None;
        menuItems.Clear();
    }

    private void OpenDistrictMenu(Rect anchor)
    {
        menuItems.Clear();
        var districts = HousingDistricts.All;
        for (var index = 0; index < districts.Count; index++)
        {
            menuItems.Add(new DropdownMenu.Item(HousingDistricts.DisplayName(districts[index].Id), string.Empty, false,
                districts[index].Id == housing.DistrictId));
        }

        menuTarget = MenuTarget.District;
        menu.Toggle("housing.district", anchor);
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

    private void ShowToast(string message)
    {
        toast = message;
        toastRemaining = ToastSeconds;
    }

    private void DrawToast(Rect area, float scale)
    {
        if (toastRemaining <= 0f || toast.Length == 0)
        {
            return;
        }

        var alpha = MathF.Min(1f, toastRemaining / 0.4f);
        var drawList = ImGui.GetForegroundDrawList();
        var maxWidth = area.Width - 40f * scale;
        var textSize = Typography.MeasureWrappedBlock(toast, TextStyles.Subheadline, maxWidth - 28f * scale);
        var width = MathF.Min(maxWidth, textSize.X + 28f * scale);
        var height = textSize.Y + 22f * scale;
        var center = new Vector2(area.Center.X, area.Max.Y - height * 0.5f - 26f * scale);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var rounding = Metrics.Radius.Md * scale;
        Elevation.Floating(drawList, min, max, rounding, scale, alpha);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(new Vector4(0.10f, 0.14f, 0.12f, 0.97f * alpha)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.45f * alpha)), Metrics.Stroke.Hairline);
        Typography.DrawWrappedCentered(drawList, center, toast, Palette.WithAlpha(ui.TitleInk, alpha),
            TextStyles.Subheadline, maxWidth - 28f * scale);
    }

    private static readonly LocString[] SortLabels =
    {
        L.Housing.SortEntries, L.Housing.SortScanned, L.Housing.SortSize, L.Housing.SortPrice, L.Housing.SortWard,
    };

    private void InvalidateCache() => cachedRevision = -1;

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

    private List<HousingPlot> FilteredDistrictPlots()
    {
        sorted.Clear();
        if (housing.Snapshot is not { } snapshot)
        {
            return sorted;
        }

        var now = DateTime.UtcNow;
        var thresholds = housing.Thresholds;
        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            if (housing.Filters.Matches(plot, now, thresholds, housing.Watch.IsWatched(plot.Key)))
            {
                sorted.Add(plot);
            }
        }

        ApplySort(sorted, configuration.HousingListSort);
        return sorted;
    }

    private static void ApplySort(List<HousingPlot> plots, int mode)
    {
        switch (mode)
        {
            case 0:
                plots.Sort(static (first, second) =>
                {
                    var left = first.Entries ?? int.MaxValue;
                    var right = second.Entries ?? int.MaxValue;
                    var compare = left.CompareTo(right);
                    return compare != 0 ? compare : HousingPlotOrder.ByWardThenPlot(first, second);
                });
                break;
            case 1:
                plots.Sort(static (first, second) =>
                {
                    var compare = second.LastSeenUtc.CompareTo(first.LastSeenUtc);
                    return compare != 0 ? compare : HousingPlotOrder.ByWardThenPlot(first, second);
                });
                break;
            case 2:
                plots.Sort(static (first, second) =>
                {
                    var compare = ((int)second.Size).CompareTo((int)first.Size);
                    return compare != 0 ? compare : HousingPlotOrder.ByWardThenPlot(first, second);
                });
                break;
            case 3:
                plots.Sort(static (first, second) =>
                {
                    var left = first.Price <= 0L ? long.MaxValue : first.Price;
                    var right = second.Price <= 0L ? long.MaxValue : second.Price;
                    var compare = left.CompareTo(right);
                    return compare != 0 ? compare : HousingPlotOrder.ByWardThenPlot(first, second);
                });
                break;
            default:
                plots.Sort(HousingPlotOrder.ByWardThenPlot);
                break;
        }
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
        housing.Thresholds.Classify(plot.LastSeenUtc, DateTime.UtcNow,
            housing.ActiveSource);

    private bool IsStale(HousingPlot plot) => FreshnessOf(plot) == HousingDataFreshness.Stale;

    private void RequestRefresh()
    {
        refreshFeedback = true;
        refreshFeedbackRemaining = 1.6f;
        housing.Refresh(true);
    }

    public void Dispose()
    {
    }
}
