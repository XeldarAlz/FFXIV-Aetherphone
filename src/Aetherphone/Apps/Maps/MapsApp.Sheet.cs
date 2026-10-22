using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal sealed partial class MapsApp
{
    private const float HeaderBottomPad = 12f;
    private const float PlaceHeaderHeight = 62f;
    private const float LocationCardHeight = 76f;
    private const float LocationTileSize = 42f;
    private const float LocationGlyphSize = 20f;
    private const float CardTextGap = 12f;
    private const float FavoriteCellWidth = 72f;
    private const float FavoriteDiscSize = 52f;
    private const float FavoriteIconSize = 28f;
    private const float FavoriteGlyphSize = 20f;
    private const float FavoriteRowGap = 14f;
    private const float FavoriteLabelGap = 6f;
    private const float FavoriteCardPadding = 14f;
    private const int BrowseColumns = 2;
    private const float BrowseRowHeight = 52f;
    private const float BrowseColumnGap = 16f;
    private const float BrowseDiscSize = 28f;
    private const float BrowseGlyphSize = 14f;
    private const int RecentsShown = 5;
    private const float ContentBottomPad = 24f;
    private const float SectionGap = 6f;

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
        using (UiScope.PushId((int)page))
        using (UiScope.PushId(page == MapsPage.Place && openPlace is not null ? (int)openPlace.RowId : openExpansion))
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
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
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
        var innerWidth = ImGui.GetContentRegionAvail().X - Metrics.Space.Lg * 2f * scale;
        var columns = Math.Max(1, (int)(innerWidth / (FavoriteCellWidth * scale)));
        var cellCount = favoriteList.Count + 1;
        var rows = (cellCount + columns - 1) / columns;
        var labelUnits = Typography.Measure(Loc.T(L.Maps.AddFavorite), TextStyles.Footnote).Y / scale;
        var cellUnits = FavoriteDiscSize + FavoriteLabelGap + labelUnits;
        var cardUnits = rows * cellUnits + (rows - 1) * FavoriteRowGap + FavoriteCardPadding * 2f;
        var card = GroupCard.Begin(theme, cardUnits);
        var grid = card.NextRow(cardUnits);
        var cellWidth = grid.Width / columns;
        var cellHeight = cellUnits * scale;
        var top = grid.Min.Y + FavoriteCardPadding * scale;
        var disc = FavoriteDiscSize * scale;
        for (var index = 0; index < cellCount; index++)
        {
            var cellMin = new Vector2(grid.Min.X + index % columns * cellWidth,
                top + index / columns * (cellUnits + FavoriteRowGap) * scale);
            var cell = new Rect(cellMin, cellMin + new Vector2(cellWidth, cellHeight));
            if (index < favoriteList.Count)
            {
                DrawFavoriteTile(drawList, cell, favoriteList[index], disc, scale);
            }
            else
            {
                DrawAddFavoriteTile(drawList, cell, disc, scale);
            }
        }

        if (favoriteList.Count == 0 && columns > 1)
        {
            var hintLeft = grid.Min.X + cellWidth + Metrics.Space.Sm * scale;
            var hintWidth = MathF.Max(1f, grid.Max.X - hintLeft);
            var hintHeight = Typography.MeasureWrappedBlock(Loc.T(L.Maps.FavoritesHint), TextStyles.Subheadline,
                hintWidth).Y;
            Typography.DrawWrappedLeft(new Vector2(hintLeft, top + (cellHeight - hintHeight) * 0.5f),
                Loc.T(L.Maps.FavoritesHint), theme.TextMuted, TextStyles.Subheadline, hintWidth);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, SectionGap * scale));
    }

    private void DrawFavoriteTile(ImDrawListPtr drawList, Rect cell, MapAetheryte aetheryte, float disc, float scale)
    {
        var center = new Vector2(cell.Center.X, cell.Min.Y + disc * 0.5f);
        var hovered = UiInteract.Hover(cell.Min, cell.Max);
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
        DrawFavoriteLabel(drawList, cell, aetheryte.Name, disc, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(cell, aetheryte.Subtitle, HoverLabelSide.Above);
        }

        if (UiInteract.Click(cell.Min, cell.Max, hovered))
        {
            OpenPlace(aetheryte);
        }
    }

    private void DrawAddFavoriteTile(ImDrawListPtr drawList, Rect cell, float disc, float scale)
    {
        var center = new Vector2(cell.Center.X, cell.Min.Y + disc * 0.5f);
        var hovered = UiInteract.Hover(cell.Min, cell.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale("maps.favorite.add", down, PressFx.IconPressedScale);
        var fill = hovered ? Palette.Mix(theme.SurfaceMuted, theme.TextStrong, 0.10f) : theme.SurfaceMuted;
        drawList.AddCircleFilled(center, disc * 0.5f * grow, ImGui.GetColorU32(fill), 40);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Plus, theme.Accent, FavoriteGlyphSize * scale * grow);
        DrawFavoriteLabel(drawList, cell, Loc.T(L.Maps.AddFavorite), disc, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(cell.Min, cell.Max, hovered))
        {
            focusSearch = true;
            drawer.SetDetent(MapDrawerDetent.Large);
        }
    }

    private void DrawFavoriteLabel(ImDrawListPtr drawList, Rect cell, string text, float disc, float scale)
    {
        var label = Typography.FitText(text, cell.Width - Metrics.Space.Xxs * 2f * scale, TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(cell.Center.X - labelSize.X * 0.5f,
            cell.Min.Y + disc + FavoriteLabelGap * scale), label, theme.TextStrong, TextStyles.Footnote);
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
        var rows = (expansions.Count + BrowseColumns - 1) / BrowseColumns;
        var cardUnits = rows * BrowseRowHeight;
        var card = GroupCard.Begin(theme, cardUnits);
        var grid = card.NextRow(cardUnits);
        var gap = BrowseColumnGap * scale;
        var cellWidth = (grid.Width - gap * (BrowseColumns - 1)) / BrowseColumns;
        var cellHeight = BrowseRowHeight * scale;
        for (var index = 0; index < expansions.Count; index++)
        {
            var row = index / BrowseColumns;
            var cellMin = new Vector2(grid.Min.X + index % BrowseColumns * (cellWidth + gap),
                grid.Min.Y + row * cellHeight);
            var cell = new Rect(cellMin, cellMin + new Vector2(cellWidth, cellHeight));
            if (index == 0)
            {
                ReportVisible("maps.expansion.first", cell);
            }

            DrawExpansionCell(drawList, cell, expansions[index], row > 0, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, SectionGap * scale));
    }

    private void DrawExpansionCell(ImDrawListPtr drawList, Rect cell, MapExpansion expansion, bool separated,
        float scale)
    {
        var hovered = UiInteract.Hover(cell.Min, cell.Max);
        if (hovered)
        {
            MapGlyphs.Highlight(drawList, cell, theme.HoverWash, RowWashInset, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(cell, expansion.Summary, HoverLabelSide.Above);
        }

        var disc = BrowseDiscSize * scale;
        var center = new Vector2(cell.Min.X + disc * 0.5f, cell.Center.Y);
        var tint = MapGlyphs.ExpansionTint(expansion.Order, accent);
        Squircle.FillCircleVerticalGradient(drawList, center, disc * 0.5f,
            ImGui.GetColorU32(Palette.Lighten(tint, 0.10f) with { W = 1f }),
            ImGui.GetColorU32(Palette.Darken(tint, 0.14f) with { W = 1f }));
        PhoneIcon.Draw(drawList, center, PhoneIcons.MapPin, AccentRing.Ink, BrowseGlyphSize * scale);
        var textLeft = center.X + disc * 0.5f + Metrics.Space.Md * scale;
        if (separated)
        {
            drawList.AddLine(new Vector2(textLeft, cell.Min.Y), new Vector2(cell.Max.X, cell.Min.Y),
                ImGui.GetColorU32(theme.Separator), Metrics.Stroke.Hairline);
        }

        var name = Typography.FitText(expansion.Name, MathF.Max(1f, cell.Max.X - textLeft), TextStyles.Subheadline);
        var nameSize = Typography.Measure(name, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Center.Y - nameSize.Y * 0.5f), name, theme.TextStrong,
            TextStyles.Subheadline);
        if (UiInteract.Click(cell.Min, cell.Max, hovered))
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
