using Aetherphone.Core;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const int FilterSectionCount = 6;
    private const float FilterSegmentHeight = 34f;

    private static readonly int[] EntryCaps = [0, 3, 10, 25];

    private readonly ChipRail sizeRail = new();
    private readonly ChipRail phaseRail = new();
    private readonly ChipRail eligibilityRail = new();
    private readonly ChipRail divisionRail = new();
    private readonly ChipRail dataRail = new();
    private readonly ChipRail entriesRail = new();
    private readonly string[] chipLabels = new string[EntryCaps.Length];
    private readonly bool[] chipActive = new bool[EntryCaps.Length];
    private CachedText matchingText;

    private void OpenFilterSheet()
    {
        legendOpen = false;
        menu.Close();
        sizeRail.Reset();
        phaseRail.Reset();
        eligibilityRail.Reset();
        divisionRail.Reset();
        dataRail.Reset();
        entriesRail.Reset();
        UiFeedback.Play(UiSound.Tap);
        filterSheet.Open();
    }

    private static float FilterSectionHeight(float scale) =>
        Typography.LineHeight(TextStyles.FootnoteEmphasized) + (ChipRail.RowHeight + Metrics.Space.Sm * 2f) * scale;

    private static float FilterSheetHeight(float scale) =>
        SheetMetrics.GrabberZone * scale + HousingArt.SheetHeaderHeight * scale +
        Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Md * scale + FilterSegmentHeight * scale +
        Metrics.Space.Lg * scale + FilterSectionCount * FilterSectionHeight(scale) + SheetBottomPad * scale;

    private void DrawFilterSheet(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float scale)
    {
        var detents = SheetDetents.Fitted(MathF.Min(FilterSheetHeight(scale),
            screen.Height * SheetMetrics.LargeFraction));
        var frame = filterSheet.Begin(drawList, screen, theme, detents, SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var content = frame.Content;
        if (HousingArt.SheetHeader(drawList, content, Loc.T(L.Housing.Filters), Loc.T(L.Housing.Done), ui.TitleInk,
                ui.Accent, scale) && frame.Interactive)
        {
            filterSheet.Close();
        }

        var filters = housing.Filters;
        var pad = SheetPad * scale;
        var left = content.Min.X + pad;
        var right = content.Max.X - pad;
        var y = content.Min.Y + HousingArt.SheetHeaderHeight * scale;
        DrawFilterSummary(drawList, left, right, y, frame.Interactive, scale);
        y += Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Md * scale;
        var segment = new Rect(new Vector2(left, y), new Vector2(right, y + FilterSegmentHeight * scale));
        var picked = HousingChrome.Segment(segment, Loc.T(L.Housing.ShowAvailableOnly), Loc.T(L.Housing.ShowAllPlots),
            filters.ShowAllPlots ? 1 : 0, ui, true);
        if (frame.Interactive && picked == 1 != filters.ShowAllPlots)
        {
            filters.ShowAllPlots = picked == 1;
            housing.PersistFilterDefaults();
            InvalidateCache();
        }

        y = segment.Max.Y + Metrics.Space.Lg * scale;
        y = DrawSizeFilters(drawList, filters, left, right, y, scale);
        y = DrawPhaseFilters(drawList, filters, left, right, y, scale);
        y = DrawEligibilityFilters(drawList, filters, left, right, y, scale);
        y = DrawDivisionFilters(drawList, filters, left, right, y, scale);
        y = DrawDataFilters(drawList, filters, left, right, y, scale);
        DrawEntryFilters(drawList, filters, left, right, y, scale);
        filterSheet.End(in frame);
    }

    private void DrawFilterSummary(ImDrawListPtr drawList, float left, float right, float top, bool interactive,
        float scale)
    {
        var count = activeTab == HousingTab.Map ? VisiblePlots().Count : PlotRows().Count;
        var text = matchingText.IsCurrent(count)
            ? matchingText.Value
            : matchingText.Store(count, Loc.T(L.Housing.MatchingPlots, count));
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var resetLabel = Loc.T(L.Housing.ClearFilters);
        var resetSize = Typography.Measure(resetLabel, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(text, MathF.Max(1f, right - left - resetSize.X - HousingArt.TextGap * scale),
                TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        var enabled = interactive && housing.Filters.HasNarrowingFilters;
        var hitMin = new Vector2(right - resetSize.X - HousingArt.TextGap * scale,
            top + lineHeight * 0.5f - Metrics.Size.TapTarget * scale * 0.5f);
        var hitMax = new Vector2(right, hitMin.Y + Metrics.Size.TapTarget * scale);
        var hovered = enabled && UiInteract.HoverWindowOnly(hitMin, hitMax);
        Typography.Draw(drawList, new Vector2(right - resetSize.X, top), resetLabel,
            enabled ? hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent : ui.MutedInk, TextStyles.Subheadline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (enabled && UiInteract.Click(hitMin, hitMax, hovered))
        {
            ClearFilters();
        }
    }

    private float DrawSizeFilters(ImDrawListPtr drawList, HousingFilterState filters, float left, float right,
        float y, float scale)
    {
        chipLabels[0] = Loc.T(L.Housing.SizeSmall);
        chipActive[0] = filters.Small;
        chipLabels[1] = Loc.T(L.Housing.SizeMedium);
        chipActive[1] = filters.Medium;
        chipLabels[2] = Loc.T(L.Housing.SizeLarge);
        chipActive[2] = filters.Large;
        y = DrawChipSection(drawList, sizeRail, left, right, y, L.Housing.FilterSizes, 3, scale, out var tapped);
        switch (tapped)
        {
            case 0:
                filters.Small = !filters.Small;
                break;
            case 1:
                filters.Medium = !filters.Medium;
                break;
            case 2:
                filters.Large = !filters.Large;
                break;
        }

        if (tapped >= 0)
        {
            housing.PersistFilterDefaults();
        }

        return y;
    }

    private float DrawPhaseFilters(ImDrawListPtr drawList, HousingFilterState filters, float left, float right,
        float y, float scale)
    {
        chipLabels[0] = Loc.T(L.Housing.PhaseEntry);
        chipActive[0] = filters.PhaseEntry;
        chipLabels[1] = Loc.T(L.Housing.PhaseResults);
        chipActive[1] = filters.PhaseResults;
        chipLabels[2] = Loc.T(L.Housing.FilterOtherPhases);
        chipActive[2] = filters.PhaseOther;
        y = DrawChipSection(drawList, phaseRail, left, right, y, L.Housing.FilterPhase, 3, scale, out var tapped);
        switch (tapped)
        {
            case 0:
                filters.PhaseEntry = !filters.PhaseEntry;
                break;
            case 1:
                filters.PhaseResults = !filters.PhaseResults;
                break;
            case 2:
                filters.PhaseOther = !filters.PhaseOther;
                break;
        }

        return y;
    }

    private float DrawEligibilityFilters(ImDrawListPtr drawList, HousingFilterState filters, float left,
        float right, float y, float scale)
    {
        chipLabels[0] = Loc.T(L.Housing.EligibilityPrivate);
        chipActive[0] = filters.PrivateBuyers;
        chipLabels[1] = Loc.T(L.Housing.EligibilityFreeCompany);
        chipActive[1] = filters.FreeCompany;
        y = DrawChipSection(drawList, eligibilityRail, left, right, y, L.Housing.FilterEligibility, 2, scale,
            out var tapped);
        switch (tapped)
        {
            case 0:
                filters.PrivateBuyers = !filters.PrivateBuyers;
                break;
            case 1:
                filters.FreeCompany = !filters.FreeCompany;
                break;
        }

        return y;
    }

    private float DrawDivisionFilters(ImDrawListPtr drawList, HousingFilterState filters, float left, float right,
        float y, float scale)
    {
        chipLabels[0] = Loc.T(L.Housing.MainDivision);
        chipActive[0] = filters.MainDivision;
        chipLabels[1] = Loc.T(L.Housing.Subdivision);
        chipActive[1] = filters.Subdivision;
        y = DrawChipSection(drawList, divisionRail, left, right, y, L.Housing.FilterDivision, 2, scale,
            out var tapped);
        switch (tapped)
        {
            case 0:
                filters.MainDivision = !filters.MainDivision;
                break;
            case 1:
                filters.Subdivision = !filters.Subdivision;
                break;
        }

        return y;
    }

    private float DrawDataFilters(ImDrawListPtr drawList, HousingFilterState filters, float left, float right,
        float y, float scale)
    {
        chipLabels[0] = Loc.T(L.Housing.FilterFreshOnly);
        chipActive[0] = filters.FreshOnly;
        chipLabels[1] = Loc.T(L.Housing.FilterWatchedOnly);
        chipActive[1] = filters.WatchedOnly;
        y = DrawChipSection(drawList, dataRail, left, right, y, L.Housing.FilterData, 2, scale, out var tapped);
        switch (tapped)
        {
            case 0:
                filters.FreshOnly = !filters.FreshOnly;
                break;
            case 1:
                filters.WatchedOnly = !filters.WatchedOnly;
                break;
        }

        return y;
    }

    private void DrawEntryFilters(ImDrawListPtr drawList, HousingFilterState filters, float left, float right,
        float y, float scale)
    {
        for (var index = 0; index < EntryCaps.Length; index++)
        {
            chipLabels[index] = EntryCaps[index] == 0
                ? Loc.T(L.Housing.FilterAnyEntries)
                : HousingText.Count(EntryCaps[index]);
            chipActive[index] = filters.MaxEntries == EntryCaps[index];
        }

        DrawChipSection(drawList, entriesRail, left, right, y, L.Housing.FilterMaxEntries, EntryCaps.Length, scale,
            out var tapped);
        if (tapped >= 0)
        {
            filters.MaxEntries = EntryCaps[tapped];
        }
    }

    private float DrawChipSection(ImDrawListPtr drawList, ChipRail rail, float left, float right, float y,
        LocString label, int count, float scale, out int tapped)
    {
        var text = Loc.T(label);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(text, right - left,
            TextStyles.FootnoteEmphasized), ui.MutedInk, TextStyles.FootnoteEmphasized);
        var top = y + Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Sm * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(right, top + ChipRail.RowHeight * scale));
        tapped = rail.Draw(row, ui, new ReadOnlySpan<string>(chipLabels, 0, count),
            new ReadOnlySpan<bool>(chipActive, 0, count), true);
        if (tapped >= 0)
        {
            UiFeedback.Play(UiSound.Tap);
            InvalidateCache();
        }

        return row.Max.Y + Metrics.Space.Sm * scale;
    }
}
