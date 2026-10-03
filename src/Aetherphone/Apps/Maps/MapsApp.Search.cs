using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal sealed partial class MapsApp
{
    private const int SearchMaxLength = 60;
    private const int SearchResultCap = 40;
    private const float EmptyDiscSize = 64f;
    private const float EmptyGlyphSize = 26f;
    private const float EmptyTopPad = 28f;
    private const float EmptyGap = 14f;
    private const float EmptyLineGap = 6f;

    private readonly List<MapAetheryte> searchResults = new();
    private readonly List<MapSearchHit> searchHits = new();
    private string search = string.Empty;
    private string searchQuery = string.Empty;

    private void RefreshSearch()
    {
        var trimmed = search.AsSpan().Trim();
        if (trimmed.Equals(searchQuery.AsSpan(), StringComparison.Ordinal))
        {
            return;
        }

        searchQuery = trimmed.ToString();
        searchResults.Clear();
        searchHits.Clear();
        if (searchQuery.Length == 0)
        {
            return;
        }

        CollectMatches(true);
        CollectMatches(false);
    }

    private void CollectMatches(bool inName)
    {
        var regions = maps.Regions;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var destinations = regions[regionIndex].Aetherytes;
            for (var index = 0; index < destinations.Count; index++)
            {
                if (searchResults.Count >= SearchResultCap)
                {
                    return;
                }

                var aetheryte = destinations[index];
                var nameAt = aetheryte.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase);
                if (inName)
                {
                    if (nameAt >= 0)
                    {
                        Add(aetheryte, aetheryte.Name, nameAt, true);
                    }

                    continue;
                }

                if (nameAt >= 0)
                {
                    continue;
                }

                var subtitleAt = aetheryte.Subtitle.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase);
                if (subtitleAt >= 0)
                {
                    Add(aetheryte, aetheryte.Subtitle, subtitleAt, false);
                }
            }
        }
    }

    private void Add(MapAetheryte aetheryte, string text, int at, bool inName)
    {
        var length = Math.Min(searchQuery.Length, text.Length - at);
        searchResults.Add(aetheryte);
        searchHits.Add(new MapSearchHit(text[..at], text.Substring(at, length), text[(at + length)..], inName));
    }

    private void DrawSearchResults(float scale)
    {
        if (searchResults.Count == 0)
        {
            DrawNoResults(scale);
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        var card = GroupCard.Begin(theme, searchResults.Count, PlaceRowHeight);
        card.SeparatorInset = PlaceRowInset;
        for (var index = 0; index < searchResults.Count; index++)
        {
            var hit = searchHits[index];
            DrawPlaceRow(card.NextRow(), searchResults[index], PhoneIcons.MapPin, in hit, true, scale, card.Bounds);
        }

        card.End();
    }

    private void DrawNoResults(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var disc = EmptyDiscSize * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + EmptyTopPad * scale + disc * 0.5f);
        drawList.AddCircleFilled(center, disc * 0.5f, ImGui.GetColorU32(theme.SurfaceMuted), 48);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Search, theme.TextMuted, EmptyGlyphSize * scale);
        var top = center.Y + disc * 0.5f + EmptyGap * scale;
        var bottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.Maps.NoZones), TextStyles.Title3,
            theme.TextStrong, new Vector2(center.X, top), width);
        bottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.Maps.NoResultsHint), TextStyles.Subheadline,
            theme.TextMuted, new Vector2(center.X, bottom + EmptyLineGap * scale), width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }
}
