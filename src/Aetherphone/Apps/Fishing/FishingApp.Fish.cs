using Aetherphone.Core;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Fishing;

internal enum FishCaughtFilter : byte
{
    All,
    Caught,
    NotCaught,
}

internal sealed partial class FishingApp
{
    private const int SearchMaxLength = 48;
    private const int UpcomingLimit = 30;
    private const int ResultLimit = 60;
    private const int WindowBudgetPerRebuild = 48;
    private const float ListRebuildSeconds = 1f;
    private const float FishRowHeight = 64f;
    private const float FishIconSize = 40f;
    private const float FishCheckSize = 14f;
    private const float FishBellSize = 12f;
    private const float FishBellHitSize = 28f;
    private const float SearchGap = 10f;
    private const float SkeletonRowUnits = 56f;
    private const float SkeletonGapUnits = 10f;
    private const float SkeletonAreaUnits = 340f;
    private const int FilterBigFish = 0;
    private const int FilterCaught = 1;
    private const int FilterUncaught = 2;
    private const int FilterSlots = 3;
    private const float AlertLeadGap = 6f;
    private static readonly int[] AlertLeadMinuteSteps = { 1, 2, 3, 5, 10, 15, 20, 30, 45, 60 };
    private const float ProgressPad = 14f;
    private const float ProgressGap = 8f;
    private const float ProgressBarHeight = 8f;
    private const float ProgressTrackAlpha = 0.12f;
    private const long OpenSortBias = long.MinValue / 4;

    private readonly ChipRail filterRail = new();
    private readonly string[] filterLabels = new string[FilterSlots];
    private readonly bool[] filterActive = new bool[FilterSlots];
    private readonly ChipRail expansionRail = new();
    private readonly string[] expansionLabels = new string[TimedFish.ExpansionSlots];
    private readonly bool[] expansionActive = new bool[TimedFish.ExpansionSlots];
    private readonly int[] expansionChips = new int[TimedFish.ExpansionSlots];
    private Action? stepAlertLeadDown;
    private Action? stepAlertLeadUp;
    private readonly ChipRail patchRail = new();
    private readonly string[] patchLabels = new string[FishPatchChips.PatchSlots];
    private readonly bool[] patchActive = new bool[FishPatchChips.PatchSlots];
    private readonly float[] patchChips = new float[FishPatchChips.PatchSlots];
    private readonly int[] patchKeys = new int[FishPatchChips.PatchSlots];
    private int[] alarmRows = Array.Empty<int>();
    private int[] openRows = Array.Empty<int>();
    private int[] upcomingRows = Array.Empty<int>();
    private int[] resultRows = Array.Empty<int>();
    private long[] sortKeys = Array.Empty<long>();
    private CachedText[] rowStatus = Array.Empty<CachedText>();
    private CachedText[] rowWhen = Array.Empty<CachedText>();
    private string[] rowSubtitles = Array.Empty<string>();
    private int alarmCount;
    private int openCount;
    private int upcomingCount;
    private int resultCount;
    private string search = string.Empty;
    private string builtSearch = string.Empty;
    private string foldedSearch = string.Empty;
    private bool bigFishOnly;
    private FishCaughtFilter caughtFilter;
    private int expansionFilter = -1;
    private int expansionChipCount = -1;
    private int patchFilter = -1;
    private int patchChipCount = -1;
    private int patchChipExpansion = -1;
    private int progressCaught;
    private int progressTotal;
    private int shownProgressCaught = -1;
    private int shownProgressTotal = -1;
    private string shownProgressLanguage = string.Empty;
    private string progressText = string.Empty;
    private bool fishListDirty = true;
    private bool scrollFishListToTop;
    private int builtCaughtVersion = -1;
    private int builtAlarmCount = -1;
    private float sinceListBuild;

