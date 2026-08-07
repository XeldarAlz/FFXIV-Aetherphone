using Aetherphone.Core;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const int FilterSectionCount = 6;

    private readonly ChipRail sizeRail = new();
    private readonly ChipRail phaseRail = new();
    private readonly ChipRail eligibilityRail = new();
    private readonly ChipRail divisionRail = new();
    private readonly ChipRail dataRail = new();
    private readonly ChipRail entriesRail = new();
    private readonly ChipRail reminderRail = new();

    private static readonly int[] EntryCaps = [0, 3, 10, 25];

    private readonly string[] chipLabels = new string[EntryCaps.Length];
    private readonly bool[] chipActive = new bool[EntryCaps.Length];
    private readonly string[] reminderLabels = new string[HousingDefaults.ReminderChoices.Length];
    private readonly bool[] reminderActive = new bool[HousingDefaults.ReminderChoices.Length];

    private static float FilterSectionHeight(float scale) =>
        Typography.LineHeight(TextStyles.Caption1) + (ChipRail.RowHeight + Metrics.Space.Sm * 2f) * scale;

    private static float FilterDrawerHeightFor(float scale)
    {
        var height = 18f * scale;
        height += Typography.LineHeight(TextStyles.Title3) + 10f * scale;
        height += 30f * scale + 12f * scale;
        height += FilterSectionCount * FilterSectionHeight(scale);
        return height + 36f * scale + 16f * scale;
    }

    private void DrawFilterDrawer(Rect area, float scale)
    {
        var progress = filterSpring.Value;
        if (progress <= 0.005f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var height = MathF.Min(FilterDrawerHeightFor(scale), area.Height * 0.90f);
        var travel = height * (1f - progress);
        var sheet = new Rect(new Vector2(area.Min.X, area.Max.Y - height + travel),
            new Vector2(area.Max.X, area.Max.Y + travel));
        drawList.PushClipRect(area.Min, area.Max, true);
        HousingChrome.SheetSurface(drawList, sheet, area, progress, ui);
        drawList.PopClipRect();
        if (progress < 0.35f)
        {
            return;
        }

        var pad = 16f * scale;
        var left = sheet.Min.X + pad;
        var right = sheet.Max.X - pad;
        var y = sheet.Min.Y + 18f * scale;
        var filters = housing.Filters;
        var matchCount = VisiblePlots().Count;
        Typography.Draw(drawList, new Vector2(left, y), Loc.T(L.Housing.Filters), ui.TitleInk, TextStyles.Title3);
        var countText = Loc.T(L.Housing.MatchingPlots, matchCount);
        var countSize = Typography.Measure(countText, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(right - countSize.X, y + 8f * scale), countText, ui.MutedInk,
            TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Title3) + 10f * scale;
        var segmentHeight = 30f * scale;
        var segmentRow = new Rect(new Vector2(left, y), new Vector2(right, y + segmentHeight));
        var picked = HousingChrome.Segment(segmentRow, Loc.T(L.Housing.ShowAvailableOnly),
            Loc.T(L.Housing.ShowAllPlots), filters.ShowAllPlots ? 1 : 0, ui, true);
        if (picked == 1 != filters.ShowAllPlots)
        {
            filters.ShowAllPlots = picked == 1;
            housing.PersistFilterDefaults();
            InvalidateCache();
        }

        y += segmentHeight + 12f * scale;
        chipLabels[0] = Loc.T(L.Housing.SizeSmall);
        chipActive[0] = filters.Small;
        chipLabels[1] = Loc.T(L.Housing.SizeMedium);
        chipActive[1] = filters.Medium;
        chipLabels[2] = Loc.T(L.Housing.SizeLarge);
        chipActive[2] = filters.Large;
        var labelSpan = new ReadOnlySpan<string>(chipLabels, 0, 3);
        var activeSpan = new ReadOnlySpan<bool>(chipActive, 0, 3);
        y = DrawChipSection(drawList, sizeRail, left, right, y, Loc.T(L.Housing.FilterSizes), scale, labelSpan,
            activeSpan, out var sizeTapped);
        switch (sizeTapped)
        {
            case 0:
                filters.Small = !filters.Small;
                housing.PersistFilterDefaults();
                break;
            case 1:
                filters.Medium = !filters.Medium;
                housing.PersistFilterDefaults();
                break;
            case 2:
                filters.Large = !filters.Large;
                housing.PersistFilterDefaults();
                break;
        }

        chipLabels[0] = Loc.T(L.Housing.PhaseEntry);
        chipActive[0] = filters.PhaseEntry;
        chipLabels[1] = Loc.T(L.Housing.PhaseResults);
        chipActive[1] = filters.PhaseResults;
        chipLabels[2] = Loc.T(L.Housing.FilterOtherPhases);
        chipActive[2] = filters.PhaseOther;
        labelSpan = new ReadOnlySpan<string>(chipLabels, 0, 3);
        activeSpan = new ReadOnlySpan<bool>(chipActive, 0, 3);
        y = DrawChipSection(drawList, phaseRail, left, right, y, Loc.T(L.Housing.FilterPhase), scale, labelSpan,
            activeSpan, out var phaseTapped);
        switch (phaseTapped)
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

        chipLabels[0] = Loc.T(L.Housing.EligibilityPrivate);
        chipActive[0] = filters.PrivateBuyers;
        chipLabels[1] = Loc.T(L.Housing.EligibilityFreeCompany);
        chipActive[1] = filters.FreeCompany;
        labelSpan = new ReadOnlySpan<string>(chipLabels, 0, 2);
        activeSpan = new ReadOnlySpan<bool>(chipActive, 0, 2);
        y = DrawChipSection(drawList, eligibilityRail, left, right, y, Loc.T(L.Housing.FilterEligibility), scale,
            labelSpan, activeSpan, out var eligibilityTapped);
        switch (eligibilityTapped)
        {
            case 0:
                filters.PrivateBuyers = !filters.PrivateBuyers;
                break;
            case 1:
                filters.FreeCompany = !filters.FreeCompany;
                break;
        }

        chipLabels[0] = Loc.T(L.Housing.MainDivision);
        chipActive[0] = filters.MainDivision;
        chipLabels[1] = Loc.T(L.Housing.Subdivision);
        chipActive[1] = filters.Subdivision;
        labelSpan = new ReadOnlySpan<string>(chipLabels, 0, 2);
        activeSpan = new ReadOnlySpan<bool>(chipActive, 0, 2);
        y = DrawChipSection(drawList, divisionRail, left, right, y, Loc.T(L.Housing.FilterDivision), scale,
            labelSpan, activeSpan, out var divisionTapped);
        switch (divisionTapped)
        {
            case 0:
                filters.MainDivision = !filters.MainDivision;
                break;
            case 1:
                filters.Subdivision = !filters.Subdivision;
                break;
        }

        chipLabels[0] = Loc.T(L.Housing.FilterFreshOnly);
        chipActive[0] = filters.FreshOnly;
        chipLabels[1] = Loc.T(L.Housing.FilterWatchedOnly);
        chipActive[1] = filters.WatchedOnly;
        labelSpan = new ReadOnlySpan<string>(chipLabels, 0, 2);
        activeSpan = new ReadOnlySpan<bool>(chipActive, 0, 2);
        y = DrawChipSection(drawList, dataRail, left, right, y, Loc.T(L.Housing.FilterData), scale, labelSpan,
            activeSpan, out var dataTapped);
        switch (dataTapped)
        {
            case 0:
                filters.FreshOnly = !filters.FreshOnly;
                break;
            case 1:
                filters.WatchedOnly = !filters.WatchedOnly;
                break;
        }

        for (var index = 0; index < EntryCaps.Length; index++)
        {
            chipLabels[index] = EntryCaps[index] == 0
                ? Loc.T(L.Housing.FilterAnyEntries)
                : EntryCaps[index].ToString(Loc.Culture);
            chipActive[index] = filters.MaxEntries == EntryCaps[index];
        }

        y = DrawChipSection(drawList, entriesRail, left, right, y, Loc.T(L.Housing.FilterMaxEntries), scale,
            chipLabels, chipActive, out var entriesTapped);
        if (entriesTapped >= 0)
        {
            filters.MaxEntries = EntryCaps[entriesTapped];
        }

        var buttonHeight = 32f * scale;
        var buttonTop = MathF.Min(y + 4f * scale, sheet.Max.Y - 16f * scale - buttonHeight);
        var gap = 8f * scale;
        var resetLabel = Loc.T(L.Housing.ClearFilters);
        var doneLabel = Loc.T(L.Common.Close);
        Span<string> buttonLabels = [resetLabel, doneLabel];
        Span<Rect> buttonRects = stackalloc Rect[2];
        HousingChrome.LayoutPills(
            new Rect(new Vector2(left, buttonTop), new Vector2(right, buttonTop + buttonHeight)), buttonLabels, gap,
            buttonRects);
        var resetRect = buttonRects[0];
        var doneRect = buttonRects[1];
        if (HousingChrome.PillButton(resetRect, resetLabel, false, ui, true, filters.HasNarrowingFilters))
        {
            filters.Reset();
            housing.PersistFilterDefaults();
            InvalidateCache();
        }

        if (HousingChrome.PillButton(doneRect, doneLabel, true, ui, true))
        {
            filtersOpen = false;
        }

        if (progress > 0.9f && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !ImGui.IsMouseHoveringRect(sheet.Min, sheet.Max, false))
        {
            filtersOpen = false;
        }
    }

    private float DrawChipSection(ImDrawListPtr drawList, ChipRail rail, float left, float right, float y,
        string label, float scale, ReadOnlySpan<string> labels, ReadOnlySpan<bool> active, out int tapped)
    {
        HousingChrome.SectionLabel(drawList, new Vector2(left, y), right - left, label, ui);
        var top = y + Typography.LineHeight(TextStyles.Caption1) + Metrics.Space.Sm * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(right, top + ChipRail.RowHeight * scale));
        tapped = rail.Draw(row, ui, labels, active, true);
        return row.Max.Y + Metrics.Space.Sm * scale;
    }

    private void ResetFilterRails()
    {
        sizeRail.Reset();
        phaseRail.Reset();
        eligibilityRail.Reset();
        divisionRail.Reset();
        dataRail.Reset();
        entriesRail.Reset();
        reminderRail.Reset();
    }
}
