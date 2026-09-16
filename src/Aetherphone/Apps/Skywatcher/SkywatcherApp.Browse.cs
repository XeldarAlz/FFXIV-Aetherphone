using Aetherphone.Apps.Maps;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher;

internal sealed partial class SkywatcherApp
{
    private const float PreviewCardHeight = 150f;
    private const int PreviewStripCount = 5;
    private const float PreviewCurrentGlyphRadius = 20f;
    private const float PreviewGlyphRadius = 14f;
    private const float ZoneRowHeight = 38f;
    private const float ZoneMiniGlyphRadius = 10f;
    private const float ZoneStarRadius = 8f;
    private const float SearchBarHeight = 40f;
    private const float FavoriteCardHeight = 116f;
    private const float FavoriteCurrentGlyphRadius = 16f;
    private readonly HashSet<uint> favorites = new();
    private readonly List<WeatherZoneEntry> favoriteZones = new();
    private readonly Dictionary<uint, WeatherEntry> rowWeather = new();
    private readonly Dictionary<uint, List<WeatherWindow>> favoriteForecasts = new();
    private readonly List<WeatherRegionGroup> filteredRegions = new();
    private readonly List<List<WeatherZoneEntry>> filteredMatchesPool = new();
    private IReadOnlyList<WeatherRegionGroup>? filteredSourceRegions;
    private string? filteredSearch;
    private bool favoritesSynced;

    private void SyncFavorites()
    {
        favorites.Clear();
        var stored = configuration.SkywatcherFavorites;
        for (var index = 0; index < stored.Count; index++)
        {
            favorites.Add(stored[index]);
        }

        favoritesSynced = true;
    }

    private void EnsureFavoritesSynced()
    {
        if (!favoritesSynced)
        {
            SyncFavorites();
        }
    }

    private void ToggleFavorite(uint territoryId)
    {
        EnsureFavoritesSynced();
        var scale = UiScale.Current;
        var visibleBefore = VisibleFavoriteCount();

        if (favorites.Remove(territoryId))
        {
            configuration.SkywatcherFavorites.Remove(territoryId);
            favoriteForecasts.Remove(territoryId);
        }
        else
        {
            favorites.Add(territoryId);
            configuration.SkywatcherFavorites.Add(territoryId);
            FavoriteForecast(territoryId);
        }

        configuration.Save();

        var visibleAfter = VisibleFavoriteCount();
        var delta = FavoritesSectionHeightFor(visibleAfter, scale) - FavoritesSectionHeightFor(visibleBefore, scale);
        if (delta != 0f)
        {
            ImGui.SetScrollY(ImGui.GetScrollY() + delta);
        }
    }

    private int VisibleFavoriteCount()
    {
        var stored = configuration.SkywatcherFavorites;
        var count = 0;
        for (var index = 0; index < stored.Count; index++)
        {
            if (TryResolveVisibleFavorite(stored[index], out _))
            {
                count++;
            }
        }

        return count;
    }

    private bool TryResolveVisibleFavorite(uint territoryId, out string zoneName)
    {
        if (territoryId == viewedTerritoryId)
        {
            zoneName = string.Empty;
            return false;
        }

        zoneName = weather.ZoneName(territoryId);
        return zoneName.Length > 0;
    }

