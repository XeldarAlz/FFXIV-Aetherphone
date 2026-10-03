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
    private const float SearchGap = 10f;
    private const float SkeletonRowUnits = 56f;
    private const float SkeletonGapUnits = 10f;
    private const float SkeletonAreaUnits = 340f;
    private const int FilterBigFish = 0;
    private const int FilterUncaught = 1;
    private const long OpenSortBias = long.MinValue / 4;

    private readonly ChipRail filterRail = new();
    private readonly string[] filterLabels = new string[2];
    private readonly bool[] filterActive = new bool[2];
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
    private bool uncaughtOnly;
    private bool fishListDirty = true;
    private int builtCaughtVersion = -1;
    private int builtAlarmCount = -1;
    private float sinceListBuild;

    private void DrawFishList(Rect body, float scale)
    {
        using var id = ImRaii.PushId("fishing.fish");
        using var surface = AppSurface.Begin(body);
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
        if (foldedSearch.Length > 0)
        {
            if (resultCount == 0)
            {
                DrawFishEmpty(body, Loc.T(L.Fishing.NoMatchTitle), Loc.T(L.Fishing.NoMatchHint));
                return;
            }

            DrawFishSection(Loc.T(L.Fishing.ResultsSection), resultRows, resultCount, nowUnix, scale, true);
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
        GlassField.Search(ImGui.GetWindowDrawList(), field, "##fishingSearch", Loc.T(L.Fishing.SearchHint),
            ref search, theme, scale, SearchMaxLength, false);
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

    private void DrawFilters()
    {
        filterLabels[FilterBigFish] = Loc.T(L.Fishing.BigFish);
        filterActive[FilterBigFish] = bigFishOnly;
        var count = 1;
        if (alerts.CaughtKnown)
        {
            filterLabels[FilterUncaught] = Loc.T(L.Fishing.UncaughtFilter);
            filterActive[FilterUncaught] = uncaughtOnly;
            count = 2;
        }

        var tapped = filterRail.Draw(ui, filterLabels.AsSpan(0, count), filterActive.AsSpan(0, count),
            "fishing.filters");
        if (tapped == FilterBigFish)
        {
            bigFishOnly = !bigFishOnly;
            fishListDirty = true;
            UiFeedback.Play(bigFishOnly ? UiSound.ToggleOn : UiSound.ToggleOff);
        }
        else if (tapped == FilterUncaught)
        {
            uncaughtOnly = !uncaughtOnly;
            fishListDirty = true;
            UiFeedback.Play(uncaughtOnly ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        ImGui.Dummy(new Vector2(0f, 2f * UiScale.Current));
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
        var hideCaught = uncaughtOnly && alerts.CaughtKnown;
        var searching = foldedSearch.Length > 0;
        alarmCount = 0;
        openCount = 0;
        upcomingCount = 0;
        resultCount = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (bigFishOnly && !entry.Fish.IsBigFish)
            {
                continue;
            }

            if (hideCaught && alerts.IsCaught(entry.ItemId))
            {
                continue;
            }

            if (searching)
            {
                if (entry.SearchKey.Contains(foldedSearch, StringComparison.Ordinal))
                {
                    resultRows[resultCount++] = index;
                }

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
        resultCount = Math.Min(resultCount, ResultLimit);
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
        ui.SectionLabel(title, TextStyles.FootnoteEmphasized, 6f);
        var card = GroupCard.Begin(ui, count, FishRowHeight);
        card.SeparatorInset = (FishIconSize + RowGap) * scale;
        for (var index = 0; index < count; index++)
        {
            var row = card.NextRow();
            if (anchorFirst && index == 0)
            {
                UiAnchors.Report("fishing.fish.first", new Rect(new Vector2(card.Bounds.Min.X, row.Min.Y),
                    new Vector2(card.Bounds.Max.X, row.Max.Y)));
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
        if (alerts.HasAlarm(entry.ItemId))
        {
            var bell = FishBellSize * scale;
            PhoneIcon.Draw(drawList, new Vector2(subRight - bell, top + titleHeight + LineGap * scale + subHeight * 0.5f),
                PhoneIcons.BellFilled, ui.Accent, bell);
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
        if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            router.Push(FishingRoute.Fish(entry.ItemId));
        }
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
