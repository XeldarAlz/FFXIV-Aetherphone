using Aetherphone.Core;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int SearchLimit = 12;
    private const int SearchScan = 48;
    private const int QueryMaxLength = 60;

    private readonly List<MarketItemRef> searchHits = new();
    private readonly List<MarketItemRef> searchFound = new();
    private string discoverQuery = string.Empty;
    private string searchedFor = string.Empty;
    private string searchTag = string.Empty;
    private string searchTagLabel = string.Empty;
    private bool searchIndexed;
    private bool searching;

    private float DrawSearchRow(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var cancel = Loc.T(L.Common.Cancel);
        var cancelWidth = PrismType.Measure(cancel, PrismFace.Label, labelPixels).X;
        var room = right - left - (searching ? cancelWidth + Metrics.Space.Md * scale : 0f);
        var bottom = DrawInput(drawList, left, top, room, Loc.T(L.Prism.SearchHint), "##glamSearch", ref discoverQuery,
            QueryMaxLength, scale);
        if (searching && PrismArt.Link(drawList, new Vector2(right - cancelWidth,
                top + (FieldUnits * scale - PrismType.LineHeight(PrismFace.Label, labelPixels)) * 0.5f), cancel,
                PrismArt.Muted, scale))
        {
            discoverQuery = string.Empty;
        }

        UpdateSearch();
        return bottom + Metrics.Space.Md * scale;
    }

    private void UpdateSearch()
    {
        if (discoverQuery == searchedFor && searchIndexed == marketIndex.Ready)
        {
            return;
        }

        searchedFor = discoverQuery;
        searchIndexed = marketIndex.Ready;
        searchFound.Clear();
        var query = discoverQuery.Trim();
        searching = query.Length > 0;
        searchTag = query.TrimStart('#').Trim().ToLowerInvariant();
        searchTagLabel = searchTag.Length > 0 ? Loc.T(L.Prism.GlamsTagged, TagLabel(searchTag)) : string.Empty;
        if (!searching || query[0] == '#')
        {
            return;
        }

        marketIndex.EnsureBuilt();
        marketIndex.Search(query, searchHits, SearchScan);
        for (var index = 0; index < searchHits.Count && searchFound.Count < SearchLimit; index++)
        {
            if (gameData.EquipSlot(searchHits[index].Id) != GearSlots.None)
            {
                searchFound.Add(searchHits[index]);
            }
        }
    }

    private float DrawSearchResults(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var icon = GearIconUnits * scale;
        var row = icon + Metrics.Space.Md * scale;
        if (searchTag.Length > 0)
        {
            var tagTop = top + (row - labelLine) * 0.5f;
            if (PrismArt.Link(drawList, new Vector2(left, tagTop),
                    PrismType.FitLabel(searchTagLabel, labelPixels, width), PrismArt.Fore, scale, row))
            {
                OpenTagGlams(searchTag);
            }

            top += row;
            PrismArt.Hairline(drawList, left, left + width, top, PrismArt.Faint, scale);
        }

        for (var index = 0; index < searchFound.Count; index++)
        {
            var item = searchFound[index];
            var iconTop = top + (row - icon) * 0.5f;
            GameIconTile.Draw(drawList, textures, item.IconId, new Vector2(left, iconTop),
                new Vector2(left + icon, iconTop + icon), 0f, scale);
            var textLeft = left + icon + Metrics.Space.Md * scale;
            if (PrismArt.Link(drawList, new Vector2(textLeft, top + (row - labelLine) * 0.5f),
                    PrismType.FitLabel(item.Name, labelPixels, left + width - textLeft), PrismArt.Fore, scale, row))
            {
                OpenItemGlams(item.Id);
            }

            top += row;
            PrismArt.Hairline(drawList, left, left + width, top, PrismArt.Faint, scale);
        }

        return top + PrismArt.BottomMargin(scale);
    }
}
