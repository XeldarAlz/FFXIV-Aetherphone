using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const float SearchRowHeight = 46f;
    private const float FilterRowHeight = 44f;
    private const float FilterRailInset = 14f;
    private const float HeroHeight = 168f;
    private const float ShelfTileWidth = 108f;
    private const float GridMinTileWidth = 104f;
    private const float TileGap = 10f;
    private const float ShelfHeadingHeight = 32f;
    private const float FriendsCardHeight = 88f;
    private const float SearchRevealSeconds = 0.12f;
    private const float EntranceSpeed = 1.6f;
    private const int MinColumns = 3;
    private const int MaxColumns = 6;

    private readonly ChipRail filterRail = new();
    private readonly TileRail latestRail = new();
    private readonly TileRail recentRail = new();
    private readonly string[] filterLabels = new string[GamesLibrary.FilterCount];
    private readonly bool[] filterActive = new bool[GamesLibrary.FilterCount];
    private LibraryFilter filter;
    private bool searchOpen;
    private bool focusSearch;
    private bool scrollToTop;
    private string searchText = string.Empty;
    private string lastSearchText = string.Empty;
    private string dailyEyebrow = string.Empty;
    private string roomsLabel = string.Empty;
    private int roomsLabelCount = -1;
    private Spring searchReveal = new(0f);
    private Spring heroScale = new(1f);
    private float entrance;

    private void ResetLauncher()
    {
        filter = LibraryFilter.All;
        searchOpen = false;
        focusSearch = false;
        searchText = string.Empty;
        lastSearchText = string.Empty;
        searchReveal.SnapTo(0f);
        heroScale.SnapTo(1f);
        entrance = 0f;
        filterRail.Reset();
        latestRail.Reset();
        recentRail.Reset();
        Array.Clear(countLabels);
        roomsLabelCount = -1;
        dailyEyebrow = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Games.Daily));
    }

    private void DrawLauncher(Rect area)
    {
        var scale = UiScale.Current;
        frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        gameRooms.EnsureFresh();
        DrawLauncherHeader(area, scale);
        var y = area.Min.Y + AppHeader.Height * scale;
        y = DrawSearchRow(area, y, scale);
        y = DrawFilterRow(area, y, scale);
        if (!string.Equals(searchText, lastSearchText, StringComparison.Ordinal))
        {
            lastSearchText = searchText;
            entrance = 0f;
            scrollToTop = true;
        }

        entrance = GameJuice.Advance(entrance, frameSeconds, EntranceSpeed);
        var body = new Rect(new Vector2(area.Min.X, y), area.Max);
        using (AppSurface.Begin(body))
        {
            if (scrollToTop)
            {
                ImGui.SetScrollY(0f);
                scrollToTop = false;
            }

            if (searchText.AsSpan().Trim().Length > 0)
            {
                DrawSearchResults(body, scale);
            }
            else if (filter == LibraryFilter.All)
            {
                DrawHome(scale);
            }
            else
            {
                DrawFilteredPage(scale);
            }
        }
    }

    private void DrawLauncherHeader(Rect area, float scale)
    {
        var actions = new HeaderActions(area, scale, 1);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), new Vector2(area.Center.X, actions.RowCenterY),
            DisplayName, ui.TitleInk, 1.3f, FontWeight.Bold);
        var glyph = searchOpen ? FontAwesomeIcon.Times : FontAwesomeIcon.Search;
        if (!ui.IconButton(actions.Slot(0), actions.Radius, IconGlyph.Of(glyph), ui.HeaderInk, ui.FieldSurface,
                0.85f))
        {
            return;
        }

        searchOpen = !searchOpen;
        focusSearch = searchOpen;
        if (!searchOpen)
        {
            searchText = string.Empty;
        }
    }

    private float DrawSearchRow(Rect area, float y, float scale)
    {
        var target = searchOpen ? 1f : 0f;
        var reveal = searchReveal.Step(target, SearchRevealSeconds, frameSeconds);
        if (searchReveal.IsResting(target, 0.005f, 0.05f))
        {
            searchReveal.SnapTo(target);
            reveal = target;
        }

        var height = SearchRowHeight * scale * Math.Clamp(reveal, 0f, 1f);
        if (height < 1f)
        {
            return y;
        }

        var drawList = ImGui.GetWindowDrawList();
        var inset = Metrics.Space.Lg * scale;
        drawList.PushClipRect(new Vector2(area.Min.X, y), new Vector2(area.Max.X, y + height), true);
        var bar = new Rect(new Vector2(area.Min.X + inset, y + height - SearchRowHeight * scale),
            new Vector2(area.Max.X - inset, y + height));
        var palette = ui.Palette;
        SearchField.Draw(bar, "##gamesSearch", Loc.T(L.Games.SearchHint), ref searchText, palette.FieldSurface,
            palette.MutedInk, palette.TitleInk, new Vector4(1f, 1f, 1f, 0.14f), palette.BackdropBottom, 40,
            focusSearch);
        focusSearch = false;
        drawList.PopClipRect();
        return y + height;
    }

    private float DrawFilterRow(Rect area, float y, float scale)
    {
        var top = y + (FilterRowHeight - ChipRail.RowHeight) * 0.5f * scale;
        var row = new Rect(new Vector2(area.Min.X + FilterRailInset * scale, top),
            new Vector2(area.Max.X - FilterRailInset * scale, top + ChipRail.RowHeight * scale));
        for (var index = 0; index < GamesLibrary.FilterCount; index++)
        {
            filterLabels[index] = Loc.T(GamesLibrary.FilterLabel((LibraryFilter)index));
            filterActive[index] = index == (int)filter;
        }

        var tapped = filterRail.Draw(row, ui, filterLabels, filterActive, false, "games.filters",
            ChipRail.CompactLabelPadding + 6f);
        UiAnchors.Report("games.filters", row);
        if (tapped >= 0 && tapped != (int)filter)
        {
            filter = (LibraryFilter)tapped;
            entrance = 0f;
            scrollToTop = true;
        }

        return y + FilterRowHeight * scale;
    }

    private void DrawHome(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var y = origin.Y + Metrics.Space.Xs * scale;
        var featured = games[featuredIndex];
        var heroRect = new Rect(new Vector2(left, y), new Vector2(left + width, y + HeroHeight * scale));
        UiAnchors.Report("games.featured", heroRect);
        if (DrawHero(heroRect, featured, Easing.EaseOutCubic(entrance), scale))
        {
            OpenGame(featured);
        }

        y = heroRect.Max.Y + Metrics.Space.Lg * scale;
        var latest = library.Latest;
        if (latest.Length > 0)
        {
            y = DrawShelf(Loc.T(L.Games.ShelfLatest), latest, latestRail, left, y, width, scale);
        }

        var recent = library.Recent;
        if (recent.Length > 0)
        {
            y = DrawShelf(Loc.T(L.Games.ShelfRecent), recent, recentRail, left, y, width, scale);
        }

        y = DrawFriendsRow(left, y, width, scale);
        var all = library.Ordered;
        DrawShelfHeading(Loc.T(L.Games.LibraryHeading), CountLabel(all.Length), left, y, width, scale);
        y += ShelfHeadingHeight * scale;
        y = DrawGrid(all, left, y, width, scale);
        FinishPage(origin, width, y, scale);
    }

    private void DrawFilteredPage(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var y = origin.Y + Metrics.Space.Xs * scale;
        var entries = library.Filter(filter, string.Empty);
        if (filter == LibraryFilter.Friends)
        {
            y = DrawFriendsRow(left, y, width, scale);
        }

        DrawShelfHeading(Loc.T(GamesLibrary.FilterLabel(filter)), CountLabel(entries.Length), left, y, width, scale);
        y += ShelfHeadingHeight * scale;
        var gridTop = y;
        y = DrawGrid(entries, left, y, width, scale);
        ReportVisible("games.filtered", new Rect(new Vector2(left, gridTop), new Vector2(left + width, y)));
        FinishPage(origin, width, y, scale);
    }

    private void DrawSearchResults(Rect body, float scale)
    {
        var entries = library.Filter(filter, searchText);
        if (entries.Length == 0)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Search, Loc.T(L.Games.SearchEmpty),
                Loc.T(L.Games.SearchEmptyHint));
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var y = origin.Y + Metrics.Space.Xs * scale;
        DrawShelfHeading(CountLabel(entries.Length), string.Empty, left, y, width, scale);
        y += ShelfHeadingHeight * scale;
        y = DrawGrid(entries, left, y, width, scale);
        FinishPage(origin, width, y, scale);
    }

    private float DrawFriendsRow(float left, float y, float width, float scale)
    {
        var layout = MeasureFriendsCard(width, scale);
        var rect = new Rect(new Vector2(left, y), new Vector2(left + width, y + layout.Height));
        ReportVisible("games.friends", rect);
        if (DrawFriendsCard(rect, layout, scale))
        {
            OpenOnlineHub(GameRoomWire.UnoKind);
        }

        return rect.Max.Y + Metrics.Space.Lg * scale;
    }

    private float DrawShelf(string heading, ReadOnlySpan<int> entries, TileRail rail, float left, float y,
        float width, float scale)
    {
        DrawShelfHeading(heading, string.Empty, left, y, width, scale);
        y += ShelfHeadingHeight * scale;
        var tileWidth = ShelfTileWidth * scale;
        var gap = TileGap * scale;
        var tileHeight = TileHeight(tileWidth, scale);
        var pad = Metrics.Space.Lg * scale;
        var row = new Rect(new Vector2(left - pad, y),
            new Vector2(left + width + pad, y + tileHeight + Metrics.Space.Sm * scale));
        var contentWidth = pad * 2f + entries.Length * (tileWidth + gap) - gap;
        var drawList = ImGui.GetWindowDrawList();
        var rowHovered = rail.Begin(drawList, row, contentWidth);
        var interactive = rowHovered && rail.TapAllowed;
        var x = left - rail.Offset;
        var activate = -1;
        for (var index = 0; index < entries.Length; index++)
        {
            var rect = new Rect(new Vector2(x, y), new Vector2(x + tileWidth, y + tileHeight));
            if (rect.Max.X >= row.Min.X && rect.Min.X <= row.Max.X
                && DrawTile(rect, entries[index], GameJuice.Stagger(entrance, index, entries.Length), interactive))
            {
                activate = entries[index];
            }

            x += tileWidth + gap;
        }

        TileRail.End(drawList);
        if (activate >= 0)
        {
            Activate(activate);
        }

        return y + tileHeight + Metrics.Space.Lg * scale;
    }

    private float DrawGrid(ReadOnlySpan<int> entries, float left, float y, float width, float scale)
    {
        if (entries.Length == 0)
        {
            return y;
        }

        var gap = TileGap * scale;
        var columns = Math.Clamp((int)((width + gap) / (GridMinTileWidth * scale + gap)), MinColumns, MaxColumns);
        var tileWidth = (width - gap * (columns - 1)) / columns;
        var tileHeight = TileHeight(tileWidth, scale);
        var drawList = ImGui.GetWindowDrawList();
        var clipMin = drawList.GetClipRectMin();
        var clipMax = drawList.GetClipRectMax();
        var activate = -1;
        for (var index = 0; index < entries.Length; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var min = new Vector2(left + column * (tileWidth + gap), y + row * (tileHeight + gap));
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (rect.Max.Y < clipMin.Y || rect.Min.Y > clipMax.Y)
            {
                continue;
            }

            if (DrawTile(rect, entries[index], GameJuice.Stagger(entrance, index, entries.Length), true))
            {
                activate = entries[index];
            }
        }

        if (activate >= 0)
        {
            Activate(activate);
        }

        var rows = (entries.Length + columns - 1) / columns;
        return y + rows * (tileHeight + gap) - gap;
    }

    private static void ReportVisible(string anchorKey, Rect rect)
    {
        var drawList = ImGui.GetWindowDrawList();
        var top = MathF.Max(rect.Min.Y, drawList.GetClipRectMin().Y);
        var bottom = MathF.Min(rect.Max.Y, drawList.GetClipRectMax().Y);
        if (bottom <= top)
        {
            return;
        }

        UiAnchors.Report(anchorKey, new Rect(new Vector2(rect.Min.X, top), new Vector2(rect.Max.X, bottom)));
    }

    private static void FinishPage(Vector2 origin, float width, float bottom, float scale)
    {
        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
        ImGui.Dummy(new Vector2(width, Metrics.Space.Xl * scale));
    }

    private string CountLabel(int count)
    {
        var label = countLabels[count];
        if (label is null)
        {
            label = Loc.Plural(L.Games.GameCount, count);
            countLabels[count] = label;
        }

        return label;
    }

    private string RoomsLabel(int count)
    {
        if (count != roomsLabelCount)
        {
            roomsLabel = Loc.Plural(L.Games.OnlineRoomsOpen, count);
            roomsLabelCount = count;
        }

        return roomsLabel;
    }

    private void Activate(int entryIndex)
    {
        ref readonly var entry = ref library.Entries[entryIndex];
        if (entry.Online)
        {
            OpenOnlineHub(entry.OnlineKind);
            return;
        }

        OpenGame(games[entry.GameIndex]);
    }
}
