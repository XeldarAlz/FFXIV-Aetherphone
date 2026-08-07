using Aetherphone.Core;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const float ListRowHeight = 64f;

    private void DrawListRoute(Rect area)
    {
        var scale = UiScale.Current;
        var sortLabel = Loc.T(SortLabels[Math.Clamp(configuration.HousingListSort, 0, SortLabels.Length - 1)]);
        var body = DrawSubHeader(area, "housing.header.list", Loc.T(L.Housing.List));
        var barHeight = 32f * scale;
        var pad = 16f * scale;
        var bar = new Rect(new Vector2(body.Min.X, body.Min.Y + 4f * scale),
            new Vector2(body.Max.X, body.Min.Y + 4f * scale + barHeight));
        var drawList = ImGui.GetWindowDrawList();
        var context = string.Concat(housing.WorldName, " · ", housing.DistrictName);
        var sortWidth = HousingChrome.MeasurePill(sortLabel, 24f * scale);
        var sortRect = new Rect(new Vector2(bar.Max.X - pad - sortWidth, bar.Center.Y - 12f * scale),
            new Vector2(bar.Max.X - pad, bar.Center.Y + 12f * scale));
        Typography.Draw(drawList,
            new Vector2(bar.Min.X + pad, bar.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(context, sortRect.Min.X - bar.Min.X - pad * 2f, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (HousingChrome.PillButton(sortRect, sortLabel, false, ui))
        {
            OpenSortMenu(sortRect);
        }

        var plots = FilteredDistrictPlots();
        var listBody = new Rect(new Vector2(body.Min.X, bar.Max.Y + 2f * scale), body.Max);
        if (plots.Count == 0)
        {
            EmptyState.Draw(listBody, ui, FontAwesomeIcon.Home,
                housing.Filters.HasNarrowingFilters
                    ? Loc.T(L.Housing.NoFilterMatches)
                    : Loc.T(L.Housing.NoScans),
                housing.Filters.HasNarrowingFilters ? string.Empty : Loc.T(L.Housing.NoScansHint));
            if (!housing.Filters.HasNarrowingFilters)
            {
                return;
            }

            var buttonWidth = 180f * scale;
            var clearRect = new Rect(
                new Vector2(listBody.Center.X - buttonWidth * 0.5f, listBody.Center.Y + 60f * scale),
                new Vector2(listBody.Center.X + buttonWidth * 0.5f, listBody.Center.Y + 92f * scale));
            if (HousingChrome.PillButton(clearRect, Loc.T(L.Housing.ClearFilters), true, ui))
            {
                housing.Filters.Reset();
                housing.PersistFilterDefaults();
                InvalidateCache();
            }

            return;
        }

        using (AppSurface.Begin(listBody))
        {
            var now = DateTime.UtcNow;
            var card = GroupCard.Begin(frameTheme, plots.Count, ListRowHeight);
            for (var index = 0; index < plots.Count; index++)
            {
                if (DrawListRow(card.NextRow(), plots[index], now, scale))
                {
                    OpenFromList(plots[index]);
                }
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void OpenFromList(HousingPlot plot)
    {
        if (plot.Key.WorldId != housing.WorldId || plot.Key.DistrictId != housing.DistrictId)
        {
            Push(HousingRoute.Details, plot.Key);
            return;
        }

        if (plot.Key.Ward != housing.Ward)
        {
            housing.SelectWard(plot.Key.Ward);
            InvalidateCache();
        }

        selectedPlot = plot.Key;
        sheetOpen = true;
        router.Pop();
        CenterOnSelected();
    }

    private bool DrawListRow(Rect row, HousingPlot plot, DateTime now, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X - 8f * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X + 8f * scale, row.Max.Y - 2f * scale), Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(frameTheme.Accent, 0.14f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var markerCenter = new Vector2(row.Min.X + 15f * scale, row.Center.Y);
        var freshness = FreshnessOf(plot);
        var style = new HousingMarkerStyle(plot.Size, plot.Phase, housing.Watch.IsWatched(plot.Key), false,
            freshness == HousingDataFreshness.Stale, false);
        HousingMarkers.Draw(drawList, markerCenter, style, frameTheme.Accent, scale * 0.86f, 0f);
        var textLeft = markerCenter.X + 22f * scale;
        var textRight = row.Max.X - 14f * scale;
        var titleStyle = TextStyles.BodyEmphasized;
        var title = HousingFormat.PlotTitle(plot);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 9f * scale),
            Typography.FitText(title, textRight - textLeft - 60f * scale, titleStyle), frameTheme.TextStrong,
            titleStyle);
        var placeLine = string.Concat(HousingFormat.WardLabel(plot.Key.Ward), " · ",
            HousingFormat.PhaseLabel(plot.Phase), " · ", HousingFormat.PhaseCountdown(plot, now));
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 27f * scale),
            Typography.FitText(placeLine, textRight - textLeft, TextStyles.Footnote), frameTheme.TextMuted,
            TextStyles.Footnote);
        var factLine = string.Concat(Loc.T(L.Housing.EntriesLabel), ": ", HousingFormat.Entries(plot.Entries), " · ",
            HousingFormat.Price(plot.Price));
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 43f * scale),
            Typography.FitText(factLine, textRight - textLeft - 44f * scale, TextStyles.Caption1),
            frameTheme.TextMuted, TextStyles.Caption1);
        var ageText = HousingFormat.ScanAgeShort(plot.LastSeenUtc, now);
        var ageSize = Typography.Measure(ageText, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textRight - ageSize.X, row.Min.Y + 43f * scale), ageText,
            freshness == HousingDataFreshness.Stale ? AppPalettes.HousingResults : frameTheme.TextMuted,
            TextStyles.Caption1);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }
}
