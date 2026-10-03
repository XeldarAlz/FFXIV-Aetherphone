using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private const float ControlGap = 12f;
    private const float SegmentHeight = 34f;
    private const float ProgressCardHeight = 84f;
    private const float ProgressPad = 16f;
    private const float ProgressBarHeight = 8f;
    private const float ProgressTrackAlpha = 0.12f;
    private const float ListStateHeight = 260f;

    private readonly List<CollectionItem> filtered = new();
    private readonly SortedSet<string> sourceSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> sourceLabels = new();
    private readonly List<bool> sourceActive = new();
    private readonly string[] ownershipLabels = new string[3];
    private readonly ChipRail sourceRail = new();
    private readonly ActionSheet sortSheet = new();
    private readonly ActionSheet.Item[] sortItems = new ActionSheet.Item[CollectionFilter.SortCount];
    private readonly NavBarButton[] categoryButtons = new NavBarButton[1];

    private string categorySearch = string.Empty;
    private OwnershipFilter ownership = OwnershipFilter.All;
    private CollectionSort sort = CollectionSort.Default;
    private int sourceIndex;
    private bool resetScroll;
    private Spring progressFill;
    private CollectionItem[]? filteredSource;
    private CollectionItem[]? sourceListFor;
    private string filteredSearch = string.Empty;
    private OwnershipFilter filteredOwnership;
    private CollectionSort filteredSort;
    private int filteredSourceIndex = -1;
    private int filteredRevision = -1;
    private bool filteredHadOwned;
    private string progressLabel = string.Empty;
    private string progressPercent = string.Empty;
    private int progressCount = -1;
    private int progressTotal = -1;
    private string progressLanguage = string.Empty;

    private void ResetCategoryState()
    {
        categorySearch = string.Empty;
        ownership = OwnershipFilter.All;
        sort = CollectionSort.Default;
        sourceIndex = 0;
        resetScroll = true;
        filteredSource = null;
        sourceListFor = null;
        progressFill.SnapTo(0f);
    }

    private void DrawCategory(Rect area, CollectionCategory category)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var entry = catalog.RequestCatalog(category);
        var owned = OwnedIds(category);
        var progress = Progress(category);
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            if (owned is not null)
            {
                cursorY = DrawProgressCard(drawList, new Vector2(origin.X, cursorY), width, owned.Count,
                    progress is { Total: > 0 } ? progress.Total : entry.Total, scale);
                cursorY += ControlGap * scale;
            }
            else if (tracking)
            {
                cursorY = DrawAccessNotice(drawList, new Vector2(origin.X, cursorY), width, category, progress, scale);
            }

            var searchTop = cursorY;
            cursorY = DrawSearchField(drawList, new Vector2(origin.X, cursorY), width, ref categorySearch,
                "##collectionsCategorySearch", Loc.T(L.Collections.Search), scale);
            UiAnchors.Report("collections.search", new Rect(new Vector2(origin.X, searchTop),
                new Vector2(origin.X + width, cursorY)));
            cursorY += ControlGap * scale;
            if (owned is not null)
            {
                cursorY = DrawOwnershipSegments(new Vector2(origin.X, cursorY), width, scale);
                cursorY += ControlGap * scale;
            }

            if (entry.State == CollectionState.Ready)
            {
                cursorY = DrawSourceRail(entry, new Vector2(origin.X, cursorY), scale);
            }

            cursorY = DrawCategoryBody(drawList, entry, category, owned, new Vector2(origin.X, cursorY), width,
                navBar.Body, scale);
            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        categoryButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Filter), Loc.T(L.Collections.Sort));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "collections.category.nav",
            Loc.T(CollectionText.Label(category)), NavBarStyle.From(ui), categoryButtons, DisplayName, back);
        if (pressed == 0)
        {
            UiFeedback.Play(UiSound.Tap);
            sortSheet.Open();
        }
    }

    private float DrawProgressCard(ImDrawListPtr drawList, Vector2 origin, float width, int count, int total,
        float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + ProgressCardHeight * scale);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale, true);
        var fraction = total > 0 ? Math.Clamp(count / (float)total, 0f, 1f) : 0f;
        RefreshProgressLabels(count, total, fraction);
        var pad = ProgressPad * scale;
        var countSize = Typography.Measure(progressLabel, TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad), progressLabel, ui.TitleInk,
            TextStyles.Title2);
        var percentSize = Typography.Measure(progressPercent, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList,
            new Vector2(max.X - pad - percentSize.X, origin.Y + pad + (countSize.Y - percentSize.Y) * 0.5f),
            progressPercent, ui.Accent, TextStyles.SubheadlineEmphasized.Scale,
            TextStyles.SubheadlineEmphasized.Weight);
        var value = progressFill.Step(fraction, Motion.Sheet, FrameDelta());
        var barTop = max.Y - pad - ProgressBarHeight * scale;
        CollectionsArt.Bar(drawList, new Vector2(origin.X + pad, barTop),
            new Vector2(max.X - pad, barTop + ProgressBarHeight * scale), value,
            Palette.WithAlpha(ui.TitleInk, ProgressTrackAlpha), ui.Accent);
        return max.Y;
    }

    private void RefreshProgressLabels(int count, int total, float fraction)
    {
        var language = Loc.Current.Code;
        if (count == progressCount && total == progressTotal &&
            string.Equals(language, progressLanguage, StringComparison.Ordinal))
        {
            return;
        }

        progressCount = count;
        progressTotal = total;
        progressLanguage = language;
        progressLabel = CountLabel(count, total);
        progressPercent = Loc.T(L.Collections.CompletePercent, (int)MathF.Floor(fraction * 100f));
    }

    private float DrawAccessNotice(ImDrawListPtr drawList, Vector2 origin, float width, CollectionCategory category,
        CategoryProgress? progress, float scale)
    {
        string message;
        FontAwesomeIcon icon;
        if (category == CollectionCategory.Achievements && lodestoneId is null)
        {
            message = Loc.T(L.Collections.LinkHint);
            icon = FontAwesomeIcon.Link;
        }
        else if (progress is { HasPercent: false })
        {
            message = Loc.T(L.Collections.CollectionPrivate);
            icon = FontAwesomeIcon.Lock;
        }
        else if (catalog.RequestOwned(lodestoneId, category).State is OwnedState.Private)
        {
            message = Loc.T(L.Collections.CollectionPrivate);
            icon = FontAwesomeIcon.Lock;
        }
        else if (catalog.RequestOwned(lodestoneId, category).State is OwnedState.Failed)
        {
            message = Loc.T(L.Collections.OwnedUnavailable);
            icon = FontAwesomeIcon.ExclamationCircle;
        }
        else
        {
            return origin.Y;
        }

        var height = CollectionsArt.Panel(drawList, ui, origin, width, icon, ui.Accent,
            Loc.T(CollectionText.Label(category)), message, scale);
        return origin.Y + height + ControlGap * scale;
    }

    private float DrawOwnershipSegments(Vector2 origin, float width, float scale)
    {
        ownershipLabels[0] = Loc.T(L.Collections.FilterAll);
        ownershipLabels[1] = Loc.T(L.Collections.FilterOwned);
        ownershipLabels[2] = Loc.T(L.Collections.FilterMissing);
        var bar = new Rect(origin, new Vector2(origin.X + width, origin.Y + SegmentHeight * scale));
        UiAnchors.Report("collections.filter.missing",
            new Rect(new Vector2(bar.Max.X - bar.Width / 3f, bar.Min.Y), bar.Max));
        var selected = SegmentStrip.Draw("collections.ownership", bar, ownershipLabels, (int)ownership, ui.Palette);
        if (selected != (int)ownership)
        {
            ownership = (OwnershipFilter)selected;
            resetScroll = true;
            UiFeedback.Play(UiSound.Tap);
        }

        return bar.Max.Y;
    }

    private float DrawSourceRail(CatalogEntry entry, Vector2 origin, float scale)
    {
        if (!ReferenceEquals(sourceListFor, entry.Items))
        {
            sourceListFor = entry.Items;
            CollectionFilter.CollectSourceTypes(entry.Items, sourceSet);
            sourceLabels.Clear();
            sourceActive.Clear();
            sourceLabels.Add(string.Empty);
            sourceActive.Add(true);
            foreach (var type in sourceSet)
            {
                sourceLabels.Add(type);
                sourceActive.Add(false);
            }

            sourceIndex = 0;
        }

        if (sourceLabels.Count <= 2)
        {
            return origin.Y;
        }

        sourceLabels[0] = Loc.T(L.Collections.AllSources);
        for (var index = 0; index < sourceActive.Count; index++)
        {
            sourceActive[index] = index == sourceIndex;
        }

        ImGui.SetCursorScreenPos(origin);
        var tapped = sourceRail.Draw(ui, CollectionsMarshal.AsSpan(sourceLabels),
            CollectionsMarshal.AsSpan(sourceActive));
        if (tapped >= 0 && tapped != sourceIndex)
        {
            sourceIndex = tapped;
            resetScroll = true;
            UiFeedback.Play(UiSound.Tap);
        }

        return origin.Y + (ChipRail.RowHeight + ControlGap) * scale;
    }

    private void RefreshFiltered(CatalogEntry entry, HashSet<int>? owned)
    {
        var revision = catalog.Revision;
        var hasOwned = owned is not null;
        if (ReferenceEquals(filteredSource, entry.Items) && filteredRevision == revision &&
            filteredOwnership == ownership && filteredSort == sort && filteredSourceIndex == sourceIndex &&
            filteredHadOwned == hasOwned && string.Equals(filteredSearch, categorySearch, StringComparison.Ordinal))
        {
            return;
        }

        filteredSource = entry.Items;
        filteredRevision = revision;
        filteredOwnership = ownership;
        filteredSort = sort;
        filteredSourceIndex = sourceIndex;
        filteredHadOwned = hasOwned;
        if (!string.Equals(filteredSearch, categorySearch, StringComparison.Ordinal))
        {
            filteredSearch = categorySearch;
            resetScroll = true;
        }

        var sourceType = sourceIndex > 0 && sourceIndex < sourceLabels.Count ? sourceLabels[sourceIndex] : string.Empty;
        CollectionFilter.Apply(entry.Items, filtered, CollectionFilter.Normalize(categorySearch), ownership, sourceType,
            owned, sort);
    }

    private float DrawCategoryBody(ImDrawListPtr drawList, CatalogEntry entry, CollectionCategory category,
        HashSet<int>? owned, Vector2 origin, float width, Rect body, float scale)
    {
        var state = new Rect(new Vector2(body.Min.X, origin.Y),
            new Vector2(body.Max.X, origin.Y + ListStateHeight * scale));
        if (entry.State == CollectionState.Failed)
        {
            if (CollectionsArt.StateScreen(state, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Collections.Failed),
                    Loc.T(L.Common.LoadFailedHint), Loc.T(L.Collections.TryAgain)))
            {
                UiFeedback.Play(UiSound.Refresh);
                catalog.Retry(category);
            }

            return state.Max.Y;
        }

        if (entry.State != CollectionState.Ready)
        {
            LoadingPulse.Draw(state.Center, 13f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
            return state.Max.Y;
        }

        RefreshFiltered(entry, owned);
        if (filtered.Count == 0)
        {
            CollectionsArt.StateScreen(state, ui, FontAwesomeIcon.SearchMinus, Loc.T(L.Collections.NoResults),
                Loc.T(L.Collections.NoResultsHint), string.Empty);
            return state.Max.Y;
        }

        return DrawVirtualList(drawList, origin, width, owned, scale);
    }

    private float DrawVirtualList(ImDrawListPtr drawList, Vector2 origin, float width, HashSet<int>? owned,
        float scale)
    {
        var rowHeight = RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + filtered.Count * rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale, true);
        var clipTop = ImGui.GetWindowPos().Y;
        var clipBottom = clipTop + ImGui.GetWindowHeight();
        var first = Math.Max(0, (int)MathF.Floor((clipTop - origin.Y) / rowHeight));
        var last = Math.Min(filtered.Count, (int)MathF.Ceiling((clipBottom - origin.Y) / rowHeight));
        var anchored = false;
        for (var index = first; index < last; index++)
        {
            var item = filtered[index];
            var rect = new Rect(new Vector2(origin.X, origin.Y + index * rowHeight),
                new Vector2(max.X, origin.Y + (index + 1) * rowHeight));
            if (!anchored && rect.Min.Y >= clipTop)
            {
                anchored = true;
                UiAnchors.Report("collections.row", rect);
            }

            if (index > 0)
            {
                DrawRowSeparator(drawList, rect, scale);
            }

            var badge = owned is null ? RowBadge.None : owned.Contains(item.Id) ? RowBadge.Owned : RowBadge.Missing;
            if (DrawItemRow(drawList, rect, item, item.Subtitle, item.RarityText, badge))
            {
                OpenItem(item);
            }
        }

        return max.Y;
    }

    private void DrawSortSheet()
    {
        if (!sortSheet.CapturesPointer)
        {
            return;
        }

        for (var index = 0; index < sortItems.Length; index++)
        {
            sortItems[index] = new ActionSheet.Item(Loc.T(CollectionText.SortLabel((CollectionSort)index)),
                Selected: index == (int)sort, Checkable: true);
        }

        var picked = sortSheet.Draw(screen, ActionSheetStyle.From(ui), sortItems, Loc.T(L.Common.Cancel), false,
            Loc.T(L.Collections.Sort));
        if (picked < 0 || picked == (int)sort)
        {
            return;
        }

        sort = (CollectionSort)picked;
        resetScroll = true;
        UiFeedback.Play(UiSound.ToggleOn);
    }
}