    private static float FavoritesSectionHeightFor(int visibleCount, float scale)
    {
        if (visibleCount == 0)
        {
            return 0f;
        }

        var labelText = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Skywatcher.Favorites));
        var labelHeight = 12f * scale + Typography.Measure(labelText, TextStyles.FootnoteEmphasized).Y + 6f * scale;
        var cardsHeight = visibleCount * (FavoriteCardHeight * scale + 6f * scale);
        var itemGaps = ImGui.GetStyle().ItemSpacing.Y * (2 * visibleCount + 2);
        return labelHeight + cardsHeight + itemGaps;
    }

    private void RefreshRowWeather()
    {
        rowWeather.Clear();
        var regions = weather.ZonesByRegion();
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var zones = regions[regionIndex].Zones;
            for (var index = 0; index < zones.Count; index++)
            {
                var territoryId = zones[index].TerritoryId;
                if (!rowWeather.ContainsKey(territoryId))
                {
                    rowWeather[territoryId] = weather.Entry(weather.NaturalNow(territoryId));
                }
            }
        }
    }

    private WeatherEntry RowWeather(uint territoryId)
    {
        if (rowWeather.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var entry = weather.Entry(weather.NaturalNow(territoryId));
        rowWeather[territoryId] = entry;
        return entry;
    }

    private void RefreshFavoriteForecasts()
    {
        favoriteForecasts.Clear();
        var stored = configuration.SkywatcherFavorites;
        for (var index = 0; index < stored.Count; index++)
        {
            FavoriteForecast(stored[index]);
        }
    }

    private List<WeatherWindow> FavoriteForecast(uint territoryId)
    {
        if (favoriteForecasts.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var windows = new List<WeatherWindow>();
        weather.Forecast(territoryId, windows, PreviewStripCount);
        favoriteForecasts[territoryId] = windows;
        return windows;
    }

    private void DrawBrowse(in SkyPalette palette, float scale)
    {
        EnsureFavoritesSynced();
        DrawCurrentAreaPreview(palette, scale);
        DrawFavoritesSection(palette, scale);
        DrawSearchField(palette, scale);

        var regions = RefreshedFilteredRegions();
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            var visibleZones = CountOtherZones(region.Zones);
            if (visibleZones == 0)
            {
                continue;
            }

            SectionLabel(region.Region, palette, scale);
            DrawZoneList(palette, scale, region.Zones, visibleZones);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void DrawSearchField(in SkyPalette palette, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var bar = new Rect(origin, origin + new Vector2(width, SearchBarHeight * scale));
        SearchField.Draw(bar, "##skywatcherSearch", Loc.T(L.Common.Search), ref search, palette.Ink with { W = 0.12f },
            palette.InkFaint, palette.Ink, palette.Ink with { W = 0.18f }, palette.Ink, 60);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, SearchBarHeight * scale));
        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }

    private IReadOnlyList<WeatherRegionGroup> RefreshedFilteredRegions()
    {
        var regions = weather.ZonesByRegion();
        if (ReferenceEquals(filteredSourceRegions, regions) && filteredSearch == search)
        {
            return filteredRegions;
        }

        filteredRegions.Clear();
        var pooledMatches = 0;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            if (search.Length == 0 || region.Region.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                filteredRegions.Add(region);
                continue;
            }

            List<WeatherZoneEntry>? matches = null;
            var zones = region.Zones;
            for (var index = 0; index < zones.Count; index++)
            {
                if (zones[index].ZoneName.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    if (matches == null)
                    {
                        matches = RentMatchList(pooledMatches);
                        pooledMatches++;
                    }

                    matches.Add(zones[index]);
                }
            }

            if (matches != null)
            {
                filteredRegions.Add(new WeatherRegionGroup(region.Region, matches));
            }
        }

        filteredSourceRegions = regions;
        filteredSearch = search;
        return filteredRegions;
    }

    private List<WeatherZoneEntry> RentMatchList(int poolIndex)
    {
        while (filteredMatchesPool.Count <= poolIndex)
        {
            filteredMatchesPool.Add(new List<WeatherZoneEntry>());
        }

        var list = filteredMatchesPool[poolIndex];
        list.Clear();
        return list;
    }

    private void DrawFavoritesSection(in SkyPalette palette, float scale)
    {
        favoriteZones.Clear();
        var stored = configuration.SkywatcherFavorites;
        for (var index = 0; index < stored.Count; index++)
        {
            var territoryId = stored[index];
            if (TryResolveVisibleFavorite(territoryId, out var zoneName))
            {
                favoriteZones.Add(new WeatherZoneEntry(territoryId, zoneName));
            }
        }

        if (favoriteZones.Count == 0)
        {
            return;
        }

        SectionLabel(Loc.T(L.Skywatcher.Favorites), palette, scale);
        for (var index = 0; index < favoriteZones.Count; index++)
        {
            DrawFavoriteCard(palette, scale, favoriteZones[index]);
        }
    }

    private void DrawFavoriteCard(in SkyPalette palette, float scale, WeatherZoneEntry entry)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = FavoriteCardHeight * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        DrawGlass(card, palette, scale);
        var inner = card.Inset(12f * scale);

        var starRadius = ZoneStarRadius * scale;
        var starCenter = new Vector2(inner.Max.X - starRadius, inner.Min.Y + starRadius);
        var nameMaxWidth = MathF.Max(1f, inner.Width - starRadius * 2f - 10f * scale);
        var name = Typography.FitText(entry.ZoneName, nameMaxWidth, TextStyles.Headline);
        Typography.Draw(new Vector2(inner.Min.X, inner.Min.Y), name, palette.Ink, TextStyles.Headline);
        DrawFavoriteStar(ImGui.GetWindowDrawList(), starCenter, starRadius, palette,
            favorites.Contains(entry.TerritoryId));

        var windows = FavoriteForecast(entry.TerritoryId);
        if (windows.Count > 0)
        {
            var weatherLine = Typography.FitText(windows[0].Weather.Name, nameMaxWidth, TextStyles.Subheadline);
            Typography.Draw(new Vector2(inner.Min.X, inner.Min.Y + Typography.LineHeight(TextStyles.Headline)),
                weatherLine, palette.InkSoft, TextStyles.Subheadline);
            DrawWeatherStrip(inner, palette, scale, windows, FavoriteCurrentGlyphRadius,
                "skywatcher.favoriteStrip.", (long)entry.TerritoryId * 10);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.Dummy(new Vector2(0f, 6f * scale));

        var starMin = new Vector2(starCenter.X - starRadius - 8f * scale, card.Min.Y);
        var starMax = new Vector2(card.Max.X, starCenter.Y + starRadius + 8f * scale);
        var starHovered = UiInteract.Hover(starMin, starMax);
        if (starHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var starClicked = UiInteract.Click(starMin, starMax, starHovered);
        var cardClicked = !starHovered && UiInteract.HoverClick(card.Min, card.Max);
        if (starClicked)
        {
            ToggleFavorite(entry.TerritoryId);
        }
        else if (cardClicked)
        {
            OpenDetail(entry.TerritoryId);
        }
    }

    private void DrawCurrentAreaPreview(in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PreviewCardHeight * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        DrawGlass(card, palette, scale);
        var inner = card.Inset(14f * scale);
        var hasWeather = forecast.Count > 0;

        var name = Typography.FitText(zone, inner.Width, TextStyles.Headline);
        Typography.Draw(new Vector2(inner.Min.X, inner.Min.Y), name, palette.Ink, TextStyles.Headline);
        if (hasWeather)
        {
            var weatherLine = Typography.FitText(forecast[0].Weather.Name, inner.Width, TextStyles.Subheadline);
            Typography.Draw(new Vector2(inner.Min.X, inner.Min.Y + Typography.LineHeight(TextStyles.Headline)),
                weatherLine, palette.InkSoft, TextStyles.Subheadline);
            DrawWeatherStrip(inner, palette, scale, forecast, PreviewCurrentGlyphRadius, "skywatcher.preview.", 0);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (UiInteract.HoverClick(card.Min, card.Max))
        {
            OpenDetail(viewedTerritoryId);
        }
    }

    private void DrawWeatherStrip(Rect inner, in SkyPalette palette, float scale, IReadOnlyList<WeatherWindow> windows,
        float currentGlyphRadius, string marqueePrefix, long marqueeBase)
    {
        var count = Math.Min(windows.Count, PreviewStripCount);
        var columnWidth = inner.Width / count;
        var labelHeight = Typography.LineHeight(TextStyles.Caption2);
        var labelTop = inner.Max.Y - labelHeight;
        var glyphBottom = labelTop - 4f * scale;
        for (var index = 0; index < count; index++)
        {
            var window = windows[index];
            var columnCenterX = inner.Min.X + columnWidth * (index + 0.5f);
            var radius = (index == 0 ? currentGlyphRadius : PreviewGlyphRadius) * scale;
            var glyphCenter = new Vector2(columnCenterX, glyphBottom - radius);
            DrawMini(window, glyphCenter, radius);
            var columnMaxWidth = MathF.Max(1f, columnWidth - 4f * scale);
            Marquee.DrawCentered(new MarqueeId(marqueePrefix, marqueeBase + index), ShortWhen(window), columnCenterX,
                labelTop, columnMaxWidth, TextStyles.Caption2, palette.InkFaint, false);
        }
    }

    private void DrawZoneList(in SkyPalette palette, float scale, IReadOnlyList<WeatherZoneEntry> zones,
        int visibleCount)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = ZoneRowHeight * scale;
        var card = new Rect(origin, origin + new Vector2(width, visibleCount * rowHeight + 10f * scale));
        DrawGlass(card, palette, scale);
        var inner = card.Inset(5f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var rowIndex = 0;
        for (var index = 0; index < zones.Count; index++)
        {
            var entry = zones[index];
            if (entry.TerritoryId == viewedTerritoryId)
            {
                continue;
            }

            var rowTop = inner.Min.Y + rowIndex * rowHeight;
            var rowCenterY = rowTop + rowHeight * 0.5f;
            if (rowIndex > 0)
            {
                drawList.AddLine(new Vector2(inner.Min.X + 12f * scale, rowTop),
                    new Vector2(inner.Max.X - 10f * scale, rowTop), ImGui.GetColorU32(palette.Ink with { W = 0.10f }),
                    1f);
            }

            var starCenter = new Vector2(inner.Max.X - 10f * scale - ZoneStarRadius * scale, rowCenterY);
            var glyphCenter = new Vector2(starCenter.X - ZoneStarRadius * scale - 10f * scale - ZoneMiniGlyphRadius * scale,
                rowCenterY);

            var nameLeft = inner.Min.X + 12f * scale;
            var nameMaxWidth = MathF.Max(1f, glyphCenter.X - ZoneMiniGlyphRadius * scale - 10f * scale - nameLeft);
            var nameSize = Typography.Measure(entry.ZoneName, TextStyles.Body);
            Marquee.DrawLeftAuto(new MarqueeId("skywatcher.zone.", entry.TerritoryId), entry.ZoneName, nameLeft,
                rowCenterY - nameSize.Y * 0.5f, nameMaxWidth, TextStyles.Body, palette.Ink);

            var entryWeather = RowWeather(entry.TerritoryId);
            var nowWindow = new WeatherWindow(entryWeather, 0, true, 0);
            DrawMini(nowWindow, glyphCenter, ZoneMiniGlyphRadius * scale);
            DrawFavoriteStar(drawList, starCenter, ZoneStarRadius * scale, palette,
                favorites.Contains(entry.TerritoryId));

            var starMin = new Vector2(starCenter.X - ZoneStarRadius * scale - 8f * scale, rowTop);
            var starMax = new Vector2(inner.Max.X, rowTop + rowHeight);
            var starHovered = UiInteract.Hover(starMin, starMax);
            if (starHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var starClicked = UiInteract.Click(starMin, starMax, starHovered);
            var rowClicked = !starHovered &&
                UiInteract.HoverClick(new Vector2(inner.Min.X, rowTop), new Vector2(inner.Max.X, rowTop + rowHeight));
            if (starClicked)
            {
                ToggleFavorite(entry.TerritoryId);
            }
            else if (rowClicked)
            {
                OpenDetail(entry.TerritoryId);
            }

            rowIndex++;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height));
    }

    private int CountOtherZones(IReadOnlyList<WeatherZoneEntry> zones)
    {
        var count = 0;
        for (var index = 0; index < zones.Count; index++)
        {
            if (zones[index].TerritoryId != viewedTerritoryId)
            {
                count++;
            }
        }

        return count;
    }

    private static void DrawFavoriteStar(ImDrawListPtr drawList, Vector2 center, float radius, in SkyPalette palette,
        bool filled)
    {
        MapGlyphs.Star(drawList, center, radius, filled, MapGlyphs.FavoriteFill, palette.InkFaint, UiScale.Current);
    }
}
