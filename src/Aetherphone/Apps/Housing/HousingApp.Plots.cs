using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal readonly record struct HousingPlotRow(HousingPlot Plot, string Title, string Subtitle, string Entries);

internal sealed partial class HousingApp
{
    private const int ScopeCount = 6;

    private readonly List<HousingPlotRow> plotRows = new();
    private readonly List<HousingPlot> plotBuffer = new();
    private readonly NavBarButton[] plotButtons = new NavBarButton[2];
    private readonly ChipRail scopeRail = new();
    private readonly string[] scopeLabels = new string[ScopeCount];
    private readonly bool[] scopeActive = new bool[ScopeCount];
    private bool plotsDirty = true;
    private int plotsRevision = -1;
    private int plotsFilterRevision = -1;
    private int plotsWatchRevision = -1;
    private uint plotsWorld;
    private int plotsScope;
    private int plotsSort = -1;
    private int plotsCulture;
    private CachedText plotsSummary;

    private void DrawPlotsTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var scale = UiScale.Current;
        var rows = PlotRows();
        if (!housing.HasWorldSelected)
        {
            if (HousingArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.Globe,
                    Loc.T(L.Housing.NoWorldTitle), Loc.T(L.Housing.NoWorldHint), Loc.T(L.Housing.ChooseWorld),
                    scale))
            {
                OpenWorldPicker();
            }
        }
        else
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                DrawScopeRail();
                var cursorY = ImGui.GetCursorScreenPos().Y + Metrics.Space.Sm * scale;
                if (rows.Count == 0)
                {
                    cursorY = DrawPlotsEmpty(drawList, new Vector2(origin.X, cursorY), width, scale);
                }
                else
                {
                    cursorY = DrawPlotRows(drawList, new Vector2(origin.X, cursorY), width, rows, scale);
                }

                ReserveTo(origin, width, cursorY + BottomPad * scale);
            }
        }

        plotButtons[0] = new NavBarButton(PhoneIcons.ArrowsSort, Loc.T(L.Housing.SortLabel));
        plotButtons[1] = new NavBarButton(PhoneIcons.AdjustmentsHorizontal, FiltersLabel());
        var sortRect = AppHeader.LargeTitleButtonRect(navBar, 0, 2);
        UiAnchors.Report("housing.plots.filters", AppHeader.LargeTitleButtonRect(navBar, 1, 2));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "housing.nav.plots", Loc.T(L.Housing.TabPlots),
            NavBarStyle.From(ui), plotButtons);
        if (pressed == 0)
        {
            OpenSortMenu(sortRect);
        }
        else if (pressed == 1)
        {
            OpenFilterSheet();
        }
    }

    private void DrawScopeRail()
    {
        var districts = HousingDistricts.All;
        scopeLabels[0] = Loc.T(L.Housing.AllDistricts);
        scopeActive[0] = plotsScope == 0;
        for (var index = 0; index < districts.Count && index + 1 < ScopeCount; index++)
        {
            scopeLabels[index + 1] = HousingDistricts.ShortDisplayName(districts[index].Id);
            scopeActive[index + 1] = plotsScope == (int)districts[index].Id;
        }

        var tapped = scopeRail.Draw(ui, scopeLabels, scopeActive, "housing.plots.scope");
        if (tapped < 0)
        {
            return;
        }

        var scope = tapped == 0 ? 0 : (int)districts[tapped - 1].Id;
        if (scope == plotsScope)
        {
            return;
        }

        plotsScope = scope;
        plotsDirty = true;
        UiFeedback.Play(UiSound.Tap);
    }

    private List<HousingPlotRow> PlotRows()
    {
        var cultureKey = Loc.Culture.GetHashCode();
        if (!plotsDirty && plotsRevision == housing.Revision && plotsFilterRevision == housing.Filters.Revision &&
            plotsWatchRevision == housing.Watch.Revision && plotsWorld == housing.WorldId &&
            plotsSort == configuration.HousingListSort && plotsCulture == cultureKey)
        {
            return plotRows;
        }

        plotsDirty = false;
        plotsRevision = housing.Revision;
        plotsFilterRevision = housing.Filters.Revision;
        plotsWatchRevision = housing.Watch.Revision;
        plotsWorld = housing.WorldId;
        plotsSort = configuration.HousingListSort;
        plotsCulture = cultureKey;
        plotBuffer.Clear();
        plotRows.Clear();
        var now = DateTime.UtcNow;
        var thresholds = housing.Thresholds;
        var districts = HousingDistricts.All;
        for (var districtIndex = 0; districtIndex < districts.Count; districtIndex++)
        {
            var districtId = districts[districtIndex].Id;
            if (plotsScope != 0 && plotsScope != (int)districtId)
            {
                continue;
            }

            if (housing.Lookup(plotsWorld, districtId) is not { } snapshot)
            {
                continue;
            }

            var plots = snapshot.Plots;
            for (var index = 0; index < plots.Count; index++)
            {
                if (housing.Filters.Matches(plots[index], now, thresholds, housing.Watch.IsWatched(plots[index].Key)))
                {
                    plotBuffer.Add(plots[index]);
                }
            }
        }

        ApplySort(plotBuffer, plotsSort);
        for (var index = 0; index < plotBuffer.Count; index++)
        {
            var plot = plotBuffer[index];
            var subtitle = string.Concat(HousingDistricts.ShortDisplayName(plot.Key.DistrictId), " · ",
                HousingFormat.SizeLabel(plot.Size), " · ", HousingFormat.Price(plot.Price));
            plotRows.Add(new HousingPlotRow(plot, Loc.T(L.Housing.PlotAndWard, plot.Key.Plot, plot.Key.Ward),
                subtitle, plot.Entries is { } entries ? HousingText.Count(entries) : "--"));
        }

        return plotRows;
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
                    return compare != 0 ? compare : CompareByPlace(first, second);
                });
                break;
            case 1:
                plots.Sort(static (first, second) =>
                {
                    var compare = second.LastSeenUtc.CompareTo(first.LastSeenUtc);
                    return compare != 0 ? compare : CompareByPlace(first, second);
                });
                break;
            case 2:
                plots.Sort(static (first, second) =>
                {
                    var compare = ((int)second.Size).CompareTo((int)first.Size);
                    return compare != 0 ? compare : CompareByPlace(first, second);
                });
                break;
            case 3:
                plots.Sort(static (first, second) =>
                {
                    var left = first.Price <= 0L ? long.MaxValue : first.Price;
                    var right = second.Price <= 0L ? long.MaxValue : second.Price;
                    var compare = left.CompareTo(right);
                    return compare != 0 ? compare : CompareByPlace(first, second);
                });
                break;
            default:
                plots.Sort(CompareByPlace);
                break;
        }
    }

    private static int CompareByPlace(HousingPlot first, HousingPlot second)
    {
        var district = first.Key.DistrictId.CompareTo(second.Key.DistrictId);
        return district != 0 ? district : HousingPlotOrder.ByWardThenPlot(first, second);
    }

    private float DrawPlotRows(ImDrawListPtr drawList, Vector2 origin, float width, List<HousingPlotRow> rows,
        float scale)
    {
        var count = rows.Count;
        var summary = plotsSummary.IsCurrent(count)
            ? plotsSummary.Value
            : plotsSummary.Store(count, Loc.Plural(L.Housing.OpenPlots, count));
        Typography.Draw(drawList, origin, Typography.FitText(summary, width, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        var top = origin.Y + Typography.LineHeight(TextStyles.Footnote) + Metrics.Space.Sm * scale;
        var rowHeight = HousingArt.RowHeight * scale;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + rowHeight * count);
        HousingArt.Card(drawList, ui, min, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var marker = HousingArt.TileSize * scale;
        for (var index = 0; index < count; index++)
        {
            var rowTop = top + rowHeight * index;
            var row = new Rect(new Vector2(min.X, rowTop), new Vector2(max.X, rowTop + rowHeight));
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            if (index > 0)
            {
                HousingArt.Hairline(drawList, ui, row.Min.X + pad + marker + HousingArt.TextGap * scale,
                    row.Max.X - pad, rowTop);
            }

            if (DrawPlotRow(drawList, row, rows[index], scale))
            {
                OpenPlot(rows[index].Plot.Key);
            }
        }

        return max.Y;
    }

    private bool DrawPlotRow(ImDrawListPtr drawList, Rect row, in HousingPlotRow entry, float scale)
    {
        var hovered = HousingArt.RowInteraction(drawList, ui, row, scale);
        var plot = entry.Plot;
        var pad = Metrics.Space.Lg * scale;
        var marker = HousingArt.TileSize * scale;
        var markerCenter = new Vector2(row.Min.X + pad + marker * 0.5f, row.Center.Y);
        var style = new HousingMarkerStyle(plot.Size, plot.Phase, housing.Watch.IsWatched(plot.Key), false,
            FreshnessOf(plot) == HousingDataFreshness.Stale, false);
        HousingMarkers.Draw(drawList, markerCenter, style, ui.Accent, scale, 0f);
        var trailing = HousingArt.TrailingValue(drawList, row.Max.X - pad, row.Center.Y, entry.Entries,
            Loc.T(L.Housing.EntriesCaption), ui.TitleInk, ui.MutedInk, scale);
        var textLeft = markerCenter.X + marker * 0.5f + HousingArt.TextGap * scale;
        HousingArt.Labels(drawList, textLeft, row.Max.X - pad - trailing - HousingArt.TextGap * scale, row.Center.Y,
            entry.Title, entry.Subtitle, ui.TitleInk, ui.MutedInk, scale);
        return hovered && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void OpenPlot(HousingPlotKey key)
    {
        if (housing.GameMaps.For(key.DistrictId) is null)
        {
            PushDetails(key, RootTitle());
            return;
        }

        OpenOnMap(key);
    }

    private float DrawPlotsEmpty(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var height = MathF.Max(ScrollLayout.StableContentWidth(), 320f * scale);
        var body = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var narrowing = housing.Filters.HasNarrowingFilters;
        var loading = housing.Snapshot is null && housing.IsRefreshing;
        if (loading)
        {
            LoadingPulse.Draw(body.Center, 18f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Housing.LoadingFirst));
            return body.Max.Y;
        }

        if (HousingArt.StateScreen(drawList, ui, body, narrowing ? FontAwesomeIcon.Filter : FontAwesomeIcon.Home,
                narrowing ? Loc.T(L.Housing.NoFilterMatches) : Loc.T(L.Housing.NoScans),
                narrowing ? string.Empty : Loc.T(L.Housing.NoScansHint),
                narrowing ? Loc.T(L.Housing.ClearFilters) : string.Empty, scale))
        {
            ClearFilters();
        }

        return body.Max.Y;
    }
}