    private void DrawFishList(Rect body, float scale)
    {
        using var id = ImRaii.PushId("fishing.fish");
        using var surface = AppSurface.Begin(body);
        if (scrollFishListToTop)
        {
            ImGui.SetScrollY(0f);
            scrollFishListToTop = false;
        }

        DrawSearch(scale);
        var state = catalog.State;
        if (state == FishingCatalogState.Failed)
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Fishing.LoadFailedTitle),
                    Loc.T(L.Fishing.LoadFailedHint), Loc.T(L.Common.Retry)))
            {
                UiFeedback.Play(UiSound.Refresh);
                catalog.Retry();
            }

            return;
        }

        if (state != FishingCatalogState.Ready)
        {
            catalog.EnsureLoaded();
            DrawFishSkeleton(scale);
            return;
        }

        EnsureRowCaches();
        RebuildFishListIfDue();
        DrawFilters();
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var listing = Listing;
        if (listing)
        {
            DrawCaughtProgress(scale);
        }

        if (listing || foldedSearch.Length > 0)
        {
            if (resultCount == 0)
            {
                DrawFishEmpty(body, Loc.T(L.Fishing.NoMatchTitle),
                    Loc.T(listing ? L.Fishing.FilterEmptyHint : L.Fishing.NoMatchHint));
                return;
            }

            var title = !listing ? L.Fishing.ResultsSection
                : caughtFilter == FishCaughtFilter.Caught ? L.Fishing.CaughtBadge : L.Fishing.NotCaughtBadge;
            DrawFishSection(Loc.T(title), resultRows, resultCount, nowUnix, scale, true);
            BottomSpacer(scale);
            return;
        }

        if (alarmCount + openCount + upcomingCount == 0)
        {
            DrawFishEmpty(body, Loc.T(L.Fishing.NoMatchTitle), Loc.T(L.Fishing.FilterEmptyHint));
            return;
        }

        if (alarmCount > 0)
        {
            DrawFishSection(Loc.T(L.Fishing.AlarmsSection), alarmRows, alarmCount, nowUnix, scale, true);
            DrawAlertLead(scale);
        }

        if (openCount > 0)
        {
            DrawFishSection(Loc.T(L.Fishing.UpNowSection), openRows, openCount, nowUnix, scale, alarmCount == 0);
        }

        if (upcomingCount > 0)
        {
            DrawFishSection(Loc.T(L.Fishing.ComingUpSection), upcomingRows, upcomingCount, nowUnix, scale,
                alarmCount == 0 && openCount == 0);
        }

        DrawFootnote(Loc.T(L.Fishing.WindowsNote), scale);
        BottomSpacer(scale);
    }

    private void DrawSearch(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var field = new Rect(origin, origin + new Vector2(width, GlassField.HeightUnits * scale));
        UiAnchors.Report("fishing.search", field);
        var ink = ui.Ink;
        SearchBar.Surface(ImGui.GetWindowDrawList(), field, ink);
        GlassField.Search(ImGui.GetWindowDrawList(), field, "##fishingSearch", Loc.T(L.Fishing.SearchHint),
            ref search, ink.Ink, ink.Muted, scale, SearchMaxLength, false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (GlassField.HeightUnits + SearchGap) * scale));
        if (string.Equals(search, builtSearch, StringComparison.Ordinal))
        {
            return;
        }

        builtSearch = search;
        foldedSearch = search.Trim().ToLowerInvariant();
        fishListDirty = true;
    }

    private bool Listing => caughtFilter != FishCaughtFilter.All && alerts.CaughtKnown;

    private void DrawFilters()
    {
        EnsureExpansionChips();
        filterLabels[FilterBigFish] = Loc.T(L.Fishing.BigFish);
        filterActive[FilterBigFish] = bigFishOnly;
        var count = FilterCaught;
        if (alerts.CaughtKnown)
        {
            filterLabels[FilterCaught] = Loc.T(L.Fishing.CaughtBadge);
            filterActive[FilterCaught] = caughtFilter == FishCaughtFilter.Caught;
            filterLabels[FilterUncaught] = Loc.T(L.Fishing.UncaughtFilter);
            filterActive[FilterUncaught] = caughtFilter == FishCaughtFilter.NotCaught;
            count = FilterUncaught + 1;
        }

        var tapped = filterRail.Draw(ui, filterLabels.AsSpan(0, count), filterActive.AsSpan(0, count),
            "fishing.filters");
        if (tapped == FilterBigFish)
        {
            bigFishOnly = !bigFishOnly;
            fishListDirty = true;
            UiFeedback.Play(bigFishOnly ? UiSound.ToggleOn : UiSound.ToggleOff);
        }
        else if (tapped == FilterCaught || tapped == FilterUncaught)
        {
            var picked = tapped == FilterCaught ? FishCaughtFilter.Caught : FishCaughtFilter.NotCaught;
            caughtFilter = caughtFilter == picked ? FishCaughtFilter.All : picked;
            fishListDirty = true;
            UiFeedback.Play(caughtFilter != FishCaughtFilter.All ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        DrawExpansionChips();
        if (expansionFilter >= 0)
        {
            DrawPatchChips();
        }

        ImGui.Dummy(new Vector2(0f, 2f * UiScale.Current));
    }

    private void DrawExpansionChips()
    {
        if (expansionChipCount <= 0)
        {
            return;
        }

        for (var chip = 0; chip < expansionChipCount; chip++)
        {
            expansionLabels[chip] = catalog.ExpansionName(expansionChips[chip]);
            expansionActive[chip] = expansionFilter == expansionChips[chip];
        }

        var tapped = expansionRail.Draw(ui, expansionLabels.AsSpan(0, expansionChipCount),
            expansionActive.AsSpan(0, expansionChipCount), "fishing.expansions");
        if (tapped < 0)
        {
            return;
        }

        var picked = expansionChips[tapped];
        expansionFilter = expansionFilter == picked ? -1 : picked;
        patchFilter = -1;
        fishListDirty = true;
        UiFeedback.Play(expansionFilter >= 0 ? UiSound.ToggleOn : UiSound.ToggleOff);
    }

    private void DrawAlertLead(float scale)
    {
        stepAlertLeadDown ??= () => StepAlertLead(-1);
        stepAlertLeadUp ??= () => StepAlertLead(1);
        var card = GroupCard.Begin(ui, 1);
        StepperField.Draw(ui, card.NextRow(), Loc.T(L.Fishing.AlertLeadValue, alerts.LeadMinutes), scale,
            stepAlertLeadDown, stepAlertLeadUp);
        card.End();
        DrawFootnote(Loc.T(L.Fishing.AlertLeadHint), scale);
        ImGui.Dummy(new Vector2(0f, AlertLeadGap * scale));
    }

    private void StepAlertLead(int direction)
    {
        var current = alerts.LeadMinutes;
        var nearest = 0;
        for (var index = 0; index < AlertLeadMinuteSteps.Length; index++)
        {
            if (AlertLeadMinuteSteps[index] <= current)
            {
                nearest = index;
            }
        }

        var target = Math.Clamp(nearest + direction, 0, AlertLeadMinuteSteps.Length - 1);
        alerts.SetLeadMinutes(AlertLeadMinuteSteps[target]);
    }

    private void DrawPatchChips()
    {
        EnsurePatchChips();
        if (patchChipCount == 0)
        {
            return;
        }

        for (var chip = 0; chip < patchChipCount; chip++)
        {
            patchActive[chip] = patchFilter == patchKeys[chip];
        }

        var tapped = patchRail.Draw(ui, patchLabels.AsSpan(0, patchChipCount), patchActive.AsSpan(0, patchChipCount),
            "fishing.patches");
        if (tapped < 0)
        {
            return;
        }

        var picked = patchKeys[tapped];
        patchFilter = patchFilter == picked ? -1 : picked;
        fishListDirty = true;
        UiFeedback.Play(patchFilter >= 0 ? UiSound.ToggleOn : UiSound.ToggleOff);
    }

    private void EnsurePatchChips()
    {
        if (patchChipCount >= 0 && patchChipExpansion == expansionFilter)
        {
            return;
        }

        patchChipExpansion = expansionFilter;
        patchChipCount = FishPatchChips.Collect(catalog.Entries, expansionFilter, patchChips);
        for (var chip = 0; chip < patchChipCount; chip++)
        {
            patchKeys[chip] = FishPatchChips.Key(patchChips[chip]);
            patchLabels[chip] = FishingCatalog.PatchText(patchChips[chip]);
        }

        patchRail.Reset();
    }

    private void EnsureExpansionChips()
    {
        if (expansionChipCount >= 0)
        {
            return;
        }

        Span<bool> present = stackalloc bool[TimedFish.ExpansionSlots];
        var entries = catalog.Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            present[entries[index].Fish.Expansion] = true;
        }

        expansionChipCount = 0;
        for (var expansion = 0; expansion < present.Length; expansion++)
        {
            if (present[expansion] && catalog.ExpansionName(expansion).Length > 0)
            {
                expansionChips[expansionChipCount++] = expansion;
            }
        }
    }

    private void DrawCaughtProgress(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = ProgressPad * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Headline);
        var barHeight = ProgressBarHeight * scale;
        var max = origin + new Vector2(width, pad * 2f + labelHeight + ProgressGap * scale + barHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var label = Typography.FitText(ProgressText(), MathF.Max(1f, width - pad * 2f), TextStyles.Headline);
        Typography.Draw(drawList, origin + new Vector2(pad, pad), label, ui.TitleInk, TextStyles.Headline);
        var barMin = new Vector2(origin.X + pad, max.Y - pad - barHeight);
        var barMax = new Vector2(max.X - pad, max.Y - pad);
        var rounding = barHeight * 0.5f;
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, ProgressTrackAlpha)),
            rounding);
        var fraction = progressTotal > 0 ? Math.Clamp(progressCaught / (float)progressTotal, 0f, 1f) : 0f;
        if (fraction > 0f)
        {
            var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y);
            drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(ui.Accent), rounding);
        }

        ImGui.Dummy(new Vector2(width, max.Y - origin.Y + ProgressGap * scale));
    }

    private string ProgressText()
    {
        var language = Loc.Current.Code;
        if (progressCaught == shownProgressCaught && progressTotal == shownProgressTotal
            && string.Equals(language, shownProgressLanguage, StringComparison.Ordinal))
        {
            return progressText;
        }

        shownProgressCaught = progressCaught;
        shownProgressTotal = progressTotal;
        shownProgressLanguage = language;
        progressText = Loc.T(L.Fishing.CaughtProgress, NumberText.Group(progressCaught),
            NumberText.Group(progressTotal));
        return progressText;
    }

    private void DrawFishSkeleton(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var area = new Rect(origin, origin + new Vector2(width, SkeletonAreaUnits * scale));
        Skeleton.Rows(ImGui.GetWindowDrawList(), area, SkeletonRowUnits, SkeletonGapUnits, scale);
        ImGui.Dummy(area.Size);
    }

    private void DrawFishEmpty(Rect body, string title, string hint)
    {
        var origin = ImGui.GetCursorScreenPos();
        var area = new Rect(origin, new Vector2(body.Max.X, MathF.Max(body.Max.Y, origin.Y + body.Height * 0.6f)));
        EmptyState.Draw(area, ui, FontAwesomeIcon.Fish, title, hint);
    }

    private void EnsureRowCaches()
    {
        var count = catalog.Entries.Length;
        if (rowStatus.Length == count)
        {
            return;
        }

        alarmRows = new int[count];
        openRows = new int[count];
        upcomingRows = new int[count];
        resultRows = new int[count];
        sortKeys = new long[count];
        rowStatus = new CachedText[count];
        rowWhen = new CachedText[count];
        rowSubtitles = new string[count];
        expansionChipCount = -1;
        patchChipCount = -1;
        fishListDirty = true;
    }

    private void RebuildFishListIfDue()
    {
        sinceListBuild += deltaSeconds;
        if (alerts.CaughtVersion != builtCaughtVersion || alerts.AlarmCount != builtAlarmCount)
        {
            fishListDirty = true;
        }

        if (!fishListDirty && sinceListBuild < ListRebuildSeconds)
        {
            return;
        }

        fishListDirty = false;
        sinceListBuild = 0f;
        builtCaughtVersion = alerts.CaughtVersion;
        builtAlarmCount = alerts.AlarmCount;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        catalog.RefreshWindows(nowUnix, WindowBudgetPerRebuild);
        var entries = catalog.Entries;
        var listing = Listing;
        var searching = foldedSearch.Length > 0;
        alarmCount = 0;
        openCount = 0;
        upcomingCount = 0;
        resultCount = 0;
        progressCaught = 0;
        progressTotal = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (bigFishOnly && !entry.Fish.IsBigFish)
            {
                continue;
            }

            if (expansionFilter >= 0 && entry.Fish.Expansion != expansionFilter)
            {
                continue;
            }

            if (patchFilter >= 0 && FishPatchChips.Key(entry.Fish.Patch) != patchFilter)
            {
                continue;
            }

            if (searching && !entry.SearchKey.Contains(foldedSearch, StringComparison.Ordinal))
            {
                continue;
            }

            if (listing && entry.LogRow == 0)
            {
                continue;
            }

            var caught = alerts.IsCaught(entry.ItemId);
            progressTotal++;
            if (caught)
            {
                progressCaught++;
            }

            if (listing && caught != (caughtFilter == FishCaughtFilter.Caught))
            {
                continue;
            }

            if (searching || listing)
            {
                resultRows[resultCount++] = index;
                continue;
            }

            if (alerts.HasAlarm(entry.ItemId))
            {
                alarmRows[alarmCount++] = index;
                continue;
            }

            if (entry.Fish.Rule.AlwaysOpen)
            {
                continue;
            }

            var window = catalog.WindowAt(index);
            if (window.IsOpen(nowUnix))
            {
                openRows[openCount++] = index;
            }
            else if (window.Exists)
            {
                upcomingRows[upcomingCount++] = index;
            }
        }

        SortRows(alarmRows, alarmCount, nowUnix);
        SortRows(openRows, openCount, nowUnix);
        SortRows(upcomingRows, upcomingCount, nowUnix);
        SortRows(resultRows, resultCount, nowUnix);
        upcomingCount = Math.Min(upcomingCount, UpcomingLimit);
        if (!listing)
        {
            resultCount = Math.Min(resultCount, ResultLimit);
        }
    }

    private void SortRows(int[] rows, int count, long nowUnix)
    {
        for (var index = 0; index < count; index++)
        {
            sortKeys[index] = SortKey(rows[index], nowUnix);
        }

        Array.Sort(sortKeys, rows, 0, count);
    }

    private long SortKey(int row, long nowUnix)
    {
        if (catalog.Entries[row].Fish.Rule.AlwaysOpen)
        {
            return OpenSortBias / 2;
        }

        var window = catalog.WindowAt(row);
        if (window.IsOpen(nowUnix))
        {
            return OpenSortBias + window.EndUnix;
        }

        return window.Exists ? window.StartUnix : long.MaxValue;
    }

    private void DrawFishSection(string title, int[] rows, int count, long nowUnix, float scale, bool anchorFirst)
    {
        CardSectionHeader.Flow(title, ui.TitleInk);
        var card = GroupCard.Begin(ui, count, FishRowHeight);
        card.SeparatorInset = (FishIconSize + RowGap) * scale;
        for (var index = 0; index < count; index++)
        {
            var row = card.NextRow();
            var bounds = new Rect(new Vector2(card.Bounds.Min.X, row.Min.Y), new Vector2(card.Bounds.Max.X, row.Max.Y));
            if (anchorFirst && index == 0)
            {
                UiAnchors.Report("fishing.fish.first", bounds);
            }

            if (!ImGui.IsRectVisible(bounds.Min, bounds.Max))
            {
                continue;
            }

            DrawFishRow(row, card.Bounds, rows[index], nowUnix, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }

    private void DrawFishRow(Rect row, Rect card, int entryIndex, long nowUnix, float scale)
    {
        var entry = catalog.Entries[entryIndex];
        var drawList = ImGui.GetWindowDrawList();
        var bounds = new Rect(new Vector2(card.Min.X, row.Min.Y), new Vector2(card.Max.X, row.Max.Y));
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var icon = FishIconSize * scale;
        FishingArt.ItemIcon(drawList, textures, entry.IconId, new Vector2(row.Min.X, row.Center.Y - icon * 0.5f), icon,
            scale, ui.FieldSurface);
        var window = catalog.WindowAt(entryIndex);
        var open = window.IsOpen(nowUnix);
        var status = RowStatus(entryIndex, entry, window, open, nowUnix);
        var when = RowWhen(entryIndex, entry, window, open);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (titleHeight + LineGap * scale + subHeight) * 0.5f;
        var statusStyle = TextStyles.SubheadlineEmphasized;
        var statusWidth = Typography.Measure(status, statusStyle).X;
        var whenWidth = Typography.Measure(when, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(row.Max.X - statusWidth,
                top + (titleHeight - Typography.LineHeight(statusStyle)) * 0.5f), status,
            open ? ui.Accent : ui.TitleInk, statusStyle);
        var subRight = row.Max.X - whenWidth;
        Typography.Draw(drawList, new Vector2(subRight, top + titleHeight + LineGap * scale), when, ui.MutedInk,
            TextStyles.Footnote);
        var bellHovered = false;
        if (alerts.HasAlarm(entry.ItemId))
        {
            var bell = FishBellSize * scale;
            var bellCenter = new Vector2(subRight - bell, top + titleHeight + LineGap * scale + subHeight * 0.5f);
            var bellZone = new Rect(bounds.Min, new Vector2(subRight, bounds.Max.Y));
            bellHovered = DrawAlarmBell(drawList, entry.ItemId, bellCenter, bell, bellZone, scale);
            subRight -= bell * 2f;
        }

        var textLeft = row.Min.X + icon + RowGap * scale;
        var nameRight = row.Max.X - statusWidth - RowGap * scale;
        if (alerts.IsCaught(entry.ItemId))
        {
            var check = FishCheckSize * scale;
            PhoneIcon.Draw(drawList, new Vector2(nameRight - check * 0.5f, top + titleHeight * 0.5f),
                PhoneIcons.CircleCheckFilled, ui.Accent, check);
            nameRight -= check + RowGap * scale * 0.5f;
        }

        Marquee.DrawLeftAuto(drawList, new MarqueeId("fishing.fish.name.", entry.ItemId), entry.Name, textLeft, top,
            MathF.Max(1f, nameRight - textLeft), TextStyles.Headline, entry.Fish.IsBigFish ? ui.TitleInk : ui.BodyInk);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + LineGap * scale),
            Typography.FitText(Subtitle(entryIndex, entry), MathF.Max(1f, subRight - RowGap * scale - textLeft),
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (UiInteract.Click(bounds.Min, bounds.Max, hovered && !bellHovered))
        {
            UiFeedback.Play(UiSound.Tap);
            router.Push(FishingRoute.Fish(entry.ItemId));
        }
    }

    private bool DrawAlarmBell(ImDrawListPtr drawList, uint itemId, Vector2 center, float size, Rect zone,
        float scale)
    {
        var hit = TapArea.Around(center, FishBellHitSize * scale, zone);
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        PhoneIcon.Draw(drawList, center, hovered ? PhoneIcons.BellOff : PhoneIcons.BellFilled, ui.Accent, size);
        if (!UiInteract.Click(hit.Min, hit.Max, hovered, UiSound.ToggleOff))
        {
            return hovered;
        }

        alerts.ToggleAlarm(itemId);
        fishListDirty = true;
        return hovered;
    }

    private string Subtitle(int entryIndex, FishEntry entry)
    {
        var cached = rowSubtitles[entryIndex];
        if (cached is not null)
        {
            return cached;
        }

        var built = FishingText.Join(entry.SpotName, entry.ZoneName);
        rowSubtitles[entryIndex] = built;
        return built;
    }

    private string RowStatus(int entryIndex, FishEntry entry, in FishWindow window, bool open, long nowUnix)
    {
        if (entry.Fish.Rule.AlwaysOpen)
        {
            return rowStatus[entryIndex].IsCurrent(long.MinValue)
                ? rowStatus[entryIndex].Value
                : rowStatus[entryIndex].Store(long.MinValue, Loc.T(L.Fishing.AnyTime));
        }

        if (!window.Exists)
        {
            return rowStatus[entryIndex].IsCurrent(long.MaxValue)
                ? rowStatus[entryIndex].Value
                : rowStatus[entryIndex].Store(long.MaxValue, Loc.T(L.Fishing.NoWindowShort));
        }

        if (open)
        {
            var left = window.EndUnix - nowUnix;
            var key = -1 - left;
            return rowStatus[entryIndex].IsCurrent(key)
                ? rowStatus[entryIndex].Value
                : rowStatus[entryIndex].Store(key, FishingClock.Countdown(left));
        }

        var until = window.StartUnix - nowUnix;
        var minuteKey = until / 60 + 1;
        return rowStatus[entryIndex].IsCurrent(minuteKey)
            ? rowStatus[entryIndex].Value
            : rowStatus[entryIndex].Store(minuteKey, FishingText.Relative(until));
    }

    private string RowWhen(int entryIndex, FishEntry entry, in FishWindow window, bool open)
    {
        if (entry.Fish.Rule.AlwaysOpen || !window.Exists)
        {
            return string.Empty;
        }

        var key = open ? -window.EndUnix : window.StartUnix;
        if (rowWhen[entryIndex].IsCurrent(key))
        {
            return rowWhen[entryIndex].Value;
        }

        var text = open
            ? Loc.T(L.Fishing.UntilClock, FishingText.LocalClock(window.EndUnix))
            : FishingText.LocalDayClock(window.StartUnix);
        return rowWhen[entryIndex].Store(key, text);
    }
}
