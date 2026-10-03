using Aetherphone.Core;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private const int SearchPreviewRows = 4;
    private const float StateTopGap = 40f;
    private const float StateHeight = 300f;

    private readonly List<DigestRow>[] searchRows = CreateRowLists();
    private readonly int[] searchTotals = new int[CollectionCategories.All.Length];
    private readonly string[] searchCounts = new string[CollectionCategories.All.Length];
    private readonly List<CollectionItem> searchScratch = new();
    private string searchSource = string.Empty;
    private int searchRevision = -1;
    private int searchHitTotal;
    private bool searchPending;

    private static List<DigestRow>[] CreateRowLists()
    {
        var lists = new List<DigestRow>[CollectionCategories.All.Length];
        for (var index = 0; index < lists.Length; index++)
        {
            lists[index] = new List<DigestRow>(SearchPreviewRows);
        }

        return lists;
    }

    private void RefreshSearch()
    {
        var revision = catalog.Revision;
        if (string.Equals(searchSource, rootQuery, StringComparison.Ordinal) && searchRevision == revision)
        {
            return;
        }

        searchSource = rootQuery;
        searchRevision = revision;
        searchHitTotal = 0;
        searchPending = false;
        var query = CollectionFilter.Normalize(rootQuery);
        var categories = CollectionCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            var rows = searchRows[index];
            rows.Clear();
            searchTotals[index] = 0;
            searchCounts[index] = string.Empty;
            var entry = catalog.RequestCatalog(categories[index]);
            if (entry.State != CollectionState.Ready)
            {
                searchPending |= entry.State != CollectionState.Failed;
                continue;
            }

            CollectionFilter.Apply(entry.Items, searchScratch, query, OwnershipFilter.All, string.Empty, null,
                CollectionSort.Default);
            searchTotals[index] = searchScratch.Count;
            searchHitTotal += searchScratch.Count;
            searchCounts[index] = searchScratch.Count.ToString("N0", Loc.Culture);
            for (var hitIndex = 0; hitIndex < searchScratch.Count && hitIndex < SearchPreviewRows; hitIndex++)
            {
                var item = searchScratch[hitIndex];
                rows.Add(new DigestRow(item, item.RarityText, BadgeFor(item)));
            }
        }

        searchScratch.Clear();
    }

    private float DrawGlobalResults(ImDrawListPtr drawList, Vector2 origin, float width, Rect body, float scale)
    {
        RefreshSearch();
        if (searchHitTotal == 0)
        {
            var state = new Rect(new Vector2(body.Min.X, origin.Y + StateTopGap * scale),
                new Vector2(body.Max.X, origin.Y + (StateTopGap + StateHeight) * scale));
            if (searchPending)
            {
                LoadingPulse.Draw(state.Center, 13f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
            }
            else
            {
                CollectionsArt.StateScreen(state, ui, FontAwesomeIcon.SearchMinus, Loc.T(L.Collections.NoResults),
                    Loc.T(L.Collections.NoResultsHint), string.Empty);
            }

            return state.Max.Y;
        }

        var cursorY = origin.Y;
        var categories = CollectionCategories.All;
        var first = true;
        for (var index = 0; index < categories.Length; index++)
        {
            var rows = searchRows[index];
            if (rows.Count == 0)
            {
                continue;
            }

            if (!first)
            {
                cursorY += SectionGap * scale;
            }

            first = false;
            cursorY = DrawResultHeader(drawList, origin.X, cursorY, width, categories[index], index, scale);
            cursorY += HeaderGap * scale;
            cursorY = DrawRowGroup(drawList, new Vector2(origin.X, cursorY), width, rows, null);
        }

        return cursorY;
    }

    private float DrawResultHeader(ImDrawListPtr drawList, float left, float top, float width,
        CollectionCategory category, int index, float scale)
    {
        var showAll = searchTotals[index] > SearchPreviewRows;
        var linkLabel = Loc.T(L.Collections.SeeAll);
        var linkWidth = showAll ? Typography.Measure(linkLabel, TextStyles.Body).X : 0f;
        var title = Loc.T(CollectionText.Label(category));
        var titleWidth = MathF.Max(1f, width - linkWidth - HeaderGap * scale);
        var height = CollectionsArt.SectionHeader(drawList, new Vector2(left, top), titleWidth, title, ui.TitleInk);
        var titleSize = Typography.Measure(Typography.FitText(title, titleWidth, TextStyles.Title3), TextStyles.Title3);
        var countLeft = left + titleSize.X + HeaderGap * scale * 0.6f;
        var countSize = Typography.Measure(searchCounts[index], TextStyles.Subheadline);
        if (countLeft + countSize.X < left + titleWidth)
        {
            Typography.Draw(drawList, new Vector2(countLeft, top + (height - countSize.Y) * 0.5f), searchCounts[index],
                ui.MutedInk, TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight);
        }

        if (!showAll)
        {
            return top + height;
        }

        var linkSize = Typography.Measure(linkLabel, TextStyles.Body);
        var linkMin = new Vector2(left + width - linkSize.X, top + (height - linkSize.Y) * 0.5f);
        var linkMax = linkMin + linkSize;
        var hovered = UiInteract.Hover(linkMin, linkMax);
        Typography.Draw(drawList, linkMin, linkLabel, hovered ? Palette.Lighten(ui.Accent, 0.2f) : ui.Accent,
            TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(linkMin, linkMax, hovered))
        {
            OpenCategory(category, rootQuery);
        }

        return top + height;
    }
}
