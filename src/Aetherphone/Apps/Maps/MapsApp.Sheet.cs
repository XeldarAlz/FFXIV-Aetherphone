using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Maps;

internal sealed partial class MapsApp
{
    private const float HeaderBottomPad = 12f;
    private const float PlaceHeaderHeight = 62f;
    private const float LocationCardHeight = 76f;
    private const float LocationTileSize = 42f;
    private const float LocationGlyphSize = 20f;
    private const float CardTextGap = 12f;
    private const float FavoriteTileWidth = 72f;
    private const float FavoriteDiscSize = 56f;
    private const float FavoriteIconSize = 30f;
    private const float FavoriteGlyphSize = 22f;
    private const float RailGap = 12f;
    private const float RailLabelGap = 6f;
    private const float ExpansionCardWidth = 164f;
    private const float ExpansionCardHeight = 98f;
    private const float ExpansionCardPadding = 14f;
    private const int RecentsShown = 5;
    private const float ContentBottomPad = 24f;
    private const float SectionGap = 6f;

    private readonly PanRail favoritesRail = new();
    private readonly PanRail browseRail = new();
    private readonly List<MapAetheryte> favoriteList = new();
    private readonly List<MapAetheryte> recentList = new();
    private bool favoritesDirty = true;
    private bool recentsDirty = true;
    private bool focusSearch;
    private bool searchWasActive;

    private float HeaderHeight(float scale) => Page == MapsPage.Place
        ? (SheetMetrics.GrabberZone + PlaceHeaderHeight + HeaderBottomPad) * scale
        : (SheetMetrics.GrabberZone + GlassField.HeightUnits + HeaderBottomPad) * scale;

    private void DrawDrawer(ImDrawListPtr drawList, Rect screen, Rect panel, float headerHeight, float scale)
    {
        MapDrawer.Draw(drawList, panel, theme, scale);
        var header = new Rect(panel.Min, new Vector2(panel.Max.X, panel.Min.Y + headerHeight));
        lastHeader = header;
        lastField = default;
        var page = Page;
        drawList.PushClipRect(panel.Min, panel.Max, true);
        switch (page)
        {
            case MapsPage.Expansion:
                DrawExpansionHeader(drawList, header, scale);
                break;
            case MapsPage.Place:
                DrawPlaceHeader(drawList, header, scale);
                break;
            default:
                DrawSearchHeader(drawList, header, scale);
                break;
        }

        drawList.PopClipRect();
        BlockHeader(header);
        var content = new Rect(new Vector2(panel.Min.X, header.Max.Y), screen.Max);
        if (content.Height <= (theme.BottomZoneHeight + Metrics.Space.Lg) * scale)
        {
            return;
        }

        contentClip = content;
        using (ImRaii.PushId((int)page))
        using (ImRaii.PushId(page == MapsPage.Place && openPlace is not null ? (int)openPlace.RowId : openExpansion))
        using (AppSurface.Begin(content))
        {
            switch (page)
            {
                case MapsPage.Expansion:
                    DrawExpansionPage(scale);
                    break;
                case MapsPage.Place:
                    DrawPlacePage(scale);
                    break;
                default:
                    DrawHomePage(scale);
                    break;
            }

            ImGui.Dummy(new Vector2(0f, (ContentBottomPad + theme.BottomZoneHeight) * scale));
        }
    }

