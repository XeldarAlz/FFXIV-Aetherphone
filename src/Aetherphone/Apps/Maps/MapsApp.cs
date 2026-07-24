using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Maps;

internal sealed class MapsApp : IPhoneApp
{
    private const float SearchHeight = 46f;
    private const float LocationCardHeight = 64f;
    private const float DestinationRowHeight = 50f;
    private const float ExpansionHeaderHeight = 40f;
    private static readonly Vector4 MapAccent = AppAccents.For("maps");
    private static readonly Vector4 FavoriteStar = new(1f, 0.78f, 0.25f, 1f);
    public string Id => "maps";
    public string DisplayName => Loc.T(L.Apps.Maps);
    public string Glyph => "Ma";
    public int BadgeCount => 0;
    private readonly MapData maps;
    private readonly Configuration configuration;
    private readonly List<MapAetheryte> favoriteDestinations = new();
    private readonly List<MapAetheryte> searchResults = new();
    private readonly HashSet<uint> favorites = new();
    private readonly HashSet<byte> expandedExpansions = new();
    private string search = string.Empty;
    private bool lifestreamAvailable;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public MapsApp(MapData maps, Configuration configuration)
    {
        this.maps = maps;
        this.configuration = configuration;
    }

    public void OnOpened()
    {
        search = string.Empty;
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        SyncFavorites();
    }

    public void OnClosed()
    {
        search = string.Empty;
    }

    private void SyncFavorites()
    {
        favorites.Clear();
        var stored = configuration.MapFavorites;
        for (var index = 0; index < stored.Count; index++)
        {
            favorites.Add(stored[index]);
        }
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        SceneCompositor.DrawLayer(context.Content,
            new SceneCompositor.Layer("maps", Vector2.Zero, 0f, DrawRoot, context.Theme.AppBackground));
    }

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, top),
            new Vector2(area.Max.X - pad, top + SearchHeight * scale));
        UiAnchors.Report("maps.search", searchBar);
        SearchField.Draw(searchBar, "##mapsSearch", Loc.T(L.Maps.Search), ref search, frameTheme, 60);
        var body = new Rect(new Vector2(area.Min.X, searchBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            if (search.Length > 0)
            {
                DrawSearchResults();
            }
            else
            {
                DrawLocationCard();
                DrawFavorites();
                DrawExpansions();
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawLocationCard()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var location = maps.CurrentLocation();
        var zoneName = location.Zone.Length > 0 ? location.Zone : Loc.T(L.Maps.Unknown);
        var regionName = location.Region.Length > 0 ? location.Region : Loc.T(L.Maps.Unknown);
        SettingsSection.Header(Loc.T(L.Maps.CurrentLocation), frameTheme);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cardMin = origin;
        var cardMax = new Vector2(origin.X + width, origin.Y + LocationCardHeight * scale);
        UiAnchors.Report("maps.location", new Rect(cardMin, cardMax));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, cardMin, cardMax, 14f * scale, ImGui.GetColorU32(frameTheme.GroupedCard));
        Material.EdgeSquircle(drawList, cardMin, cardMax, 14f * scale, scale);
        var pinCenter = new Vector2(cardMin.X + 28f * scale, cardMin.Y + LocationCardHeight * scale * 0.5f);
        MapGlyphs.Pin(drawList, pinCenter, 12f * scale, frameTheme.Accent);
        var textLeft = pinCenter.X + 24f * scale;
        var textMaxWidth = MathF.Max(1f, cardMax.X - 14f * scale - textLeft);
        var zoneY = cardMin.Y + 14f * scale;
        var zoneSize = Typography.Measure(zoneName, TextStyles.Headline);
        var zoneHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, zoneY),
            new Vector2(textLeft + textMaxWidth, zoneY + zoneSize.Y));
        Marquee.DrawLeft("maps.location.zone", zoneName, textLeft, zoneY, textMaxWidth,
            TextStyles.Headline, frameTheme.TextStrong, zoneHovering);
        var regionY = cardMin.Y + 36f * scale;
        var regionSize = Typography.Measure(regionName, TextStyles.Footnote);
        var regionHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, regionY),
            new Vector2(textLeft + textMaxWidth, regionY + regionSize.Y));
        Marquee.DrawLeft("maps.location.region", regionName, textLeft, regionY, textMaxWidth,
            TextStyles.Footnote, frameTheme.TextMuted, regionHovering);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, LocationCardHeight * scale + 4f * scale));
    }

    private void DrawFavorites()
    {
        favoriteDestinations.Clear();
        var stored = configuration.MapFavorites;
        for (var index = 0; index < stored.Count; index++)
        {
            if (maps.TryGetAetheryte(stored[index], out var aetheryte))
            {
                favoriteDestinations.Add(aetheryte);
            }
        }

        if (favoriteDestinations.Count == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Maps.Favorites), frameTheme);
        var card = GroupCard.Begin(frameTheme, favoriteDestinations.Count, DestinationRowHeight);
        for (var index = 0; index < favoriteDestinations.Count; index++)
        {
            DrawDestinationRow(card.NextRow(), favoriteDestinations[index]);
        }

        card.End();
    }

    private void DrawExpansions()
    {
        var expansions = maps.Expansions;
        if (expansions.Count == 0)
        {
            DrawEmptyState(Loc.T(L.Maps.NoZones));
            return;
        }

        for (var expansionIndex = 0; expansionIndex < expansions.Count; expansionIndex++)
        {
            var expansion = expansions[expansionIndex];
            var expanded = expandedExpansions.Contains(expansion.Order);
            if (DrawExpansionHeader(expansion, expanded))
            {
                if (expanded)
                {
                    expandedExpansions.Remove(expansion.Order);
                }
                else
                {
                    expandedExpansions.Add(expansion.Order);
                }

                expanded = !expanded;
            }

            if (!expanded)
            {
                continue;
            }

            var expansionRegions = expansion.Regions;
            for (var regionIndex = 0; regionIndex < expansionRegions.Count; regionIndex++)
            {
                DrawRegion(expansionRegions[regionIndex]);
            }
        }
    }

    private void DrawRegion(MapRegion region)
    {
        var destinations = region.Aetherytes;
        if (destinations.Count == 0)
        {
            return;
        }

        SettingsSection.Header(region.Name, frameTheme);
        var card = GroupCard.Begin(frameTheme, destinations.Count, DestinationRowHeight);
        for (var index = 0; index < destinations.Count; index++)
        {
            DrawDestinationRow(card.NextRow(), destinations[index]);
        }

        card.End();
    }

    private void DrawSearchResults()
    {
        CollectSearchResults();
        if (searchResults.Count == 0)
        {
            DrawEmptyState(Loc.T(L.Maps.NoZones));
            return;
        }

        var card = GroupCard.Begin(frameTheme, searchResults.Count, DestinationRowHeight);
        for (var index = 0; index < searchResults.Count; index++)
        {
            DrawDestinationRow(card.NextRow(), searchResults[index]);
        }

        card.End();
    }

    private void CollectSearchResults()
    {
        searchResults.Clear();
        var regions = maps.Regions;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var destinations = regions[regionIndex].Aetherytes;
            for (var index = 0; index < destinations.Count; index++)
            {
                var aetheryte = destinations[index];
                if (aetheryte.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    searchResults.Add(aetheryte);
                }
            }
        }
    }

    private bool DrawExpansionHeader(MapExpansion expansion, bool expanded)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ExpansionHeaderHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var drawList = ImGui.GetWindowDrawList();
        if (hovered)
        {
            MapGlyphs.Highlight(new Rect(min, max), Palette.WithAlpha(frameTheme.TextStrong, 0.06f), 0f, scale);
        }

        var disclosureCenter = new Vector2(min.X + 19f * scale, min.Y + height * 0.5f);
        var ink = hovered ? frameTheme.TextStrong : frameTheme.TextMuted;
        MapGlyphs.Disclosure(drawList, disclosureCenter, 5f * scale, 2.2f * scale, expanded, ink);
        var titleLeft = disclosureCenter.X + 16f * scale;
        var titleMaxWidth = MathF.Max(1f, max.X - 16f * scale - titleLeft);
        var titleText = Typography.FitText(expansion.Name, titleMaxWidth, TextStyles.Headline);
        var titleSize = Typography.Measure(titleText, TextStyles.Headline);
        Typography.Draw(new Vector2(titleLeft, min.Y + height * 0.5f - titleSize.Y * 0.5f),
            titleText, frameTheme.TextStrong, TextStyles.Headline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void DrawDestinationRow(Rect row, MapAetheryte aetheryte)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var starRadius = 9f * scale;
        var starCenter = new Vector2(row.Min.X + starRadius, row.Center.Y);
        var starMin = new Vector2(row.Min.X, row.Min.Y);
        var starMax = new Vector2(starCenter.X + starRadius + 6f * scale, row.Max.Y);
        var starHovered = UiInteract.Hover(starMin, starMax);
        var rowHovered = UiInteract.Hover(row.Min, row.Max);
        var actionHovered = rowHovered && !starHovered;
        if (actionHovered)
        {
            MapGlyphs.Highlight(row, Palette.WithAlpha(frameTheme.Accent, 0.16f), 3f, scale);
        }

        var isFavorite = favorites.Contains(aetheryte.RowId);
        MapGlyphs.Star(drawList, starCenter, starRadius, isFavorite, FavoriteStar,
            Palette.WithAlpha(frameTheme.TextMuted, 0.6f), scale);
        var textLeft = starCenter.X + starRadius + 12f * scale;
        var textRight = row.Max.X - 14f * scale;
        var textMaxWidth = MathF.Max(1f, textRight - textLeft);
        var labelSize = Typography.Measure(aetheryte.Name, TextStyles.Body);
        var textHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, row.Min.Y), new Vector2(textRight, row.Max.Y));
        Marquee.DrawLeft("maps.destination." + aetheryte.RowId, aetheryte.Name, textLeft,
            row.Center.Y - labelSize.Y * 0.5f, textMaxWidth, TextStyles.Body, frameTheme.TextStrong, textHovering);
        var arrowTip = new Vector2(row.Max.X, row.Center.Y);
        MapGlyphs.ChevronRight(arrowTip, 6f * scale, 2.2f * scale,
            actionHovered ? frameTheme.Accent : frameTheme.TextMuted);
        var starClicked = UiInteract.Click(starMin, starMax, starHovered);
        var rowClicked = !starHovered && UiInteract.Click(row.Min, row.Max, rowHovered);
        if (starHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        else if (rowHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (!lifestreamAvailable)
            {
                HoverTooltip.Show(row, Loc.T(L.Maps.NeedsLifestream), HoverLabelSide.Above);
            }
        }

        if (starClicked)
        {
            ToggleFavorite(aetheryte.RowId);
            return;
        }

        if (rowClicked)
        {
            Teleport(aetheryte);
        }
    }

    private void Teleport(MapAetheryte aetheryte)
    {
        if (lifestreamAvailable)
        {
            LifestreamBridge.TravelToAetheryte(aetheryte.Name);
        }
        else
        {
            ImGui.SetClipboardText(LifestreamBridge.AetheryteCommand(aetheryte.Name));
        }
    }

    private void ToggleFavorite(uint rowId)
    {
        if (favorites.Remove(rowId))
        {
            configuration.MapFavorites.Remove(rowId);
        }
        else
        {
            favorites.Add(rowId);
            configuration.MapFavorites.Add(rowId);
        }

        configuration.Save();
    }

    private void DrawEmptyState(string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var maxWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X - 8f * scale);
        var clipped = Typography.FitText(message, maxWidth, TextStyles.Footnote);
        Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 16f * scale), clipped,
            frameTheme.TextMuted, TextStyles.Footnote);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 40f * scale));
    }

    public void Dispose()
    {
    }
}