    private static void BlockHeader(Rect header)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(header.Min);
        ImGui.InvisibleButton("##mapsDrawerHeader", header.Size, ImGuiButtonFlags.MouseButtonLeft);
        ImGui.SetCursorScreenPos(cursor);
    }

    private void DrawSearchHeader(ImDrawListPtr drawList, Rect header, float scale)
    {
        var inset = Metrics.Space.Lg * scale;
        var top = header.Min.Y + SheetMetrics.GrabberZone * scale;
        var field = new Rect(new Vector2(header.Min.X + inset, top),
            new Vector2(header.Max.X - inset, top + GlassField.HeightUnits * scale));
        lastField = field;
        UiAnchors.Report("maps.search", field);
        Material.ThemedGlass(drawList, field.Min, field.Max, GlassField.Radius(field), scale, theme);
        GlassField.Search(drawList, field, "##mapsSearch", Loc.T(L.Maps.Search), ref search, theme, scale,
            SearchMaxLength, focusSearch);
        var active = ImGui.IsItemActive();
        focusSearch = false;
        if (active && !searchWasActive && drawer.Detent != MapDrawerDetent.Large)
        {
            drawer.SetDetent(MapDrawerDetent.Large);
        }

        searchWasActive = active;
        RefreshSearch();
    }

    private void DrawHomePage(float scale)
    {
        if (searchQuery.Length > 0)
        {
            DrawSearchResults(scale);
            return;
        }

        DrawLocationCard(scale);
        DrawFavorites(scale);
        DrawBrowse(scale);
        DrawRecents(scale);
    }

    private void DrawLocationCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(theme, 1, LocationCardHeight);
        var row = card.NextRow();
        var bounds = card.Bounds;
        ReportVisible("maps.location", bounds);
        var tile = LocationTileSize * scale;
        var tileMin = new Vector2(row.Min.X, row.Center.Y - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(accent));
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f), LocationHero.GlyphFor(location.Kind),
            AccentRing.Ink, LocationGlyphSize * scale);
        var buttonRadius = MapChrome.CircleSize * 0.5f * scale;
        var hasActions = location.IsKnown;
        var copyCenter = new Vector2(row.Max.X - buttonRadius, row.Center.Y);
        var mapCenter = new Vector2(copyCenter.X - buttonRadius * 2f - Metrics.Space.Sm * scale, row.Center.Y);
        var textLeft = tileMin.X + tile + CardTextGap * scale;
        var textRight = hasActions ? mapCenter.X - buttonRadius - Metrics.Space.Sm * scale : row.Max.X;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var titleHeight = Typography.Measure(location.Title, TextStyles.Headline).Y;
        var subtitleHeight = Typography.Measure(location.Subtitle, TextStyles.Subheadline).Y;
        var textTop = row.Center.Y - (titleHeight + Metrics.Space.Xxs * scale + subtitleHeight) * 0.5f;
        var cardHovered = UiInteract.Hover(bounds.Min, bounds.Max);
        Marquee.DrawLeft(drawList, "maps.location.title", location.Title, textLeft, textTop, textWidth,
            TextStyles.Headline, theme.TextStrong, cardHovered);
        Marquee.DrawLeft(drawList, "maps.location.subtitle", location.Subtitle, textLeft,
            textTop + titleHeight + Metrics.Space.Xxs * scale, textWidth, TextStyles.Subheadline, theme.TextMuted,
            cardHovered);
        var overButtons = false;
        if (hasActions)
        {
            var hit = new Vector2(MapChrome.ControlSize * 0.5f * scale, MapChrome.ControlSize * 0.5f * scale);
            overButtons = UiInteract.Hover(mapCenter - hit, copyCenter + hit);
            if (MapChrome.Circle(drawList, "maps.location.map", mapCenter, PhoneIcons.Compass, theme.Accent, theme,
                    scale, Loc.T(L.Maps.GameMap), false))
            {
                OpenCurrentGameMap();
            }

            if (MapChrome.Circle(drawList, "maps.location.copy", copyCenter, PhoneIcons.Copy, theme.Accent, theme,
                    scale, Loc.T(L.Maps.CopyLocation), false))
            {
                CopyCurrentLocation();
            }
        }

        var canRecenter = mode != StageMode.Hero;
        if (canRecenter && cardHovered && !overButtons)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (canRecenter && !overButtons && UiInteract.Click(bounds.Min, bounds.Max, cardHovered))
        {
            UiFeedback.Play(UiSound.Tap);
            camera.Recenter();
            drawer.SetDetent(MapDrawerDetent.Peek);
        }

        card.End();
    }

    private void OpenCurrentGameMap()
    {
        if (LocationShare.Capture() is { } captured)
        {
            OpenGameMap(captured.TerritoryId, captured.MapId, captured.MapX, captured.MapY);
        }
    }

    private void CopyCurrentLocation()
    {
        var text = mode == StageMode.Live && reader.Coordinates.Length > 0
            ? string.Concat(location.Title, " (", reader.Coordinates, ")")
            : location.Title;
        ImGui.SetClipboardText(text);
        UiFeedback.Play(UiSound.Tap);
        ShellToast.Show();
    }

    private void DrawFavorites(float scale)
    {
        RebuildFavorites();
        ListSection.Header(Loc.T(L.Maps.Favorites), theme.TextMuted);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var labelHeight = Typography.Measure(Loc.T(L.Maps.AddFavorite), TextStyles.Footnote).Y;
        var tileWidth = FavoriteTileWidth * scale;
        var disc = FavoriteDiscSize * scale;
        var height = disc + RailLabelGap * scale + labelHeight;
        var rail = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var count = favoriteList.Count + 1;
        var gap = RailGap * scale;
        favoritesRail.Begin(rail, count * tileWidth + (count - 1) * gap);
        for (var index = 0; index < count; index++)
        {
            var left = rail.Min.X - favoritesRail.Offset + index * (tileWidth + gap);
            if (left > rail.Max.X || left + tileWidth < rail.Min.X)
            {
                continue;
            }

            var tileRect = new Rect(new Vector2(left, rail.Min.Y), new Vector2(left + tileWidth, rail.Max.Y));
            if (index < favoriteList.Count)
            {
                DrawFavoriteTile(drawList, tileRect, favoriteList[index], disc, scale);
            }
            else
            {
                DrawAddFavoriteTile(drawList, tileRect, disc, scale);
            }
        }

        favoritesRail.End();
        if (favoriteList.Count == 0)
        {
            var hintLeft = rail.Min.X + tileWidth + gap;
            var hintWidth = MathF.Max(1f, rail.Max.X - hintLeft);
            var hintHeight = Typography.MeasureWrappedBlock(Loc.T(L.Maps.FavoritesHint), TextStyles.Subheadline,
                hintWidth).Y;
            Typography.DrawWrappedLeft(new Vector2(hintLeft, rail.Min.Y + (disc - hintHeight) * 0.5f),
                Loc.T(L.Maps.FavoritesHint), theme.TextMuted, TextStyles.Subheadline, hintWidth);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + SectionGap * scale));
    }

    private void DrawFavoriteTile(ImDrawListPtr drawList, Rect tile, MapAetheryte aetheryte, float disc, float scale)
    {
        var center = new Vector2(tile.Center.X, tile.Min.Y + disc * 0.5f);
        var half = new Vector2(disc * 0.5f, disc * 0.5f);
        var hovered = favoritesRail.Hover(center - half, center + half);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(unchecked(ImGui.GetID("maps.favorite") + aetheryte.RowId), down,
            PressFx.IconPressedScale);
        var radius = disc * 0.5f * grow;
        var surface = IconTile.Surface(accent);
        Squircle.FillCircleVerticalGradient(drawList, center, radius,
            ImGui.GetColorU32(Palette.Lighten(surface, 0.10f) with { W = 1f }),
            ImGui.GetColorU32(Palette.Darken(surface, 0.14f) with { W = 1f }));
        var icon = FavoriteIconSize * 0.5f * scale * grow;
        GameIconTile.Draw(drawList, Plugin.TextureProvider, MapCanvas.AetheryteIconId,
            center - new Vector2(icon, icon), center + new Vector2(icon, icon), icon, scale);
        var label = Typography.FitText(aetheryte.Name, tile.Width, TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(tile.Center.X - labelSize.X * 0.5f,
            tile.Min.Y + disc + RailLabelGap * scale), label, theme.TextStrong, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(center - half, center + half), aetheryte.Subtitle, HoverLabelSide.Above);
        }

        if (favoritesRail.Tapped(center - half, center + half, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            OpenPlace(aetheryte);
        }
    }

    private void DrawAddFavoriteTile(ImDrawListPtr drawList, Rect tile, float disc, float scale)
    {
        var center = new Vector2(tile.Center.X, tile.Min.Y + disc * 0.5f);
        var half = new Vector2(disc * 0.5f, disc * 0.5f);
        var hovered = favoritesRail.Hover(center - half, center + half);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale("maps.favorite.add", down, PressFx.IconPressedScale);
        var fill = hovered ? Palette.Mix(theme.SurfaceMuted, theme.TextStrong, 0.10f) : theme.SurfaceMuted;
        drawList.AddCircleFilled(center, disc * 0.5f * grow, ImGui.GetColorU32(fill), 40);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Plus, theme.Accent, FavoriteGlyphSize * scale * grow);
        var label = Typography.FitText(Loc.T(L.Maps.AddFavorite), tile.Width, TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(tile.Center.X - labelSize.X * 0.5f,
            tile.Min.Y + disc + RailLabelGap * scale), label, theme.TextStrong, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (favoritesRail.Tapped(center - half, center + half, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            focusSearch = true;
            drawer.SetDetent(MapDrawerDetent.Large);
        }
    }

    private void RebuildFavorites()
    {
        if (!favoritesDirty)
        {
            return;
        }

        favoritesDirty = false;
        favoriteList.Clear();
        var stored = configuration.MapFavorites;
        for (var index = 0; index < stored.Count; index++)
        {
            if (maps.TryGetAetheryte(stored[index], out var aetheryte))
            {
                favoriteList.Add(aetheryte);
            }
        }
    }

    private void DrawBrowse(float scale)
    {
        var expansions = maps.Expansions;
        if (expansions.Count == 0)
        {
            return;
        }

        ListSection.Header(Loc.T(L.Maps.Browse), theme.TextMuted);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cardWidth = ExpansionCardWidth * scale;
        var height = ExpansionCardHeight * scale;
        var gap = RailGap * scale;
        var rail = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        browseRail.Begin(rail, expansions.Count * cardWidth + (expansions.Count - 1) * gap);
        for (var index = 0; index < expansions.Count; index++)
        {
            var left = rail.Min.X - browseRail.Offset + index * (cardWidth + gap);
            var cardRect = new Rect(new Vector2(left, rail.Min.Y), new Vector2(left + cardWidth, rail.Max.Y));
            if (index == 0)
            {
                ReportVisible("maps.expansion.first", new Rect(
                    new Vector2(MathF.Max(cardRect.Min.X, rail.Min.X), cardRect.Min.Y),
                    new Vector2(MathF.Min(cardRect.Max.X, rail.Max.X), cardRect.Max.Y)));
            }

            if (left > rail.Max.X || left + cardWidth < rail.Min.X)
            {
                continue;
            }

            DrawExpansionCard(drawList, cardRect, expansions[index], scale);
        }

        browseRail.End();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + SectionGap * scale));
    }

    private void DrawExpansionCard(ImDrawListPtr drawList, Rect card, MapExpansion expansion, float scale)
    {
        var hovered = browseRail.Hover(card.Min, card.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(unchecked(ImGui.GetID("maps.expansion") + expansion.Order), down,
            PressFx.CardPressedScale);
        var half = card.Size * 0.5f * grow;
        var min = card.Center - half;
        var max = card.Center + half;
        var radius = Metrics.Radius.Lg * scale * grow;
        var tint = MapGlyphs.ExpansionTint(expansion.Order, accent);
        Elevation.Card(drawList, min, max, radius, scale);
        IconTile.FillShaded(drawList, min, max, radius, hovered ? Palette.Lighten(tint, 0.06f) : tint);
        var padding = ExpansionCardPadding * scale;
        var textWidth = MathF.Max(1f, max.X - min.X - padding * 2f);
        Typography.DrawWrappedLeft(new Vector2(min.X + padding, min.Y + padding), expansion.Name, AccentRing.Ink,
            TextStyles.Headline, textWidth);
        var summary = Typography.FitText(expansion.Summary, textWidth, TextStyles.Footnote);
        var summarySize = Typography.Measure(summary, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(min.X + padding, max.Y - padding - summarySize.Y), summary,
            Palette.WithAlpha(AccentRing.Ink, 0.82f), TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (browseRail.Tapped(card.Min, card.Max, hovered))
        {
            OpenExpansion(expansion);
        }
    }

    private void DrawRecents(float scale)
    {
        RebuildRecents();
        if (recentList.Count == 0)
        {
            return;
        }

        ListSection.Header(Loc.T(L.Maps.Recents), theme.TextMuted);
        var card = GroupCard.Begin(theme, recentList.Count, PlaceRowHeight);
        card.SeparatorInset = PlaceRowInset;
        for (var index = 0; index < recentList.Count; index++)
        {
            DrawPlaceRow(card.NextRow(), recentList[index], PhoneIcons.Clock, default, false, scale, card.Bounds);
        }

        card.End();
    }

    private void RebuildRecents()
    {
        if (!recentsDirty)
        {
            return;
        }

        recentsDirty = false;
        recentList.Clear();
        var stored = configuration.MapRecents;
        for (var index = 0; index < stored.Count && recentList.Count < RecentsShown; index++)
        {
            if (maps.TryGetAetheryte(stored[index], out var aetheryte))
            {
                recentList.Add(aetheryte);
            }
        }
    }
}
